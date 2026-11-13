using Microsoft.Data.SqlClient;
using Procurement.TestSupport;

namespace Procurement.Database.Tests;

public sealed class SchemaTests(SqlServerFixture sql)
{
    [Fact]
    public void Dacpac_Builds()
    {
        Assert.True(File.Exists(SqlServerFixture.DacpacPath), $"Expected dacpac at {SqlServerFixture.DacpacPath}");
        Assert.Equal("Procurement.Database.dacpac", Path.GetFileName(SqlServerFixture.DacpacPath));
    }

    [Fact]
    public async Task Invoice_DuplicateNormalizedNumber_Rejected()
    {
        var connectionString = await sql.CreateDatabaseAsync();
        await using var connection = await Db.OpenAsync(connectionString);

        var error = await Assert.ThrowsAsync<SqlException>(() => Db.ExecuteAsync(connection, """
            INSERT INTO dbo.invoice (id, supplier_id, invoice_number, normalized_number, invoice_date, due_date,
                                     currency, po_id, tax_amount, status, block_reason, first_match_passed, is_retro_po, created_at)
            SELECT NEWID(), s.id, N'inv-0077812', N'77812', '2026-10-09', '2026-11-08',
                   'USD', NULL, 0, 'Blocked', N'duplicate', 0, 0, SYSUTCDATETIME()
            FROM dbo.supplier s
            WHERE s.code = 'V-10442';
            """));

        Assert.Contains(error.Number, new[] { 2627, 2601 });
    }

    [Fact]
    public async Task Sequences_StartAfterSeed()
    {
        var connectionString = await sql.CreateDatabaseAsync();
        await using var connection = await Db.OpenAsync(connectionString);

        var nextPo = Convert.ToInt64(await Db.ScalarAsync<object>(connection, "SELECT NEXT VALUE FOR dbo.po_number_seq;"));
        var nextGr = Convert.ToInt64(await Db.ScalarAsync<object>(connection, "SELECT NEXT VALUE FOR dbo.gr_number_seq;"));

        Assert.Equal(4500124L, nextPo);
        Assert.Equal(5000988L, nextGr);
    }
}
