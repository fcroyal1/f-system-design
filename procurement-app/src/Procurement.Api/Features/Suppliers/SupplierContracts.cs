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
