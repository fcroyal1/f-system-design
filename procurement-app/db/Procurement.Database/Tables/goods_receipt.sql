CREATE TABLE dbo.goods_receipt
(
    id           uniqueidentifier NOT NULL CONSTRAINT pk_goods_receipt PRIMARY KEY,
    gr_number    varchar(10)      NOT NULL CONSTRAINT uq_goods_receipt_gr_number UNIQUE,
    po_id        uniqueidentifier NOT NULL CONSTRAINT fk_goods_receipt_purchase_order REFERENCES dbo.purchase_order (id),
    received_at  datetime2        NOT NULL,
    received_by  nvarchar(100)    NOT NULL,
    packing_slip nvarchar(40)     NULL
);
GO

CREATE INDEX ix_goods_receipt_po_id ON dbo.goods_receipt (po_id);
