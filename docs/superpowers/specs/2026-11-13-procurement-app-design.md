# Design: Procurement & AP Workbench (Angular + BFF + .NET API + SQL Server)

**Date:** 2026-11-13 · **Size:** project (full design) · **Status:** approved 2026-11-13
**Built with:** the `stacked-superpowers` skill (Full mode), shipped as two gh-stacks, one PR per architectural layer.

## Intent and why

A **brand-new** procurement and accounts-payable workbench in `f-system-design/procurement-app/`. Someone can raise a purchase order, receive goods, enter the supplier's invoice and watch the automatic 3-way match pass or block, with the same numbers and rules as the guide `procurement-overview.html`.

The architecture is the point: an Angular single-page app holding state in NgRx Signals, a .NET BFF that is the browser's only backend, a .NET Minimal API that owns the rules and the data, and SQL Server whose schema is owned by a dacpac. All of it runs with one `docker compose up`.

A Blazor version of this app already exists in another repo. **Nothing is copied from it**: no code, no SQL, no tests. The behaviour it must reproduce comes from the guide (domain source of truth) and from the rules written down in this document.

## Goals

The work ships as two stacks. Stack 1 is a usable app on its own.

### Stack 1: core procure-to-pay

1. **Suppliers:** list, view, create. Fields: code (e.g. `V-10442`), legal name, spend category, payment terms (days), tax ID and bank account shown masked (last 4 only), blocked flag.
2. **Purchase orders:** create as Draft (supplier, requester, cost center, currency, lines: item code, description, unit of measure, quantity, unit price, GL account, receipt-required flag); approve, which records the approver tier chosen by total (DoA, below); view with their receipts and invoices.
3. **Goods receipts** against PO lines; receiving more than ordered (across all receipts) is rejected with a readable message.
4. **PO-backed invoices:** enter header and lines; duplicate check; retro-PO flag; 2-way or 3-way match per line; status Matched or Blocked with a reason.
5. **Re-match** a blocked invoice (e.g. after more goods arrive).
6. **Match detail page:** the guide's check table: Check / Purchase order / Goods receipt / Invoice / Result.
7. **Dashboard KPIs:** PO compliance by spend and by count (exempt categories excluded), first-pass match rate, retro-PO rate, open exceptions.
8. **Seed data** reproducing the guide's example: buyer Harborview Clinics, supplier Bluepeak Medical Supply `V-10442`, PO `4500123` (line 1: `GLV-NIT-M`, 100 BX at 10.00, GL 6110), GR `5000987` receiving 98, invoice `INV-77812` billing 100 at 10.20, which **blocks on quantity**.

### Stack 2: the rest (designed now, planned after Stack 1 merges)

9. Non-PO invoices: GL account and cost center on every line, routed to an approver by amount.
10. Blocked-invoice resolution: override with a reason code; short-pay with a credit-memo request.
11. Channel advisor: the guide's PO / non-PO / P-card decision tree as a form.
12. Glossary (searchable) and meeting-questions checklist (ticks and notes saved).
13. The guide served at `/guide`.
14. Live updates: a change in one tab refreshes other tabs showing the same PO, invoice, list or dashboard.

## Business rules (binding)

- **Match tolerances:** quantity 0 %; price ±3 % **and** at most $50.00 per line (both must hold). Undercharges always pass. Exactly +3.00 % and exactly $50.00 pass; $50.01 fails.
- **Quantity available to invoice** = (receipt-required ? received : ordered) − previously invoiced, per PO line. Several invoices against one line count cumulatively.
- **Check order:** header checks first (supplier matches PO, currency matches, PO is Approved), then per line (PO line exists with the same item and unit, quantity, unit price, amount). Amount fails exactly when that line's quantity or price fails. Block reason = first failed check: `"<Check>: <message>"`.
- **Guide example result:** `Quantity: billed 2 more than received (line 1)`; unit price passes (+2.0 %); amount fails with `$40.00 more than the value received`.
- **Duplicate invoice:** same supplier + normalized number. Normalizing uppercases, strips non-alphanumerics and a leading alphabetic prefix, then leading zeros (`INV-001`, `inv001`, `1` are the same). The same number from another supplier is not a duplicate. Enforced by a unique index as well as in code.
- **Retro-PO:** an invoice whose date is before its PO's date.
- **DoA tiers:** ≤ 5,000 Cost center owner; ≤ 25,000 Director; ≤ 100,000 VP Finance; above, CFO.
- **Bad references** (unknown PO number, unapproved PO, blocked supplier, currency mismatch) produce a **Blocked invoice with a readable reason**, never an error page.
- **KPIs:** addressable = invoices not in a PO-exempt category. By spend = Σ total with PO ÷ Σ total; by count likewise; first-pass = invoices whose first match passed ÷ PO-backed invoices; retro = retro-PO ÷ PO-backed. Percentages to 1 decimal place (midpoint away from zero), 0 when the denominator is 0.
- **Money** `decimal(18,2)`, quantities `decimal(18,3)`, currency `char(3)`. Currency is compared, never converted.
- **Audit:** every money-moving status change (approve PO, block/match invoice, and in Stack 2 override, short-pay, reject) writes an audit row with actor, action, entity, before/after status and time.

