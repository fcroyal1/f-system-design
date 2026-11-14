using System.Text.Json.Nodes;

namespace Procurement.Bff.Views;

/// <summary>GET /bff/purchase-orders/{id}: the API's PurchaseOrderDto and SupplierDto, unchanged.</summary>
public sealed record PurchaseOrderView(JsonNode Po, JsonNode Supplier);

/// <summary>
/// GET /bff/invoices/{id}: the API's InvoiceDto, its PurchaseOrderDto (null when the invoice has no PO)
/// and its SupplierDto, unchanged.
/// </summary>
public sealed record InvoiceView(JsonNode Invoice, JsonNode? Po, JsonNode Supplier);
