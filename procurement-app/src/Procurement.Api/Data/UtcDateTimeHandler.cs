using System.Data;
using Dapper;

namespace Procurement.Api.Data;

/// <summary>
/// Every <c>datetime2</c> column is written as UTC (<c>SYSUTCDATETIME()</c> or UTC seed values), but
/// SqlClient reads it back as <see cref="DateTimeKind.Unspecified"/>, which serializes without a
/// <c>Z</c>. This marks every DateTime Dapper reads as UTC. Date-only columns are unaffected, since
/// they are converted with <see cref="DateOnly.FromDateTime"/>.
/// </summary>
public sealed class UtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public static void Register() => SqlMapper.AddTypeHandler(new UtcDateTimeHandler());

    public override DateTime Parse(object value) => DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.DbType = DbType.DateTime2;
        parameter.Value = value;
    }
}
