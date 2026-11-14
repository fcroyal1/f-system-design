using Procurement.Bff;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// The only thing the BFF knows about the API is where it lives. Compose sets Api__BaseUrl.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl) || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri))
{
    throw new InvalidOperationException("Configuration value Api:BaseUrl must be an absolute URL, e.g. http://api:8080.");
}

// A trailing slash keeps any path prefix on the base URL when relative paths are resolved against it.
if (!apiBaseUri.AbsoluteUri.EndsWith('/'))
{
    apiBaseUri = new Uri(apiBaseUri.AbsoluteUri + "/");
}

builder.Services.AddProblemDetails();

// /api/* is forwarded unchanged (method, path, query, body, status and error bodies).
builder.Services.AddReverseProxy().LoadFromMemory(
    [
        new RouteConfig
        {
            RouteId = "api",
            ClusterId = "api",
            Match = new RouteMatch { Path = "/api/{**catch-all}" },
        },
    ],
    [
        new ClusterConfig
        {
            ClusterId = "api",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["api"] = new() { Address = apiBaseUri.AbsoluteUri },
            },
        },
    ]);

// /bff/* composes API reads into screen-shaped views.
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Api:TimeoutSeconds", 15));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles(); // "/" serves wwwroot/index.html
app.UseStaticFiles();

app.MapReverseProxy();
app.MapBffEndpoints();

// Angular deep links. Paths under /api and /bff are excluded here as well as being claimed by the routes above,
// so an unknown API or BFF path is a 404 and never the SPA shell. "nonfile" keeps missing assets (*.js) as 404s.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!(api|bff)(/|$)))}", "index.html");

app.Run();

/// <summary>Exposed for WebApplicationFactory in the BFF tests.</summary>
public partial class Program;
