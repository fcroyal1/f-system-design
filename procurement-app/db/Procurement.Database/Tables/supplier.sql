CREATE TABLE dbo.supplier
(
    id                 uniqueidentifier NOT NULL CONSTRAINT pk_supplier PRIMARY KEY,
    code               varchar(20)      NOT NULL CONSTRAINT uq_supplier_code UNIQUE,
    legal_name         nvarchar(200)    NOT NULL,
    category_code      varchar(20)      NOT NULL CONSTRAINT fk_supplier_spend_category REFERENCES dbo.spend_category (code),
    payment_terms_days int              NOT NULL CONSTRAINT ck_supplier_payment_terms_days CHECK (payment_terms_days >= 0),
    tax_id_last4       char(4)          NULL,
    bank_last4         char(4)          NULL,
    is_blocked         bit              NOT NULL CONSTRAINT df_supplier_is_blocked DEFAULT (0)
);
