CREATE TABLE dbo.cost_center
(
    code       varchar(20)   NOT NULL CONSTRAINT pk_cost_center PRIMARY KEY,
    name       nvarchar(100) NOT NULL,
    owner_name nvarchar(100) NOT NULL
);
