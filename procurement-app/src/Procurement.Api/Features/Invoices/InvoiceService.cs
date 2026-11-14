using Microsoft.Data.SqlClient;
using Procurement.Api.Data;
using Procurement.Domain;
using Procurement.Domain.Matching;
using Procurement.Domain.Policies;
using static Procurement.Api.Features.Validation;

namespace Procurement.Api.Features.Invoices;

public sealed class InvoiceService(Db db, InvoiceStore store, AuditLog audit)
{
    private const string Matched = "Matched";
    private const string Blocked = "Blocked";

    public async Task<InvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await store.GetAsync(id, cancellationToken)
            ?? throw new NotFoundProblemException($"Invoice {id} was not found.");
        return InvoiceDto.From(rows);
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(string? status, CancellationToken cancellationToken)
    {
        if (status is not null)
        {
            status = new[] { Matched, Blocked }.FirstOrDefault(s => string.Equals(s, status.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new ValidationProblemException("status must be Matched or Blocked.");
        }

        return (await store.ListAsync(status, cancellationToken)).Select(InvoiceListItemDto.From).ToList();
    }

    public async Task<InvoiceDto> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var input = Validate(request);
        string? supplierName = null;

        try
        {
            await db.InTransactionAsync(async (connection, transaction) =>
            {
                var supplier = await SupplierStore.FindAsync(connection, transaction, input.SupplierId)
                    ?? throw new ValidationProblemException($"supplierId {input.SupplierId} is not a known supplier.");
                supplierName = supplier.LegalName;

                // PO first: receipts and re-matches lock the PO before the invoice rows, and so do we.
                var po = await InvoiceStore.LockPoByNumberAsync(connection, transaction, input.PoNumber);

                if (await InvoiceStore.FindNumberAsync(connection, transaction, supplier.Id, input.NormalizedNumber) is { } existing)
                {
                    throw Duplicate(existing, supplier.LegalName);
                }

                if (input.InvoiceDate > DateOnly.MaxValue.AddDays(-supplier.PaymentTermsDays))
                {
                    throw new ValidationProblemException("invoiceDate plus the supplier's payment terms is past the last supported date.");
                }

                var dueDate = input.InvoiceDate.AddDays(supplier.PaymentTermsDays);
                var isRetro = po is not null && RetroPoRule.IsRetro(input.InvoiceDate, DateOnly.FromDateTime(po.PoDate));
                var unknownPoReason = po is null ? $"PO: PO {input.PoNumber} not found" : null;

                await InvoiceStore.InsertAsync(connection, transaction, new NewInvoice(
                    input.Id, supplier.Id, input.InvoiceNumber, input.NormalizedNumber,
                    input.InvoiceDate.ToDateTime(TimeOnly.MinValue), dueDate.ToDateTime(TimeOnly.MinValue),
                    input.Currency, po?.Id, input.TaxAmount, unknownPoReason, isRetro, input.Lines));

                if (unknownPoReason is not null)
                {
                    // Saved Blocked without running the match: no check rows, first_match_passed stays NULL.
                    await audit.WriteAsync(connection, transaction, "InvoiceBlocked", "Invoice", input.Id, null, Blocked,
                        AuditDetail(input.InvoiceNumber, unknownPoReason));
                    return 0;
                }

                await EvaluateAsync(connection, transaction, input.Id, isNew: true);
                return 0;
            }, cancellationToken);
        }
        catch (SqlException e) when (e.Number is 2627 or 2601)
        {
            // Backstop for the unique index; the in-transaction check normally answers first.
            await using var connection = await db.OpenAsync(cancellationToken);
            var existing = await InvoiceStore.FindNumberAsync(connection, null, input.SupplierId, input.NormalizedNumber);
            throw Duplicate(existing ?? input.InvoiceNumber, supplierName ?? input.SupplierId.ToString());
        }

        return await GetAsync(input.Id, cancellationToken);
    }

    public async Task<InvoiceDto> RematchAsync(Guid id, CancellationToken cancellationToken)
    {
        await db.InTransactionAsync((connection, transaction) => EvaluateAsync(connection, transaction, id), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Re-evaluates an invoice inside the caller's transaction: a blocked supplier keeps it Blocked
    /// without running the match; otherwise <see cref="MatchAsync"/> decides.
    /// </summary>
    public async Task<InvoiceOutcome> EvaluateAsync(SqlConnection connection, SqlTransaction transaction, Guid id, bool isNew = false)
    {
        var (invoice, po) = await LockAsync(connection, transaction, id);
        var fromStatus = isNew ? null : invoice.Status;

        if (invoice.SupplierBlocked)
        {
            var reason = $"Supplier: supplier {invoice.SupplierCode} is blocked";
            await InvoiceStore.ReplaceChecksAsync(connection, transaction, id, []);
            await InvoiceStore.SetOutcomeAsync(connection, transaction, id, Blocked, reason, matchPassed: null);
            await audit.WriteAsync(connection, transaction, "InvoiceBlocked", "Invoice", id, fromStatus, Blocked,
                AuditDetail(invoice.InvoiceNumber, reason));
            return new InvoiceOutcome(id, invoice.InvoiceNumber, fromStatus, Blocked, reason);
        }

        var result = await MatchLockedAsync(connection, transaction, invoice, po, fromStatus);
        return new InvoiceOutcome(id, invoice.InvoiceNumber, fromStatus, result.Passed ? Matched : Blocked, result.BlockReason);
    }

    /// <summary>
    /// Runs the 3-way match for an invoice inside the caller's transaction: replaces its check rows,
    /// sets status and reason, records the first match's result once, and writes the audit row.
    /// </summary>
    public async Task<MatchResult> MatchAsync(SqlConnection connection, SqlTransaction transaction, Guid invoiceId)
    {
        var (invoice, po) = await LockAsync(connection, transaction, invoiceId);
        return await MatchLockedAsync(connection, transaction, invoice, po, invoice.Status);
    }

    private async Task<MatchResult> MatchLockedAsync(
        SqlConnection connection, SqlTransaction transaction, MatchInvoiceRow invoice, MatchPoRow po, string? fromStatus)
    {
        var poLines = await InvoiceStore.PoLinesAsync(connection, transaction, po.Id);
        var received = await InvoiceStore.ReceivedByLineAsync(connection, transaction, po.Id);
        var previouslyInvoiced = await InvoiceStore.PreviouslyInvoicedByLineAsync(connection, transaction, po.Id, invoice.Id);
        var invoiceLines = await InvoiceStore.LinesAsync(connection, transaction, invoice.Id);

        var result = MatchEngine.Match(new MatchInput(
            invoice.SupplierId == po.SupplierId,
            po.Currency,
            invoice.Currency,
            Enum.Parse<PoStatus>(po.Status),
            poLines.Select(l => new PoLine(l.LineNo, l.ItemCode, l.Uom, l.Quantity, l.UnitPrice, l.ReceiptRequired)).ToList(),
            received,
            previouslyInvoiced,
            invoiceLines.Select(l => new InvoiceLine(l.LineNo, l.PoLineNo, l.ItemCode, l.Uom, l.Quantity, l.UnitPrice)).ToList()),
            Tolerance.Default);

        var status = result.Passed ? Matched : Blocked;
        await InvoiceStore.ReplaceChecksAsync(connection, transaction, invoice.Id, result.Checks);
        await InvoiceStore.SetOutcomeAsync(connection, transaction, invoice.Id, status, result.BlockReason, result.Passed);
        await audit.WriteAsync(connection, transaction, result.Passed ? "InvoiceMatched" : "InvoiceBlocked", "Invoice",
            invoice.Id, fromStatus, status, AuditDetail(invoice.InvoiceNumber, result.BlockReason ?? "all checks passed"));
        return result;
    }

    /// <summary>Locks the invoice's PO, then the invoice, in the same order receipts use.</summary>
    private static async Task<(MatchInvoiceRow Invoice, MatchPoRow Po)> LockAsync(
        SqlConnection connection, SqlTransaction transaction, Guid id)
    {
        var (exists, poId) = await InvoiceStore.FindPoIdAsync(connection, transaction, id);
        if (!exists)
        {
            throw new NotFoundProblemException($"Invoice {id} was not found.");
        }

        var po = poId is { } knownPoId ? await InvoiceStore.LockPoAsync(connection, transaction, knownPoId) : null;
        var invoice = await InvoiceStore.LockInvoiceAsync(connection, transaction, id);
        if (po is null)
        {
            throw new RuleViolationProblemException(
                $"Invoice {invoice.InvoiceNumber} is not linked to a purchase order, so it cannot be matched.");
        }

        return (invoice, po);
    }

    private static string AuditDetail(string invoiceNumber, string outcome) => $"Invoice {invoiceNumber}: {outcome}";

    private static ConflictProblemException Duplicate(string existingNumber, string supplierName) =>
        new($"Invoice {existingNumber} from {supplierName} is already recorded");

    private sealed record ValidInvoice(
        Guid Id, Guid SupplierId, string InvoiceNumber, string NormalizedNumber, DateOnly InvoiceDate, string Currency,
        string PoNumber, decimal TaxAmount, IReadOnlyList<NewInvoiceLine> Lines);

    private static ValidInvoice Validate(CreateInvoiceRequest request)
    {
        if (request.SupplierId is not { } supplierId || supplierId == Guid.Empty)
        {
            throw new ValidationProblemException("supplierId is required.");
        }

        var invoiceNumber = Required(request.InvoiceNumber, "invoiceNumber", 40);
        var normalized = InvoiceNumberNormalizer.Normalize(invoiceNumber);
        if (normalized.Length == 0)
        {
            throw new ValidationProblemException("Invoice number must contain digits or letters after the prefix");
        }

        if (request.InvoiceDate is not { } invoiceDate)
        {
            throw new ValidationProblemException("invoiceDate is required (yyyy-MM-dd).");
        }

        var currency = Currency(request.Currency);
        var poNumber = Required(request.PoNumber, "poNumber", 20);

        var taxAmount = request.TaxAmount ?? 0m;
        if (taxAmount < 0)
        {
            throw new ValidationProblemException("taxAmount must be 0 or more.");
        }

        if (taxAmount > Limits.MaxAmount || Limits.HasMoreDecimals(taxAmount, 2))
        {
            throw new ValidationProblemException($"taxAmount must have at most 2 decimal places and be at most {Limits.MaxAmountText}.");
        }

        if (request.Lines is not { Count: > 0 } lines)
        {
            throw new ValidationProblemException("An invoice needs at least one line.");
        }

        var newLines = lines.Select((line, index) => ValidateLine(line, index + 1)).ToList();
        Limits.Total(newLines.Select(l => l.Quantity * l.UnitPrice).Append(taxAmount), "invoice");

        return new ValidInvoice(
            Guid.NewGuid(), supplierId, invoiceNumber, normalized, invoiceDate, currency, poNumber, taxAmount, newLines);
    }

    private static NewInvoiceLine ValidateLine(CreateInvoiceLineRequest? line, int lineNo)
    {
        var prefix = $"Line {lineNo}";
        if (line is null)
        {
            throw new ValidationProblemException($"{prefix} is empty.");
        }

        string Field(string? value, string name, int maxLength)
        {
            try
            {
                return Required(value, name, maxLength);
            }
            catch (ValidationProblemException e)
            {
                throw new ValidationProblemException($"{prefix}: {e.Message}");
            }
        }

        var itemCode = Field(line.ItemCode, "itemCode", 40).ToUpperInvariant();
        var uom = Field(line.Uom, "uom", 10).ToUpperInvariant();
        var description = Field(line.Description, "description", 200);
        var quantity = Limits.Quantity(line.Quantity, prefix);
        var unitPrice = Limits.UnitPrice(line.UnitPrice, prefix);
        Limits.LineAmount(quantity, unitPrice, prefix);

        return new NewInvoiceLine(lineNo, line.PoLineNo, itemCode, uom, description, quantity, unitPrice);
    }
}
