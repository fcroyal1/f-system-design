using Microsoft.Data.SqlClient;
using Procurement.Api.Data;
using static Procurement.Api.Features.Validation;

namespace Procurement.Api.Features.Suppliers;

public sealed class SupplierService(Db db, SupplierStore store, AuditLog audit)
{
    private const string Active = "Active";
    private const string Blocked = "Blocked";

    /// <summary>
    /// Updates a supplier and writes what changed to the audit trail: one SupplierUpdated row for ordinary
    /// fields, SupplierBlocked / SupplierUnblocked for the status, and a separate SupplierBankChanged row,
    /// because a changed bank account is the classic invoice-fraud vector and reviewers look for it.
    /// </summary>
    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        var legalName = Required(request.LegalName, "legalName", 200);
        var categoryCode = Required(request.CategoryCode, "categoryCode", 20);
        var terms = PaymentTerms(request.PaymentTermsDays);
        var newTaxId = Masking.Last4(request.TaxId, "taxId");
        var newBank = Masking.Last4(request.BankAccount, "bankAccount");

        await db.InTransactionAsync(async (connection, transaction) =>
        {
            var current = await SupplierStore.LockAsync(connection, transaction, id)
                ?? throw new NotFoundProblemException($"Supplier {id} was not found.");

            if (categoryCode != current.CategoryCode
                && !await ReferenceStore.CategoryExistsAsync(connection, transaction, categoryCode))
            {
                throw new ValidationProblemException($"categoryCode '{categoryCode}' is not a known spend category.");
            }

            var next = new SupplierChanges(
                id, legalName, categoryCode, terms,
                newTaxId ?? current.TaxIdLast4,
                newBank ?? current.BankLast4,
                request.IsBlocked ?? current.IsBlocked);

            await SupplierStore.UpdateAsync(connection, transaction, next);

            var changes = new List<string>();
            if (next.LegalName != current.LegalName)
            {
                changes.Add($"legal name '{current.LegalName}' → '{next.LegalName}'");
            }

            if (next.CategoryCode != current.CategoryCode)
            {
                changes.Add($"category {current.CategoryCode} → {next.CategoryCode}");
            }

            if (next.PaymentTermsDays != current.PaymentTermsDays)
            {
                changes.Add($"payment terms {current.PaymentTermsDays} → {next.PaymentTermsDays} days");
            }

            if (next.TaxIdLast4 != current.TaxIdLast4)
            {
                changes.Add($"tax ID {Shown(current.TaxIdLast4)} → {Shown(next.TaxIdLast4)}");
            }

            if (changes.Count > 0)
            {
                await audit.WriteAsync(connection, transaction, "SupplierUpdated", "Supplier", id, null, null,
                    "Changed " + string.Join(", ", changes));
            }

            if (next.IsBlocked != current.IsBlocked)
            {
                await audit.WriteAsync(connection, transaction,
                    next.IsBlocked ? "SupplierBlocked" : "SupplierUnblocked", "Supplier", id,
                    current.IsBlocked ? Blocked : Active, next.IsBlocked ? Blocked : Active,
                    next.IsBlocked ? "New invoices from this supplier are blocked." : "Supplier can be invoiced again.");
            }

            if (next.BankLast4 != current.BankLast4)
            {
                await audit.WriteAsync(connection, transaction, "SupplierBankChanged", "Supplier", id, null, null,
                    $"Bank account {Shown(current.BankLast4)} → {Shown(next.BankLast4)}. Confirm with the supplier before paying.");
            }

            return 0;
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<IReadOnlyList<SupplierHistoryDto>> HistoryAsync(Guid id, CancellationToken cancellationToken)
    {
        _ = await store.GetAsync(id, cancellationToken)
            ?? throw new NotFoundProblemException($"Supplier {id} was not found.");
        return (await store.HistoryAsync(id, cancellationToken))
            .Select(h => new SupplierHistoryDto(h.OccurredAt, h.Actor, h.Action, h.FromStatus, h.ToStatus, h.Detail))
            .ToList();
    }

    private static string Shown(string? last4) => Masking.Mask(last4) ?? "none";

    private static int PaymentTerms(int? value) => value switch
    {
        null => throw new ValidationProblemException("paymentTermsDays is required."),
        < 0 => throw new ValidationProblemException("paymentTermsDays must be 0 or more."),
        _ => value.Value,
    };

    public async Task<SupplierDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await store.GetAsync(id, cancellationToken)
            ?? throw new NotFoundProblemException($"Supplier {id} was not found.");
        return SupplierDto.From(row);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = Validate(request);

        try
        {
            await db.InTransactionAsync(async (connection, transaction) =>
            {
                if (!await ReferenceStore.CategoryExistsAsync(connection, transaction, supplier.CategoryCode))
                {
                    throw new ValidationProblemException($"categoryCode '{supplier.CategoryCode}' is not a known spend category.");
                }

                if (await SupplierStore.CodeExistsAsync(connection, transaction, supplier.Code))
                {
                    throw DuplicateCode(supplier.Code);
                }

                await SupplierStore.InsertAsync(connection, transaction, supplier);
                return 0;
            }, cancellationToken);
        }
        catch (SqlException e) when (e.Number is 2627 or 2601)
        {
            throw DuplicateCode(supplier.Code);
        }

        return await GetAsync(supplier.Id, cancellationToken);
    }

    private static ConflictProblemException DuplicateCode(string code) =>
        new($"A supplier with code {code} already exists.");

    private static NewSupplier Validate(CreateSupplierRequest request)
    {
        var code = Required(request.Code, "code", 20);
        var legalName = Required(request.LegalName, "legalName", 200);
        var categoryCode = Required(request.CategoryCode, "categoryCode", 20);

        if (request.PaymentTermsDays is not { } terms)
        {
            throw new ValidationProblemException("paymentTermsDays is required.");
        }

        if (terms < 0)
        {
            throw new ValidationProblemException("paymentTermsDays must be 0 or more.");
        }

        return new NewSupplier(
            Guid.NewGuid(), code, legalName, categoryCode, terms,
            Masking.Last4(request.TaxId, "taxId"),
            Masking.Last4(request.BankAccount, "bankAccount"));
    }
}
