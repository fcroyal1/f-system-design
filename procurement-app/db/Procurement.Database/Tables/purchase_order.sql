CREATE TABLE dbo.purchase_order
(
    id               uniqueidentifier NOT NULL CONSTRAINT pk_purchase_order PRIMARY KEY,
    po_number        varchar(10)      NOT NULL CONSTRAINT uq_purchase_order_po_number UNIQUE,
    supplier_id      uniqueidentifier NOT NULL CONSTRAINT fk_purchase_order_supplier REFERENCES dbo.supplier (id),
    requester_name   nvarchar(100)    NOT NULL,
    cost_center_code varchar(20)      NOT NULL CONSTRAINT fk_purchase_order_cost_center REFERENCES dbo.cost_center (code),
    currency         char(3)          NOT NULL,
    po_date          date             NOT NULL,
    status           varchar(10)      NOT NULL CONSTRAINT ck_purchase_order_status CHECK (status IN ('Draft', 'Approved', 'Closed')),
    approver_role    nvarchar(40)     NULL,
    approved_by      nvarchar(100)    NULL,
    approved_at      datetime2        NULL
);
