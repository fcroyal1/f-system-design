using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Procurement.TestSupport;

namespace Procurement.Api.Tests;

/// <summary>
/// The API host wired to its own freshly published database. Used as a class fixture, so each
/// test class gets one database; a test that needs a pristine database can create its own.
/// </summary>
public sealed class ApiFactory(SqlServerFixture sql) : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? _connectionString;

    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("ApiFactory was not initialized.");

    public async ValueTask InitializeAsync()
    {
        // Without Docker the tests skip themselves (SkipIfNoDocker.Check); the fixture must not fail.
        if (SkipIfNoDocker.IsDockerAvailable)
        {
            _connectionString = await sql.CreateDatabaseAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Procurement"] = ConnectionString,
        }));

    public async Task<SqlConnection> OpenDbAsync()
    {
        var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }
}

internal static class Http
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return await response.JsonAsync();
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, Ct);

    /// <summary>Asserts the status code, showing the response body when it differs.</summary>
    public static async Task AssertStatusAsync(this HttpResponseMessage response, System.Net.HttpStatusCode status)
    {
        if (response.StatusCode != status)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.Fail($"Expected {(int)status} {status} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    /// <summary>Asserts a ProblemDetails response with the given status and returns its detail.</summary>
    public static async Task<string> ProblemAsync(this HttpResponseMessage response, System.Net.HttpStatusCode status)
    {
        await response.AssertStatusAsync(status);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.JsonAsync();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        var detail = problem.GetProperty("detail").GetString();
        Assert.False(string.IsNullOrWhiteSpace(detail));
        return detail!;
    }
}
