CREATE TABLE dbo.match_check
(
    invoice_id     uniqueidentifier NOT NULL CONSTRAINT fk_match_check_invoice REFERENCES dbo.invoice (id),
    seq            int              NOT NULL,
    name           nvarchar(40)     NOT NULL,
    line_no        int              NULL,
    po_value       nvarchar(60)     NOT NULL,
    receipt_value  nvarchar(60)     NOT NULL,
    invoice_value  nvarchar(60)     NOT NULL,
    outcome        varchar(4)       NOT NULL CONSTRAINT ck_match_check_outcome CHECK (outcome IN ('Pass', 'Fail')),
    message        nvarchar(400)    NOT NULL,
    CONSTRAINT pk_match_check PRIMARY KEY (invoice_id, seq)
);
