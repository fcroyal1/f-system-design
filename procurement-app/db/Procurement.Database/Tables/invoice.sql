CREATE TABLE dbo.invoice
(
    id                 uniqueidentifier NOT NULL CONSTRAINT pk_invoice PRIMARY KEY,
    supplier_id        uniqueidentifier NOT NULL CONSTRAINT fk_invoice_supplier REFERENCES dbo.supplier (id),
    invoice_number     nvarchar(40)     NOT NULL,
    normalized_number  nvarchar(40)     NOT NULL,
    invoice_date       date             NOT NULL,
    due_date           date             NOT NULL,
    currency           char(3)          NOT NULL,
    po_id              uniqueidentifier NULL CONSTRAINT fk_invoice_purchase_order REFERENCES dbo.purchase_order (id),
    tax_amount         decimal(18,2)    NOT NULL CONSTRAINT df_invoice_tax_amount DEFAULT (0),
    status             varchar(16)      NOT NULL CONSTRAINT ck_invoice_status CHECK (status IN ('Matched', 'Blocked')),
    block_reason       nvarchar(400)    NULL,
    first_match_passed bit              NULL,
    is_retro_po        bit              NOT NULL CONSTRAINT df_invoice_is_retro_po DEFAULT (0),
    created_at         datetime2        NOT NULL CONSTRAINT df_invoice_created_at DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT uq_invoice_supplier_normalized_number UNIQUE (supplier_id, normalized_number)
);
GO

CREATE INDEX ix_invoice_po_id ON dbo.invoice (po_id);
