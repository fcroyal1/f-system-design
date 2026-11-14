using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Procurement.Bff;

/// <summary>The API could not be reached, timed out, answered 5xx, or sent a body that is not JSON.</summary>
public sealed class ApiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The API answered 404. <see cref="Detail"/> is the API's own detail sentence when it sent one.</summary>
public sealed class ApiNotFoundException(string? detail) : Exception(detail ?? "The API returned 404.")
{
    public string? Detail { get; } = detail;
}

/// <summary>The API answered another 4xx. The status and body are passed back to the browser unchanged.</summary>
public sealed class ApiClientErrorException(int status, string body, string? contentType)
    : Exception($"The API returned {status}.")
{
    public int Status { get; } = status;
    public string Body { get; } = body;
    public string? ContentType { get; } = contentType;
}

/// <summary>
/// Typed HttpClient for the BFF's own composed views. It only reads JSON and classifies failures;
/// it never interprets business data.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    /// <param name="relativePath">A path relative to <c>Api:BaseUrl</c>, without a leading slash, e.g. <c>api/kpis</c>.</param>
    public async Task<JsonNode> GetJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            // ResponseContentRead (the default) buffers the whole body inside GetAsync, so HttpClient.Timeout
            // covers a body that stalls, and a connection dropped mid-body surfaces here as HttpRequestException.
            response = await http.GetAsync(relativePath, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException($"GET {relativePath} failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient.Timeout surfaces as a TaskCanceledException that the caller did not ask for.
            throw new ApiUnavailableException($"GET {relativePath} timed out.", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            // Already buffered in memory, so this read cannot touch the network.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (status >= 500)
            {
                throw new ApiUnavailableException($"GET {relativePath} returned {status}.");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ApiNotFoundException(TryReadDetail(body));
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ApiClientErrorException(status, body, response.Content.Headers.ContentType?.ToString());
            }

            try
            {
                return JsonNode.Parse(body) ?? throw new ApiUnavailableException($"GET {relativePath} returned null.");
            }
            catch (JsonException ex)
            {
                throw new ApiUnavailableException($"GET {relativePath} returned a body that is not JSON.", ex);
            }
        }
    }

    private static string? TryReadDetail(string body)
    {
        try
        {
            return JsonNode.Parse(body)?["detail"]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
