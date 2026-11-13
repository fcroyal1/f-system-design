using Procurement.Api;
using Procurement.Api.Data;
using Procurement.Api.Features.PurchaseOrders;
using Procurement.Api.Features.Reference;
using Procurement.Api.Features.Suppliers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    // Status-code-only responses (e.g. an unmatched route) still get a readable detail.
    if (context.ProblemDetails.Detail is null && context.ProblemDetails.Status == StatusCodes.Status404NotFound)
    {
        context.ProblemDetails.Detail = $"Nothing was found at {context.HttpContext.Request.Path}.";
    }
});
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
// Malformed JSON bodies throw BadHttpRequestException, which ProblemExceptionHandler turns into a 400.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton(sp => Actor.FromConfiguration(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<AuditLog>();
builder.Services.AddSingleton<ReferenceStore>();
builder.Services.AddSingleton<SupplierStore>();
builder.Services.AddSingleton<PurchaseOrderStore>();
builder.Services.AddScoped<SupplierService>();
builder.Services.AddScoped<PurchaseOrderService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapReferenceEndpoints();
app.MapSupplierEndpoints();
app.MapPurchaseOrderEndpoints();

app.Run();

/// <summary>Exposed for WebApplicationFactory in the API tests.</summary>
public partial class Program;
