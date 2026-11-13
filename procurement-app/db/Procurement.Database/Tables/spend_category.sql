CREATE TABLE dbo.spend_category
(
    code         varchar(20)   NOT NULL CONSTRAINT pk_spend_category PRIMARY KEY,
    name         nvarchar(100) NOT NULL,
    is_po_exempt bit           NOT NULL CONSTRAINT df_spend_category_is_po_exempt DEFAULT (0)
);