## Architecture

```
Browser ──HTTP──▶ Procurement.Bff (ASP.NET Core, :5300)
                    ├─ serves the Angular build (static files, SPA fallback)
                    ├─ /bff/*  screen endpoints (aggregation, e.g. dashboard, PO detail)
                    └─ /api/*  forwarded to the API with YARP (+ /hubs/* WebSockets in Stack 2)
                         │
                         ▼
                  Procurement.Api (ASP.NET Core Minimal API, internal :8080, not published to the host)
                    ├─ endpoint groups: suppliers, purchase-orders, receipts, invoices, kpis, reference
                    ├─ Procurement.Domain (pure rules: match, tolerance, normalizer, DoA, retro-PO, KPIs)
                    └─ Dapper + Microsoft.Data.SqlClient, hand-written SQL
                         │
                         ▼
                  SQL Server 2025 (mcr.microsoft.com/mssql/server) ◀── db-deploy (one-off: sqlpackage publish of Procurement.Database.dacpac)
```

| Project | Responsibility |
|---|---|
| `db/Procurement.Database` | SDK-style SQL project (`Microsoft.Build.Sql`). Tables, constraints, indexes, sequences for document numbers; post-deployment script for reference and demo seed data (idempotent `MERGE`). `dotnet build` produces the dacpac. **The only description of the schema.** |
| `src/Procurement.Domain` | Pure C# rules, no I/O. Tested without a database. |
| `src/Procurement.Api` | Minimal API endpoints, application services that load data, call the domain and write (one transaction per command, audit row inside it), Dapper repositories. Returns `ProblemDetails` for validation errors. |
| `src/Procurement.Bff` | The browser's only backend: serves the SPA, forwards `/api/*` with YARP, owns `/bff/*` view endpoints that combine several API calls into one screen payload. Holds no business rules and no connection string. |
| `web/` | Angular standalone app, NgRx Signals (`signalStore`) per feature (suppliers, purchase orders, invoices, dashboard), calls only the BFF. |
| `docker-compose.yml` | `sqlserver` → `db-deploy` (waits for healthy, exits 0) → `api` → `bff` (published on 5300). |

**Request flow example (enter invoice):** UI store → `POST /api/invoices` → BFF forwards → API loads PO, receipts, prior invoice lines and supplier, runs the domain match, saves the invoice + lines + audit row in one transaction, returns the invoice with its match result → store updates → match detail page renders the checks from the response.

## Non-goals

Authentication and users (a configured demo actor name is recorded); real payments; FX conversion; OCR; ERP integration; P-card feeds; scaling the BFF/API horizontally; replaying missed live updates (Stack 2); server-side rendering.

## Anti-goals (failure even if it works)

- A match result that disagrees with the guide's worked example.
- The same invoice saved twice for one supplier and normalized number.
- Schema created or changed by EF, by app startup, or by anything other than the dacpac.
- The browser calling the API directly, or the BFF containing business rules or SQL.
- Code, SQL or tests copied from the existing Blazor app.
- A layer PR that does not build and pass its tests on its own.

## Constraints

- **Location:** everything under `f-system-design/procurement-app/`.
- **Versions:** .NET 10 (`net10.0`), Angular CLI 22.2.x, `@ngrx/signals` 22.0.x, `Microsoft.Build.Sql` 2.3.0, `microsoft.sqlpackage` 170.5.96 (dotnet tool), Dapper 2.1.89, `Microsoft.Data.SqlClient` 7.1.2, `Yarp.ReverseProxy` 2.3.0, `Testcontainers.MsSql` 4.16.0, SQL Server image `mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04`.
- **Apple Silicon:** the SQL Server image is amd64 only; compose sets `platform: linux/amd64` and runs it under Rosetta. Health check allows a slow start.
- **Secrets:** SA password and connection string come from a git-ignored `.env`; a committed `.env.example` documents them. No real secrets committed.
- **Ports:** BFF `5300` on the host; API and SQL Server internal only, except SQL Server `14330` on the host for local tests and tooling.
- **Tests:** xUnit for Domain (no DB), Api (Testcontainers SQL Server with the dacpac published into it) and Bff (forwarding and aggregation with a fake API); Angular's default unit test runner for stores and components.
- **Git:** shipped via `stacked-superpowers`; no commits to `main`.

## Stack layout

**Stack 1** (topic `procurement`):

