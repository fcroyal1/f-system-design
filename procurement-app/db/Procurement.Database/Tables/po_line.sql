CREATE TABLE dbo.po_line
(
    po_id            uniqueidentifier NOT NULL CONSTRAINT fk_po_line_purchase_order REFERENCES dbo.purchase_order (id),
    line_no          int              NOT NULL,
    item_code        varchar(40)      NOT NULL,
    description      nvarchar(200)    NOT NULL,
    uom              varchar(10)      NOT NULL,
    quantity         decimal(18,3)    NOT NULL CONSTRAINT ck_po_line_quantity CHECK (quantity > 0),
    unit_price       decimal(18,4)    NOT NULL CONSTRAINT ck_po_line_unit_price CHECK (unit_price >= 0),
    gl_account       varchar(10)      NOT NULL CONSTRAINT fk_po_line_gl_account REFERENCES dbo.gl_account (number),
    receipt_required bit              NOT NULL CONSTRAINT df_po_line_receipt_required DEFAULT (1),
    CONSTRAINT pk_po_line PRIMARY KEY (po_id, line_no)
);
