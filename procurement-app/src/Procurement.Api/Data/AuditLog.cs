using Dapper;
using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

/// <summary>Writes <c>audit_event</c> rows inside the caller's transaction.</summary>
public sealed class AuditLog(Actor actor)
{
    /// <summary>Length of <c>audit_event.detail</c> (nvarchar(400)).</summary>
    public const int MaxDetailLength = 400;

    /// <summary>Writes one audit row; a detail longer than the column is cut to fit rather than failing the transaction.</summary>
    public Task WriteAsync(SqlConnection connection, SqlTransaction transaction, string action, string entityType,
        Guid entityId, string? from, string? to, string? detail) =>
        connection.ExecuteAsync(
            """
            INSERT INTO dbo.audit_event (actor, action, entity_type, entity_id, from_status, to_status, detail)
            VALUES (@actor, @action, @entityType, @entityId, @from, @to, @detail);
            """,
            new { actor = actor.Name, action, entityType, entityId, from, to, detail = Bound(detail) },
            transaction);

    private static string? Bound(string? detail) =>
        detail is { Length: > MaxDetailLength } ? detail[..(MaxDetailLength - 1)] + "…" : detail;
}
