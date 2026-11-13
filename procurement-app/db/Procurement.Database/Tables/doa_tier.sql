CREATE TABLE dbo.doa_tier
(
    id            int           NOT NULL CONSTRAINT pk_doa_tier PRIMARY KEY,
    max_amount    decimal(18,2) NOT NULL CONSTRAINT uq_doa_tier_max_amount UNIQUE,
    approver_role nvarchar(40)  NOT NULL
);