| # | Branch | Contents |
|---|---|---|
| 0 | `procurement/00-design` | this spec, the Stack 1 plan, rewritten `PLAN.md` and `ARCHITECTURE.md` |
| 1 | `procurement/01-schema` | `Procurement.Database` + seed post-deploy script + a build check that the dacpac compiles |
| 2 | `procurement/02-domain` | `Procurement.Domain` + tests |
| 3 | `procurement/03-api` | `Procurement.Api` + Testcontainers tests |
| 4 | `procurement/04-bff` | `Procurement.Bff` + tests |
| 5 | `procurement/05-ui` | Angular app + store/component tests |
| 6 | `procurement/06-infra-docs` | `docker-compose.yml`, Dockerfiles, `.env.example`, README, end-to-end smoke test of the guide example through the BFF |

**Stack 2** (topic `procurement-ext`) follows the same tiers for Goals 9–14 after Stack 1 merges.

## Approaches

| | Approach | Trade-off | Decision |
|---|---|---|---|
| **A** | **Dacpac published by a one-off `sqlpackage` container; Dapper in the API** | **One extra container; the schema has a single owner and the app never changes tables** | **Chosen by the user** |
| B | API publishes the dacpac on startup with DacFx | Fewer containers; every start may change schema, a failed deploy takes the API down | Not chosen |
| C | Dacpac for schema, EF Core for data access | Less SQL; EF's model is a second schema description that drifts | Not chosen |

Also decided in conversation: brand-new app, nothing copied; no Liquibase; Microsoft's SQL Server image; .NET for BFF and API; Angular with NgRx Signals; two stacks, Stack 1 first; one PR per architectural layer.

## Settled after the builder check (Claude's calls, grounded in the guide)

1. **Previously invoiced** = quantity on *other* invoices for the same PO line whose status is Matched (Stack 2 adds Approved); Blocked and Rejected invoices don't count, and an invoice never counts against itself when re-matched. **Re-match** overwrites the current status, reason and check rows; `first_match_passed` is set once, on the first match, and never changes (the first-pass KPI needs it). Every match outcome writes an audit row, which is the history.
2. **Re-match triggers:** automatic and manual. The guide says a quantity block "clears by itself" when the rest arrives, so recording a goods receipt re-matches every Blocked invoice on that PO in the same transaction (with audit rows). A Re-match button covers other cases.
3. **The PO line check compares item code and unit of measure** as well as line existence (the guide's first row: "Supplier, PO number, line, item, unit"). Message for a mismatch: `"invoice line {n} is {item}/{uom}, PO line is {item}/{uom}"`.
4. **The $50.00 price cap** = (invoice unit price − PO unit price) × invoice quantity on that line (guide: 0.20 × 100 = $20), in the invoice's currency.
5. **Invoice header:** supplier, invoice number, invoice date, due date (date + supplier terms), currency, PO number (exactly one PO per invoice in Stack 1), tax amount (default 0). **Total = Σ lines + tax.** Tax is stored and shown, not matched. An invoice line with no matching PO line blocks via the PO-line check.
6. **Seed data:** the post-deployment script inserts reference data, the supplier, PO `4500123` (Approved) and GR `5000987` as plain data, and `INV-77812` with status Blocked and the guide's reason. It only inserts rows that are missing; it never resets rows a user has changed. Drift guard: an API test re-matches the seeded invoice through the real code and asserts the identical status, reason and checks. A second seeded supplier and fully matched PO/invoice give the dashboard non-trivial numbers.
7. **KPIs in Stack 1:** an invoice's spend category is its supplier's; PO-exempt categories are seeded data (`spend_category.is_po_exempt`: utilities, rent, taxes, insurance, legal fees, refunds); "with PO" = linked to an existing PO; retro-PO invoices count as PO-backed; Blocked invoices are included; open exceptions = count of Blocked invoices. Compliance reads 100 % until Stack 2 adds non-PO invoices; that is expected.

## Left to the builder

Table and column names beyond those named here; endpoint URL shapes; Angular component structure and styling; how the BFF's view endpoints are shaped per screen; test fixture design.

## Calls Claude made (check these)

- Folder `procurement-app/`; BFF on host port 5300; SQL Server on host 14330.
- BFF uses YARP for pass-through plus hand-written `/bff/*` view endpoints for aggregated screens.
- Stack 1 includes re-match (needed to show the guide example blocking then passing), but override, short-pay and non-PO invoices are Stack 2.
- SQL Server 2025 image, pinned to `2025-CU9-ubuntu-24.04`.
- Seed data lives in the dacpac's post-deployment script, written to be re-runnable.
- Infra (compose, Dockerfiles) is the top layer, so lower layers are tested with Testcontainers rather than compose.
- `PLAN.md` and `ARCHITECTURE.md` at the repo root are rewritten for this architecture in the `00-design` layer.
