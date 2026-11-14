using System.Text.Json.Nodes;

namespace Procurement.Bff.Views;

/// <summary>GET /bff/dashboard: the KPI snapshot as the API sent it, plus the newest Blocked invoices.</summary>
public sealed record DashboardView(JsonNode Kpis, IReadOnlyList<ExceptionItem> Exceptions);

/// <summary>One row of the dashboard's exception list.</summary>
public sealed record ExceptionItem(Guid Id, string InvoiceNumber, string SupplierName, decimal Total, string? BlockReason);

/// <summary>The fields the BFF reads from an API <c>InvoiceListItemDto</c>; everything else is ignored.</summary>
public sealed record InvoiceListItem(
    Guid Id,
    string InvoiceNumber,
    string SupplierName,
    DateOnly InvoiceDate,
    decimal Total,
    string? BlockReason);
