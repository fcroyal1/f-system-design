/*
Post-deployment seed. Runs on every publish (docker compose db-deploy and the test fixture).

Insert-only: every statement is MERGE ... WHEN NOT MATCHED THEN INSERT with fixed GUIDs, so a
re-run adds rows that are missing and never resets rows a user has changed (for example
INV-77812 re-matched to Matched after the last 2 boxes were received).
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

:r .\Seed.ReferenceData.sql
:r .\Seed.DemoData.sql

COMMIT TRANSACTION;
