using System.Globalization;
using Procurement.Api.Data;
using Procurement.Api.Features.Invoices;
using static Procurement.Api.Features.Validation;

namespace Procurement.Api.Features.Receipts;

public sealed class ReceiptService(Db db, AuditLog audit, InvoiceService invoices)
{
    /// <summary>
    /// Records a goods receipt and, in the same transaction, re-matches every Blocked invoice on the PO.
    /// The PO row and its lines are update-locked before received quantities are summed, so two
    /// receipts on one PO cannot both pass the over-receipt check.
    /// </summary>
    public async Task<ReceiptDto> CreateAsync(Guid poId, CreateReceiptRequest request, CancellationToken cancellationToken)
    {
        var receipt = Validate(poId, request);

        return await db.InTransactionAsync(async (connection, transaction) =>
        {
            var po = await ReceiptStore.LockPoAsync(connection, transaction, poId)
                ?? throw new NotFoundProblemException($"Purchase order {poId} was not found.");

            if (po.Status != "Approved")
            {
                throw new RuleViolationProblemException(
                    $"PO {po.Number} is {po.Status}; goods can only be received against an Approved PO");
            }

            var ordered = await ReceiptStore.LockOrderedByLineAsync(connection, transaction, poId);
            var received = await InvoiceStore.ReceivedByLineAsync(connection, transaction, poId);

            foreach (var line in receipt.Lines)
            {
                if (!ordered.TryGetValue(line.PoLineNo, out var orderedQuantity))
                {
                    throw new ValidationProblemException($"PO {po.Number} has no line {line.PoLineNo}.");
                }

                var total = received.GetValueOrDefault(line.PoLineNo) + line.Quantity;
                if (total > orderedQuantity)
                {
                    throw new RuleViolationProblemException(
                        $"Line {line.PoLineNo}: receiving {Qty(line.Quantity)} would bring the total to {Qty(total)}, " +
                        $"more than the {Qty(orderedQuantity)} ordered");
                }
            }

            var inserted = await ReceiptStore.InsertAsync(connection, transaction, receipt);
            await audit.WriteAsync(connection, transaction, "GoodsReceived", "GoodsReceipt", receipt.Id, null, null,
                $"GR {inserted.GrNumber} on PO {po.Number}: " +
                string.Join(", ", receipt.Lines.Select(l => $"line {l.PoLineNo} × {Qty(l.Quantity)}")));

            var rematched = new List<RematchedInvoiceDto>();
            foreach (var invoiceId in await InvoiceStore.BlockedIdsForPoAsync(connection, transaction, poId))
            {
                var outcome = await invoices.EvaluateAsync(connection, transaction, invoiceId);
                rematched.Add(new RematchedInvoiceDto(
                    outcome.Id, outcome.InvoiceNumber, outcome.FromStatus, outcome.Status, outcome.BlockReason));
            }

            return new ReceiptDto(
                receipt.Id, inserted.GrNumber, poId, po.Number, inserted.ReceivedAt, receipt.ReceivedBy, receipt.PackingSlip,
                receipt.Lines.Select(l => new ReceiptLineDto(l.PoLineNo, l.Quantity)).ToList(),
                rematched);
        }, cancellationToken);
    }

    private static string Qty(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static NewReceipt Validate(Guid poId, CreateReceiptRequest request)
    {
        var receivedBy = Required(request.ReceivedBy, "receivedBy", 100);
        var packingSlip = string.IsNullOrWhiteSpace(request.PackingSlip) ? null : Required(request.PackingSlip, "packingSlip", 40);

        if (request.Lines is not { Count: > 0 } lines)
        {
            throw new ValidationProblemException("A goods receipt needs at least one line.");
        }

        var newLines = new List<NewReceiptLine>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i] ?? throw new ValidationProblemException($"Receipt line {i + 1} is empty.");
            if (line.PoLineNo is not { } poLineNo)
            {
                throw new ValidationProblemException($"Receipt line {i + 1}: poLineNo is required.");
            }

            if (newLines.Any(l => l.PoLineNo == poLineNo))
            {
                throw new ValidationProblemException($"Line {poLineNo} appears more than once in the receipt.");
            }

            newLines.Add(new NewReceiptLine(poLineNo, Limits.Quantity(line.Quantity, $"Line {poLineNo}")));
        }

        return new NewReceipt(Guid.NewGuid(), poId, receivedBy, packingSlip, newLines);
    }
}
