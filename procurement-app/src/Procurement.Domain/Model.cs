namespace Procurement.Domain;

public enum PoStatus { Draft, Approved, Closed }

public enum InvoiceStatus { Matched, Blocked }

public enum CheckOutcome { Pass, Fail }

public record PoLine(int LineNo, string ItemCode, string Uom, decimal Quantity, decimal UnitPrice, bool ReceiptRequired = true);

public record InvoiceLine(int LineNo, int? PoLineNo, string ItemCode, string Uom, decimal Quantity, decimal UnitPrice);
