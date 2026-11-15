using Procurement.Api.Data;

namespace Procurement.Api.Features.Suppliers;

/// <summary>POST /api/suppliers body. Fields are nullable so missing ones produce a readable 400.</summary>
public sealed record CreateSupplierRequest(
    string? Code,
    string? LegalName,
    string? CategoryCode,
    int? PaymentTermsDays,
    string? TaxId,
    string? BankAccount);

/// <summary>
/// PUT /api/suppliers/{id} body. The code never changes (other records and the seed refer to it).
/// TaxId / BankAccount: omit or null to keep the stored value; send a new value to replace it.
/// IsBlocked: omit or null to keep.
/// </summary>
public sealed record UpdateSupplierRequest(
    string? LegalName,
    string? CategoryCode,
    int? PaymentTermsDays,
    string? TaxId,
    string? BankAccount,
    bool? IsBlocked);

/// <summary>One audit-trail entry about a supplier, newest first.</summary>
public sealed record SupplierHistoryDto(
    DateTime OccurredAt,
    string Actor,
    string Action,
    string? FromStatus,
    string? ToStatus,
    string? Detail);

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string LegalName,
    string CategoryCode,
    string CategoryName,
    int PaymentTermsDays,
    string? TaxIdMasked,
    string? BankMasked,
    bool IsBlocked)
{
    public static SupplierDto From(SupplierRow row) => new(
        row.Id, row.Code, row.LegalName, row.CategoryCode, row.CategoryName, row.PaymentTermsDays,
        Masking.Mask(row.TaxIdLast4), Masking.Mask(row.BankLast4), row.IsBlocked);
}
