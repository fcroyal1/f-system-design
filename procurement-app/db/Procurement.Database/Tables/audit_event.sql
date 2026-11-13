CREATE TABLE dbo.audit_event
(
    id          bigint           NOT NULL IDENTITY(1, 1) CONSTRAINT pk_audit_event PRIMARY KEY,
    occurred_at datetime2        NOT NULL CONSTRAINT df_audit_event_occurred_at DEFAULT (SYSUTCDATETIME()),
    actor       nvarchar(100)    NOT NULL,
    action      varchar(40)      NOT NULL,
    entity_type varchar(20)      NOT NULL,
    entity_id   uniqueidentifier NOT NULL,
    from_status varchar(16)      NULL,
    to_status   varchar(16)      NULL,
    detail      nvarchar(400)    NULL
);
GO

CREATE INDEX ix_audit_event_entity ON dbo.audit_event (entity_type, entity_id);
