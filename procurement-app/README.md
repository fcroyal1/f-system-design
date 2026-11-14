# Procurement

A small purchase-to-pay app: suppliers, purchase orders, goods receipts and supplier invoices with a
3-way match (PO ↔ receipt ↔ invoice). An invoice that fails a check is **Blocked** with a readable
reason; recording the missing receipt re-matches it automatically.

```
browser ──► BFF + Angular (:5300) ──► API (internal :8080) ──► SQL Server 2025 (host :14330)
                                                                  ▲
                                         db-deploy (one-off) ─────┘  publishes the dacpac
```

## Prerequisites

- Docker Desktop (or Docker Engine) with Docker Compose v2+.
- For running the tests outside Docker: .NET SDK 10 and Node.js 22 with npm.

## Run it

```bash
cd procurement-app
cp .env.example .env          # holds SA_PASSWORD; .env is git-ignored
# edit .env and set your own SA_PASSWORD now, before the first `up` (see below)
docker compose up --build
```

**Choose `SA_PASSWORD` before the first start.** SQL Server only applies it when the database volume is
first created. It must be at least 8 characters and use 3 of the 4 classes: upper case, lower case, digits,
symbols. Do not use `;`, `"` or `$`: they break the connection strings and compose variable interpolation.
To change it later you must run `docker compose down -v`, which **deletes the database** (it is re-seeded on
the next `up`).

Open <http://localhost:5300> once the `bff` container is healthy.

Start-up order: `sqlserver` becomes healthy → `db-deploy` builds the dacpac and publishes it with
sqlpackage (schema plus demo seed), then exits → `api` starts → `bff` starts. Both host ports are bound to
`127.0.0.1` only, so nothing is reachable from your network. SQL Server is published on
`localhost:14330` (user `sa`, password from `.env`) if you want to look at the data; the API has no host port.

Stop with `Ctrl+C`, then `docker compose down` (add `-v` to delete the database volume and start fresh).

**Apple Silicon:** the SQL Server image is amd64-only, so it runs under Rosetta emulation
(`platform: linux/amd64` in compose). It works, but the first start is slow: allow a minute or two
before the healthcheck passes. Make sure "Use Rosetta for x86_64/amd64 emulation" is on in Docker Desktop.

## Try the guide example

The seed contains the worked example: Bluepeak Medical Supply's invoice **INV-77812** against
**PO 4500123** bills 100 boxes of nitrile gloves, but only 98 have been received.

1. The dashboard shows 1 open exception: INV-77812, *Blocked*, reason
   `Quantity: billed 2 more than received (line 1)`. Click it to see every match check.
2. Open PO **4500123** (Purchase orders). Under *Receive goods*, enter your name in *Received by*,
   `2` for line 1, and press *Record receipt*.
3. The receipt re-matches every Blocked invoice on the PO: INV-77812 is now **Matched** and the
   dashboard shows 0 open exceptions.

`tools/smoke.sh` automates exactly this walkthrough against a fresh stack (see below; it resets the database).

## Tests

```bash
cd procurement-app
dotnet test Procurement.sln                       # Docker must be running
cd web && npm ci && npx ng test --watch=false     # Angular unit tests (Vitest)
```

The database and API tests start their own SQL Server with Testcontainers and publish the dacpac into it;
without Docker they are skipped with a clear message.

End-to-end smoke test (builds the images, runs the guide example through the BFF; needs `.env`, `curl`,
and `jq` or `python3`). **It deletes your local database:** it runs `docker compose down -v` before starting
(so it always begins from the seed) and again when it finishes, removing the `procurement-sqldata` volume.

```bash
bash tools/smoke.sh                               # prints SMOKE PASS
```

## Layer map

| Layer  | Folder                       | What lives there |
|--------|------------------------------|------------------|
| schema | `db/Procurement.Database`    | SQL project (dacpac): tables, sequences, seed scripts. The only place the schema changes. |
| domain | `src/Procurement.Domain`     | Pure C#: match engine and tolerances, approval routing (DoA), invoice-number normalising, KPIs. |
| api    | `src/Procurement.Api`        | Minimal API + Dapper. Validation, transactions, audit trail, ProblemDetails errors. |
| bff    | `src/Procurement.Bff`        | Serves the Angular app, forwards `/api/*` (YARP), composes `/bff/*` screen views. No business rules, no SQL. |
| ui     | `web`                        | Angular 22 app with NgRx Signals stores. |
| infra  | `docker`, `docker-compose.yml`, `tools` | Dockerfiles, compose stack, smoke test. |
| tests  | `tests`                      | xUnit tests per layer; `Procurement.TestSupport` holds the SQL Server fixture. |

## Configuration

| Setting | Used by | Set in compose to |
|---------|---------|-------------------|
| `SA_PASSWORD` | sqlserver, db-deploy, api | from `.env` |
| `ConnectionStrings__Procurement` | api | `Server=sqlserver,1433;Database=procurement;…` |
| `Actor__Name` | api (name written to the audit trail) | `Demo User` |
| `Api__BaseUrl` | bff | `http://api:8080` |
