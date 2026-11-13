using Microsoft.Data.SqlClient;
using Procurement.Api.Data;
using static Procurement.Api.Features.Validation;

namespace Procurement.Api.Features.Suppliers;

public sealed class SupplierService(Db db, SupplierStore store)
{
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
