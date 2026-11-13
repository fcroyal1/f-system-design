CREATE TABLE dbo.invoice_line
(
    invoice_id  uniqueidentifier NOT NULL CONSTRAINT fk_invoice_line_invoice REFERENCES dbo.invoice (id),
    line_no     int              NOT NULL,
    po_line_no  int              NULL,
    item_code   varchar(40)      NOT NULL,
    uom         varchar(10)      NOT NULL,
    description nvarchar(200)    NOT NULL,
    quantity    decimal(18,3)    NOT NULL,
    unit_price  decimal(18,4)    NOT NULL,
    CONSTRAINT pk_invoice_line PRIMARY KEY (invoice_id, line_no)
);
