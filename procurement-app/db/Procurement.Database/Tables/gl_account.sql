CREATE TABLE dbo.gl_account
(
    number varchar(10)   NOT NULL CONSTRAINT pk_gl_account PRIMARY KEY,
    name   nvarchar(100) NOT NULL,
    type   varchar(20)   NOT NULL
);
