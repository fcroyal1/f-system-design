using System.Globalization;
using Procurement.Api.Data;
using Procurement.Domain.Policies;
using static Procurement.Api.Features.Validation;

namespace Procurement.Api.Features.PurchaseOrders;

public sealed class PurchaseOrderService(Db db, PurchaseOrderStore store, AuditLog audit, Actor actor)
{
    // Column limits: quantity decimal(18,3), unit_price decimal(18,4).
    private const decimal MaxQuantity = 999_999_999_999_999.999m;
    private const decimal MaxUnitPrice = 99_999_999_999_999.9999m;

    public async Task<PurchaseOrderDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await store.GetAsync(id, cancellationToken)
            ?? throw new NotFoundProblemException($"Purchase order {id} was not found.");
        return PurchaseOrderDto.From(rows);
    }

    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var po = Validate(request);

        await db.InTransactionAsync(async (connection, transaction) =>
        {
            if (!await SupplierStore.ExistsAsync(connection, transaction, po.SupplierId))
            {
                throw new ValidationProblemException($"supplierId {po.SupplierId} is not a known supplier.");
            }

            if (!await ReferenceStore.CostCenterExistsAsync(connection, transaction, po.CostCenterCode))
            {
                throw new ValidationProblemException($"costCenterCode '{po.CostCenterCode}' is not a known cost center.");
            }

            var glAccounts = await ReferenceStore.GlAccountNumbersAsync(connection, transaction);
            var unknown = po.Lines.FirstOrDefault(l => !glAccounts.Contains(l.GlAccount));
            if (unknown is not null)
            {
                throw new ValidationProblemException($"Line {unknown.LineNo}: glAccount '{unknown.GlAccount}' is not a known GL account.");
            }

            return await PurchaseOrderStore.InsertDraftAsync(connection, transaction, po);
        }, cancellationToken);

        return await GetAsync(po.Id, cancellationToken);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        await db.InTransactionAsync(async (connection, transaction) =>
        {
            var po = await PurchaseOrderStore.LockForApprovalAsync(connection, transaction, id)
                ?? throw new NotFoundProblemException($"Purchase order {id} was not found.");

            if (po.Status != "Draft")
            {
                throw new RuleViolationProblemException($"PO {po.Number} is {po.Status}; only Draft POs can be approved");
            }

            var tiers = await ReferenceStore.GetDoaTiersAsync(connection, transaction);
            if (tiers.Count == 0 || po.Total > tiers.Max(t => t.MaxAmount))
            {
                throw new RuleViolationProblemException(
                    $"No delegation-of-authority tier covers PO {po.Number}'s total of {Format(po.Total)}.");
            }

            var approverRole = ApprovalRouter.Route(po.Total, tiers);
            await PurchaseOrderStore.MarkApprovedAsync(connection, transaction, id, approverRole, actor.Name);
            await audit.WriteAsync(connection, transaction, "PoApproved", "PurchaseOrder", id, "Draft", "Approved",
                $"PO {po.Number} total {Format(po.Total)} routed to {approverRole}");
            return 0;
        }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private static string Format(decimal amount) => Money.Display(amount).ToString("N2", CultureInfo.InvariantCulture);

    private static NewPurchaseOrder Validate(CreatePurchaseOrderRequest request)
    {
        if (request.SupplierId is not { } supplierId || supplierId == Guid.Empty)
        {
            throw new ValidationProblemException("supplierId is required.");
        }

        var requesterName = Required(request.RequesterName, "requesterName", 100);
        var costCenterCode = Required(request.CostCenterCode, "costCenterCode", 20);
        var currency = Required(request.Currency, "currency", 3).ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            throw new ValidationProblemException("currency must be a 3-letter ISO code such as USD.");
        }

        if (request.PoDate is not { } poDate)
        {
            throw new ValidationProblemException("poDate is required (yyyy-MM-dd).");
        }

        if (request.Lines is not { Count: > 0 } lines)
        {
            throw new ValidationProblemException("A purchase order needs at least one line.");
        }

        var newLines = lines.Select((line, index) => ValidateLine(line, index + 1)).ToList();
        return new NewPurchaseOrder(
            Guid.NewGuid(), supplierId, requesterName, costCenterCode, currency, poDate.ToDateTime(TimeOnly.MinValue), newLines);
    }

    private static NewPoLine ValidateLine(CreatePoLineRequest? line, int lineNo)
    {
        if (line is null)
        {
            throw new ValidationProblemException($"Line {lineNo} is empty.");
        }

        string Field(string? value, string name, int maxLength)
        {
            try
            {
                return Required(value, name, maxLength);
            }
            catch (ValidationProblemException e)
            {
                throw new ValidationProblemException($"Line {lineNo}: {e.Message}");
            }
        }

        var itemCode = Field(line.ItemCode, "itemCode", 40);
        var description = Field(line.Description, "description", 200);
        var uom = Field(line.Uom, "uom", 10);
        var glAccount = Field(line.GlAccount, "glAccount", 10);

        if (line.Quantity is not { } quantity)
        {
            throw new ValidationProblemException($"Line {lineNo}: quantity is required.");
        }

        if (quantity <= 0)
        {
            throw new ValidationProblemException($"Line {lineNo}: quantity must be greater than 0.");
        }

        if (quantity > MaxQuantity || HasMoreDecimals(quantity, 3))
        {
            throw new ValidationProblemException($"Line {lineNo}: quantity must have at most 3 decimal places and 15 digits before the point.");
        }

        if (line.UnitPrice is not { } unitPrice)
        {
            throw new ValidationProblemException($"Line {lineNo}: unitPrice is required.");
        }

        if (unitPrice < 0)
        {
            throw new ValidationProblemException($"Line {lineNo}: unitPrice must be 0 or more.");
        }

        if (unitPrice > MaxUnitPrice || HasMoreDecimals(unitPrice, 4))
        {
            throw new ValidationProblemException($"Line {lineNo}: unitPrice must have at most 4 decimal places and 14 digits before the point.");
        }

        return new NewPoLine(lineNo, itemCode, description, uom, quantity, unitPrice, glAccount, line.ReceiptRequired ?? true);
    }

    private static bool HasMoreDecimals(decimal value, int places) => Math.Round(value, places) != value;
}
