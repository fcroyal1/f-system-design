using Microsoft.Data.SqlClient;
using Procurement.TestSupport;

namespace Procurement.Database.Tests;

public sealed class SeedTests(SqlServerFixture sql)
{
    [Fact]
    public async Task Seed_GuideExample_Present()
    {
        var connectionString = await sql.CreateDatabaseAsync();
        await using var connection = await Db.OpenAsync(connectionString);

        var po = Assert.Single(await Db.RowsAsync(connection, """
            SELECT po.status, s.code, po.approver_role
            FROM dbo.purchase_order po JOIN dbo.supplier s ON s.id = po.supplier_id
            WHERE po.po_number = '4500123';
            """));
        Assert.Equal("Approved", po[0]);
        Assert.Equal("V-10442", po[1]);
        Assert.Equal("Cost center owner", po[2]);

        var poLine = Assert.Single(await Db.RowsAsync(connection, """
            SELECT l.line_no, l.item_code, l.uom, l.quantity, l.unit_price, l.gl_account, l.receipt_required
            FROM dbo.po_line l JOIN dbo.purchase_order po ON po.id = l.po_id
            WHERE po.po_number = '4500123';
            """));
        Assert.Equal(1, poLine[0]);
        Assert.Equal("GLV-NIT-M", poLine[1]);
        Assert.Equal("BX", poLine[2]);
        Assert.Equal(100m, poLine[3]);
        Assert.Equal(10.0000m, poLine[4]);
        Assert.Equal("6110", poLine[5]);
        Assert.Equal(true, poLine[6]);

        var grLine = Assert.Single(await Db.RowsAsync(connection, """
            SELECT gl.po_line_no, gl.quantity_received
            FROM dbo.gr_line gl
            JOIN dbo.goods_receipt gr ON gr.id = gl.gr_id
            JOIN dbo.purchase_order po ON po.id = gr.po_id
            WHERE gr.gr_number = '5000987' AND po.po_number = '4500123';
            """));
        Assert.Equal(1, grLine[0]);
        Assert.Equal(98m, grLine[1]);

        var invoice = Assert.Single(await Db.RowsAsync(connection, """
            SELECT i.status, i.block_reason, i.first_match_passed, i.is_retro_po, i.normalized_number, po.po_number
            FROM dbo.invoice i JOIN dbo.purchase_order po ON po.id = i.po_id
            WHERE i.invoice_number = N'INV-77812';
            """));
        Assert.Equal("Blocked", invoice[0]);
        Assert.Equal("Quantity: billed 2 more than received (line 1)", invoice[1]);
        Assert.Equal(false, invoice[2]);
        Assert.Equal(false, invoice[3]);
        Assert.Equal("77812", invoice[4]);
        Assert.Equal("4500123", invoice[5]);

        var checks = await Db.RowsAsync(connection, """
            SELECT c.seq, c.name, c.outcome, c.po_value, c.receipt_value, c.invoice_value, c.message
            FROM dbo.match_check c JOIN dbo.invoice i ON i.id = c.invoice_id
            WHERE i.invoice_number = N'INV-77812'
            ORDER BY c.seq;
            """);
        Assert.Equal(
            new[] { "Supplier", "Currency", "PO status", "PO line", "Quantity", "Unit price", "Amount" },
            checks.Select(c => (string)c[1]));
        Assert.Equal(
            new[] { "Pass", "Pass", "Pass", "Pass", "Fail", "Pass", "Fail" },
            checks.Select(c => (string)c[2]));

        // Non-ASCII text (em dash, ±) survives the dacpac round trip.
        Assert.Equal(new object[] { "—", "—", "—" }, checks[0][3..6]);
        Assert.Equal("+2.0%, inside the ±3% / $50 price tolerance", checks[5][6]);
        Assert.Equal(new object[] { "1000.00", "980.00", "1020.00", "$40.00 more than the value received" }, checks[6][3..7]);

        var requester = await Db.ScalarAsync<string>(connection,
            "SELECT requester_name FROM dbo.purchase_order WHERE po_number = '4500123';");
        Assert.Equal("Harborview Clinics – Supply room", requester);
    }

    [Fact]
    public async Task Seed_PoExemptCategories()
    {
        var connectionString = await sql.CreateDatabaseAsync();
        await using var connection = await Db.OpenAsync(connectionString);

        var exempt = (await Db.RowsAsync(connection,
                "SELECT code FROM dbo.spend_category WHERE is_po_exempt = 1 ORDER BY code;"))
            .Select(r => (string)r[0]);

        Assert.Equal(new[] { "INSURANCE", "LEGAL", "REFUNDS", "RENT", "TAXES", "UTILITIES" }, exempt);
    }

    [Fact]
    public async Task Seed_RerunAfterChange_KeepsChangedRows()
    {
        var connectionString = await sql.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        await using (var connection = await Db.OpenAsync(connectionString))
        {
            var updated = await Db.ExecuteAsync(connection, """
                UPDATE dbo.invoice SET status = 'Matched', block_reason = NULL
                WHERE invoice_number = N'INV-77812';
                """);
            Assert.Equal(1, updated);

            // Removing a seed row proves the redeploy really re-runs the post-deployment seed.
            Assert.Equal(1, await Db.ExecuteAsync(connection, "DELETE FROM dbo.doa_tier WHERE approver_role = N'CFO';"));
        }

        await sql.RedeployAsync(databaseName);

        await using (var connection = await Db.OpenAsync(connectionString))
        {
            var invoice = Assert.Single(await Db.RowsAsync(connection,
                "SELECT status, block_reason FROM dbo.invoice WHERE invoice_number = N'INV-77812';"));
            Assert.Equal("Matched", invoice[0]);
            Assert.Equal(DBNull.Value, invoice[1]);

            Assert.Equal(1, await Db.ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.doa_tier WHERE approver_role = N'CFO';"));
            Assert.Equal(2, await Db.ScalarAsync<int>(connection, "SELECT COUNT(*) FROM dbo.invoice;"));
        }
    }
}
