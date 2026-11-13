CREATE TABLE dbo.gr_line
(
    gr_id             uniqueidentifier NOT NULL CONSTRAINT fk_gr_line_goods_receipt REFERENCES dbo.goods_receipt (id),
    po_line_no        int              NOT NULL,
    quantity_received decimal(18,3)    NOT NULL CONSTRAINT ck_gr_line_quantity_received CHECK (quantity_received > 0),
    CONSTRAINT pk_gr_line PRIMARY KEY (gr_id, po_line_no)
);
