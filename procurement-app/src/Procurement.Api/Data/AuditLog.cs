using Dapper;
using Microsoft.Data.SqlClient;

namespace Procurement.Api.Data;

/// <summary>Writes <c>audit_event</c> rows inside the caller's transaction.</summary>
public sealed class AuditLog(Actor actor)
{
    public Task WriteAsync(SqlConnection connection, SqlTransaction transaction, string action, string entityType,
        Guid entityId, string? from, string? to, string? detail) =>
        connection.ExecuteAsync(
            """
            INSERT INTO dbo.audit_event (actor, action, entity_type, entity_id, from_status, to_status, detail)
            VALUES (@actor, @action, @entityType, @entityId, @from, @to, @detail);
            """,
            new { actor = actor.Name, action, entityType, entityId, from, to, detail },
            transaction);
}
