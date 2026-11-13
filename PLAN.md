# Procurement & AP Workbench: Stack 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Ship it with the `stacked-superpowers` skill: each task is built on its layer's branch (see **Stack**). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A brand-new procure-to-pay workbench (suppliers, POs, goods receipts, invoices with an automatic 3-way match, match detail, KPIs) as Angular + NgRx Signals → .NET BFF → .NET Minimal API → SQL Server, run with docker compose.

**Architecture:** `Procurement.Database` (dacpac) owns the schema and seed data; a one-off `sqlpackage` container publishes it. `Procurement.Domain` holds pure rules. `Procurement.Api` (Minimal API, Dapper) runs commands in transactions with audit rows. `Procurement.Bff` serves the Angular build, forwards `/api/*` with YARP and builds `/bff/*` screen payloads. Angular calls only the BFF. See [ARCHITECTURE.md](ARCHITECTURE.md).

**Tech Stack:** .NET 10 (`net10.0`), Microsoft.Build.Sql 2.3.0, microsoft.sqlpackage 170.5.96, Microsoft.SqlServer.DacFx 170.5.96 (tests only), Dapper 2.1.89, Microsoft.Data.SqlClient 7.1.2, Yarp.ReverseProxy 2.3.0, xunit.v3 4.0.3, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.12.0, Microsoft.AspNetCore.Mvc.Testing 10.0.15, Testcontainers.MsSql 4.16.0, SQL Server `mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04`, Angular CLI 22.2.x, @ngrx/signals 22.0.x, Vitest (Angular's default runner).

**Spec:** [docs/superpowers/specs/2026-11-13-procurement-app-design.md](docs/superpowers/specs/2026-11-13-procurement-app-design.md). Its **Business rules** and **Settled after the builder check** sections are binding.

## Global Constraints

- All code under `procurement-app/`. Solution `procurement-app/Procurement.sln`. Paths below are relative to `procurement-app/`.
- **Nothing copied** from `csharp-playground/director_AP_Information` (code, SQL, tests). The guide is read for domain facts only.
- Schema changes only in `db/Procurement.Database`. No EF, no schema code in app startup.
- Browser → BFF only. BFF holds no business rules, no SQL, no connection string.
- `Nullable` and `ImplicitUsings` on; file-scoped namespaces; root namespace `Procurement.*`.
- Money `decimal(18,2)`; **unit prices `decimal(18,4)`** (the price-cap boundary needs 4 places); quantities `decimal(18,3)`; currency `char(3)`. C# `decimal` throughout; never round before comparing.
- Tolerances: quantity 0 %, price ±3 % **and** ≤ $50.00 per line, cap = (invoice price − PO price) × invoice qty, in invoice currency.
- DoA: ≤ 5,000 `Cost center owner`; ≤ 25,000 `Director`; ≤ 100,000 `VP Finance`; above `CFO`.
- Every money-moving status change writes `audit_event` in the same transaction. Actor = config `Actor:Name`, default `Demo User`.
- Error contract: validation → 400 `ProblemDetails`; not found → 404; duplicate (invoice, supplier code) → 409; rule violation (over-receipt, approving a non-Draft PO) → 422. `detail` is a readable sentence.
- Ports: BFF host 5300; SQL Server host 14330; API internal only. Secrets only in git-ignored `.env`.
- Tests that need SQL Server use Testcontainers with the dacpac published by `Procurement.TestSupport`; they skip with a clear message when Docker is unavailable.
- Each layer builds and passes its own tests. Stage explicit paths only.

## Review Focus

1. **Two receipts for the same PO line at the same moment.** Expected: the second one that would over-receive gets 422; never both saved. Test: `Receipt_ConcurrentOverReceipt_OnlyOneSucceeds` (Task 5): the receipt transaction locks the PO's lines (`UPDLOCK, HOLDLOCK`) before summing.
2. **Invoice numbers that normalize to empty** (`"INV-"`, `"---"`). Expected: 400 "Invoice number must contain digits or letters after the prefix", not a unique-index crash. Tests: `Normalize_OnlyPrefix_ReturnsEmpty` (Task 3), `Invoice_NumberNormalizesToEmpty_Returns400` (Task 5).
3. **Seed re-run after the user changed demo rows.** Expected: `docker compose up` again keeps their changes (INV-77812 stays Matched after they received the last 2 boxes). Test: `Seed_RerunAfterChange_KeepsChangedRows` (Task 1).
4. **BFF when the API is down.** Expected: `/bff/*` returns 502 `ProblemDetails` "The procurement API is unavailable", and the UI shows it, not a blank page. Tests: `Dashboard_ApiDown_Returns502Problem` (Task 6), `DashboardStore_LoadFails_SetsError` (Task 7).
5. **Deep link refresh in the browser** (`/invoices/<id>` reloaded). Expected: the BFF returns `index.html`, the route loads the invoice; unknown `/api/...` and `/bff/...` paths still 404, not `index.html`. Tests: `SpaFallback_DeepLink_ReturnsIndex`, `UnknownApiPath_Returns404NotIndex` (Task 6).

---

## Stack

Topic `procurement`, trunk `main`. Create each branch with `gh stack add` when its first task starts.

| # | Branch | Layer | Tasks |
|---|---|---|---|
| 0 | `procurement/00-design` | spec + plan + architecture | 0 |
| 1 | `procurement/01-schema` | schema (dacpac, seed, test support) | 1 |
| 2 | `procurement/02-domain` | domain | 2, 3 |
| 3 | `procurement/03-api` | api | 4, 5 |
| 4 | `procurement/04-bff` | bff | 6 |
| 5 | `procurement/05-ui` | ui | 7, 8 |
| 6 | `procurement/06-infra-docs` | infra + docs | 9 |

## File Structure

```
procurement-app/
  Procurement.sln
  Directory.Build.props                 net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors
  db/Procurement.Database/
    Procurement.Database.sqlproj        Sdk="Microsoft.Build.Sql/2.3.0", DSP Sql170 (Sql160 if 170 unsupported: ledger it)
    Tables/*.sql                        one file per table
    Sequences/*.sql                     po_number_seq, gr_number_seq
    PostDeployment/Script.PostDeployment.sql   :r includes of Seed/*.sql
    PostDeployment/Seed/01-reference.sql, 02-suppliers.sql, 03-guide-example.sql, 04-second-example.sql
  src/Procurement.Domain/
    Model.cs                            enums + line records
    Matching/{Tolerance,MatchInput,MatchCheck,MatchResult,MatchEngine}.cs
    Policies/{InvoiceNumberNormalizer,ApprovalRouter,RetroPoRule}.cs
    Kpis/KpiCalculator.cs
  src/Procurement.Api/
    Program.cs                          composition, endpoint group mapping, ProblemDetails
    Data/{Db,SupplierStore,ReferenceStore,PurchaseOrderStore,ReceiptStore,InvoiceStore,AuditLog}.cs
    Features/{Suppliers,Reference,PurchaseOrders,Receipts,Invoices,Kpis}/   *Endpoints.cs + *Service.cs + Dtos.cs
    Errors.cs                           DomainRuleException(422), DuplicateException(409), NotFoundException(404) → ProblemDetails
  src/Procurement.Bff/
    Program.cs                          YARP, /bff endpoints, static files + SPA fallback
    ApiClient.cs                        typed HttpClient to the API
    Views/{DashboardView,PurchaseOrderView,InvoiceView}.cs
  web/                                  Angular app (ng new), src/app/{core,suppliers,purchase-orders,invoices,dashboard,shared}
  tests/
    Procurement.TestSupport/            SqlServerFixture (Testcontainers + DacFx publish), SkipIfNoDocker
    Procurement.Database.Tests/
    Procurement.Domain.Tests/
    Procurement.Api.Tests/
    Procurement.Bff.Tests/
  docker/{api,bff,db-deploy}.Dockerfile
  docker-compose.yml  .env.example  .gitignore  README.md
  tools/smoke.sh
```

---

### Task 0: Design layer

**Files:** Commit `docs/superpowers/specs/2026-11-13-procurement-app-design.md`, `PLAN.md` (this file), `ARCHITECTURE.md` (rewritten: C4 Level 1 context and Level 2 containers for this architecture, request-flow sequence for "enter invoice", the rules the diagram encodes).

- [ ] **Step 1:** `gh stack init procurement/00-design` (from `main`, untracked unrelated files stashed with `git stash push -u` around any `gh stack rebase`).
- [ ] **Step 2: Rewrite `ARCHITECTURE.md`** with Mermaid diagrams matching the spec's Architecture section exactly (container names, ports, arrows: Browser→BFF, BFF→API via YARP and typed client, API→SQL Server, db-deploy→SQL Server).
- [ ] **Step 3: Commit.** `git add PLAN.md ARCHITECTURE.md docs/superpowers/specs/2026-11-13-procurement-app-design.md && git commit -m "docs(procurement): design, Stack 1 plan, architecture"`

### Task 1: Database project, seed data and test support

**Files:**
- Create: `.gitignore` (`.env`, `bin/`, `obj/`, `web/node_modules`, `web/dist`, `web/.angular`), `Procurement.sln`, `Directory.Build.props`, `db/Procurement.Database/**`, `tests/Procurement.TestSupport/{Procurement.TestSupport.csproj,SqlServerFixture.cs,SkipIfNoDocker.cs}`, `tests/Procurement.Database.Tests/{csproj,SchemaTests.cs,SeedTests.cs}`

**Interfaces:**
- Produces tables (snake_case):
  - `spend_category(code varchar(20) PK, name, is_po_exempt bit)`, `gl_account(number varchar(10) PK, name, type)`, `cost_center(code varchar(20) PK, name, owner_name)`, `doa_tier(id int PK, max_amount decimal(18,2), approver_role nvarchar(40))`
  - `supplier(id uniqueidentifier PK, code varchar(20) UNIQUE, legal_name, category_code FK, payment_terms_days int, tax_id_last4 char(4) NULL, bank_last4 char(4) NULL, is_blocked bit)`
  - `purchase_order(id PK, po_number varchar(10) UNIQUE, supplier_id FK, requester_name, cost_center_code FK, currency char(3), po_date date, status varchar(10) CHECK IN ('Draft','Approved','Closed'), approver_role NULL, approved_by NULL, approved_at datetime2 NULL)`; `po_line(po_id, line_no, item_code, description, uom varchar(10), quantity decimal(18,3), unit_price decimal(18,4), gl_account FK, receipt_required bit, PK(po_id,line_no))`
  - `goods_receipt(id PK, gr_number varchar(10) UNIQUE, po_id FK, received_at datetime2, received_by, packing_slip NULL)`; `gr_line(gr_id, po_line_no, quantity_received decimal(18,3), PK(gr_id,po_line_no))`
  - `invoice(id PK, supplier_id FK, invoice_number nvarchar(40), normalized_number nvarchar(40), invoice_date date, due_date date, currency char(3), po_id NULL FK, tax_amount decimal(18,2) DEFAULT 0, status varchar(16) CHECK IN ('Matched','Blocked'), block_reason nvarchar(400) NULL, first_match_passed bit NULL, is_retro_po bit, created_at datetime2)`, **UNIQUE(supplier_id, normalized_number)**; `invoice_line(invoice_id, line_no, po_line_no NULL, item_code, uom, description, quantity decimal(18,3), unit_price decimal(18,4), PK)`; `match_check(invoice_id, seq int, name, line_no NULL, po_value, receipt_value, invoice_value, outcome varchar(4) CHECK IN ('Pass','Fail'), message nvarchar(400), PK(invoice_id,seq))`
  - `audit_event(id bigint IDENTITY PK, occurred_at datetime2, actor, action varchar(40), entity_type varchar(20), entity_id uniqueidentifier, from_status NULL, to_status NULL, detail nvarchar(400) NULL)`
  - Sequences `po_number_seq` START 4500124, `gr_number_seq` START 5000988.
- Produces seed (fixed GUIDs, insert-only `MERGE … WHEN NOT MATCHED THEN INSERT`):
  - categories `MED-SUPPLIES`, `OFFICE` (not exempt); `UTILITIES`, `RENT`, `TAXES`, `INSURANCE`, `LEGAL`, `REFUNDS` (exempt). GL `6110 Medical supplies` (+ `6200 Office supplies`). Cost center `CC-200 Clinic operations`, owner `Dana Ruiz`. DoA tiers as in Global Constraints (top tier `max_amount 999999999999.99`).
  - Supplier `V-10442 Bluepeak Medical Supply` (MED-SUPPLIES, 30 days, tax `4421`, bank `6630`); supplier `V-10518 Northline Office Co` (OFFICE, 45 days).
  - PO `4500123` Bluepeak, requester `Harborview Clinics – Supply room`, CC-200, USD, 2026-10-01, Approved by `Cost center owner`; line 1 `GLV-NIT-M`, "Nitrile exam gloves, medium, box of 100", `BX`, 100 @ 10.0000, GL 6110, receipt required. GR `5000987` 2026-10-06, line 1 received 98.
  - Invoice `INV-77812` (normalized `77812`) dated 2026-10-08, due 2026-11-07, USD, PO 4500123, tax 0, **Blocked**, reason `Quantity: billed 2 more than received (line 1)`, `first_match_passed 0`, not retro; line 1 100 @ 10.2000; `match_check` rows exactly as `MatchEngine` produces for this input (Task 2's check list and messages; Task 5's drift test is the guard).
  - Northline PO `4500118` (Approved, 2026-09-20) line 1 `PPR-A4-500` 40 `RM` @ 6.5000, GL 6200, fully received (GR `5000980`); invoice `NL-2231` 40 @ 6.5000, **Matched**, `first_match_passed 1`, with its passing `match_check` rows.
- Produces `Procurement.TestSupport`:
  - `SqlServerFixture : IAsyncLifetime` (one container per test assembly via `AssemblyFixture`), `Task<string> CreateDatabaseAsync(string? name = null)` publishes `Procurement.Database.dacpac` with DacFx (`DacServices.Deploy`, `CreateNewDatabase = true`) and returns its connection string.
  - The dacpac path is resolved from a `ProjectReference` to the sqlproj with `ReferenceOutputAssembly="false"` and a copied `.dacpac` item.

- [ ] **Step 1:** `gh stack add procurement/01-schema`.
- [ ] **Step 2: Scaffold** `dotnet new sln -n Procurement`, `Directory.Build.props`, the sqlproj, `dotnet new classlib`/`xunit` projects (switch test projects to xunit.v3 4.0.3). Add all to the solution.
- [ ] **Step 3: Write the failing tests** (`Procurement.Database.Tests`):

```csharp
[Fact] Dacpac_Builds()                                // file Procurement.Database.dacpac exists after build
[Fact] Seed_GuideExample_Present()                    // PO 4500123 Approved, 1 line 100 @ 10.0000; GR 5000987 line 1 = 98; INV-77812 Blocked with reason "Quantity: billed 2 more than received (line 1)"
[Fact] Seed_PoExemptCategories()                      // exempt = {INSURANCE, LEGAL, REFUNDS, RENT, TAXES, UTILITIES}
[Fact] Invoice_DuplicateNormalizedNumber_Rejected()   // inserting (Bluepeak, normalized '77812') again → SqlException 2627/2601
[Fact] Seed_RerunAfterChange_KeepsChangedRows()       // UPDATE INV-77812 to Matched, re-run post-deploy (redeploy dacpac) → still Matched
[Fact] Sequences_StartAfterSeed()                     // NEXT VALUE FOR po_number_seq = 4500124
```

- [ ] **Step 4: Run, confirm they fail.** `dotnet test tests/Procurement.Database.Tests`. Expected: compile errors / missing dacpac.
- [ ] **Step 5: Implement** the tables, sequences, seed scripts and `SqlServerFixture`.
- [ ] **Step 6: Run, confirm they pass.** Expected: 6 passed (or 6 skipped with "Docker not available" message, which does not count as passing: Docker must be running).
- [ ] **Step 7: Commit** `procurement-app/.gitignore procurement-app/Procurement.sln procurement-app/Directory.Build.props procurement-app/db procurement-app/tests/Procurement.TestSupport procurement-app/tests/Procurement.Database.Tests` with `feat(procurement): SQL Server schema dacpac and seed data`.

### Task 2: Domain model and match engine

**Files:** Create `src/Procurement.Domain/{Procurement.Domain.csproj,Model.cs,Matching/*.cs}`; test `tests/Procurement.Domain.Tests/MatchEngineTests.cs`.

**Interfaces (namespace `Procurement.Domain` / `.Matching`):**
- `enum PoStatus { Draft, Approved, Closed }`, `enum InvoiceStatus { Matched, Blocked }`, `enum CheckOutcome { Pass, Fail }`
- `record PoLine(int LineNo, string ItemCode, string Uom, decimal Quantity, decimal UnitPrice, bool ReceiptRequired = true)`
- `record InvoiceLine(int LineNo, int? PoLineNo, string ItemCode, string Uom, decimal Quantity, decimal UnitPrice)`
- `record Tolerance(decimal QuantityPct, decimal PricePct, decimal PriceAbsolute) { public static Tolerance Default { get; } = new(0m, 3.0m, 50.00m); }`
- `record MatchInput(bool SupplierMatches, string PoCurrency, string InvoiceCurrency, PoStatus PoStatus, IReadOnlyList<PoLine> PoLines, IReadOnlyDictionary<int, decimal> ReceivedQtyByLine, IReadOnlyDictionary<int, decimal> PreviouslyInvoicedQtyByLine, IReadOnlyList<InvoiceLine> InvoiceLines)`
- `record MatchCheck(string Name, int? LineNo, string PoValue, string ReceiptValue, string InvoiceValue, CheckOutcome Outcome, string Message)`
- `record MatchResult(IReadOnlyList<MatchCheck> Checks) { bool Passed; string? BlockReason; }` — BlockReason = `$"{first failed.Name}: {first failed.Message}"`.
- `static class MatchEngine { static MatchResult Match(MatchInput input, Tolerance tolerance); }`

Check names and messages (exact): `Supplier` ("supplier on invoice is not the PO's supplier"), `Currency` ("invoice in {inv}, PO in {po}"), `PO status` ("PO is {status}, not approved"), `PO line` ("invoice line {n} has no matching PO line" / "invoice line {n} is {item}/{uom}, PO line is {item}/{uom}"), `Quantity` ("billed {over:0.##} more than received (line {n})", "ordered" for receipt-not-required lines), `Unit price` (pass: "+{pct:0.0}%, inside the ±3% / $50 price tolerance"; fail: "+{pct:0.0}% / ${abs:0.00} over the ±3% / $50 price tolerance"), `Amount` (fail: "${invAmount − min(invQty, available) × poPrice:0.00} more than the value received"; pass: "matches the value received"). Header checks always emitted, in order; line checks per invoice line in order. Values shown without trailing zeros (`100`, `98`, `10.00`, `10.20`); `—` where not applicable.

**Canonical check rows** (binding for both this engine and Task 1's seed `match_check` rows; Task 5's drift test compares them):

- Formats: quantities `0.###`; unit prices `0.00##`; amounts `0.00` (no thousands separator); price % with sign, `+0.0;-0.0;+0.0`.
- Pass messages: `Supplier` "supplier matches the PO"; `Currency` "invoice and PO both in {cur}"; `PO status` "PO is approved"; `PO line` "invoice line {n} matches PO line {p}"; `Quantity` "within the quantity received (line {n})" (or "ordered"); `Unit price` / `Amount` as above.
- Values: `Supplier` `—`/`—`/`—`; `Currency` PO cur / `—` / invoice cur; `PO status` status / `—` / `—`; `PO line` `{item}/{uom}` of PO line (or `—`) / `—` / invoice `{item}/{uom}`; `Quantity` ordered / received (`—` when receipt not required) / invoiced; `Unit price` PO price / `—` / invoice price; `Amount` PO line amount / value available (min(invQty, available) × PO price) / invoice line amount.
- When a line's `PO line` check fails, that line's Quantity, Unit price and Amount checks are not emitted.

| seq | INV-77812 (guide) | NL-2231 (Northline) |
|---|---|---|
| 1 | Supplier, —/—/—, Pass, "supplier matches the PO" | same |
| 2 | Currency, USD/—/USD, Pass, "invoice and PO both in USD" | same |
| 3 | PO status, Approved/—/—, Pass, "PO is approved" | same |
| 4 | PO line (1), GLV-NIT-M/BX / — / GLV-NIT-M/BX, Pass, "invoice line 1 matches PO line 1" | PPR-A4-500/RM, same message |
| 5 | Quantity (1), 100/98/100, **Fail**, "billed 2 more than received (line 1)" | 40/40/40, Pass, "within the quantity received (line 1)" |
| 6 | Unit price (1), 10.00/—/10.20, Pass, "+2.0%, inside the ±3% / $50 price tolerance" | 6.50/—/6.50, Pass, "+0.0%, inside the ±3% / $50 price tolerance" |
| 7 | Amount (1), 1000.00/980.00/1020.00, **Fail**, "$40.00 more than the value received" | 260.00/260.00/260.00, Pass, "matches the value received" |

- [ ] **Step 1:** `gh stack add procurement/02-domain`; create the projects, add to the solution.
- [ ] **Step 2: Write the failing tests.** Fixture `GuideExample()` = PO line 1 `GLV-NIT-M`/`BX` 100 @ 10.00, received {1: 98}, previously invoiced {}, invoice line 1 → PO line 1 `GLV-NIT-M`/`BX` 100 @ 10.20, USD both, Approved, supplier matches.

```csharp
[Fact] Match_GuideExample_BlocksOnQuantityOnly()   // BlockReason "Quantity: billed 2 more than received (line 1)"; Unit price Pass with "+2.0%, inside the ±3% / $50 price tolerance"; Amount Fail "$40.00 more than the value received"; 7 checks in order Supplier, Currency, PO status, PO line, Quantity, Unit price, Amount
[Fact] Match_FullyReceivedAtPoPrice_Passes()       // received 100, price 10.00 → Passed, BlockReason null
[Fact] Match_PriceAtPercentLimit_Passes()          // PO 10 @ 100.00, inv 10 @ 103.00 → +3.0%, $30 → Pass
[Fact] Match_PriceAtAbsoluteLimit_Passes()         // PO 1000 @ 10.00, recv 1000, inv 1000 @ 10.05 → $50.00 → Pass
[Fact] Match_PriceOneCentOverAbsolute_Fails()      // inv 1000 @ 10.0501 → $50.10 → Unit price Fail
[Fact] Match_PriceOverPercent_Fails()              // PO 1 @ 10.00, inv 1 @ 10.31 → Fail
[Fact] Match_Undercharge_Passes()                  // inv 100 @ 9.50, recv 100 → Passed
[Fact] Match_SecondInvoiceCountsPreviouslyInvoiced() // recv 98, prev {1: 60}, inv 40 → "Quantity: billed 2 more than received (line 1)"
[Fact] Match_TwoWayLine_UsesOrdered()              // ReceiptRequired=false, recv {} → inv 100 passes; inv 101 → "Quantity: billed 1 more than ordered (line 1)"
[Fact] Match_CurrencyMismatch_Fails()              // EUR → "Currency: invoice in EUR, PO in USD"
[Fact] Match_DraftPo_Fails()                       // "PO status: PO is Draft, not approved"
[Fact] Match_SupplierMismatch_Fails()              // "Supplier: supplier on invoice is not the PO's supplier"
[Fact] Match_UnknownPoLine_Fails()                 // PoLineNo 7 → "PO line: invoice line 1 has no matching PO line"
[Fact] Match_ItemOrUomMismatch_Fails()             // uom EA → "PO line: invoice line 1 is GLV-NIT-M/EA, PO line is GLV-NIT-M/BX"
```

- [ ] **Step 3: Run, confirm they fail.** `dotnet test tests/Procurement.Domain.Tests`. Expected: compile error, `MatchEngine` missing.
- [ ] **Step 4: Implement** to the rules above (decimal arithmetic, no early rounding).
- [ ] **Step 5: Run, confirm they pass.** Expected: 14 passed.
- [ ] **Step 6: Commit** `feat(procurement): domain model and 3-way match engine`.

### Task 3: Policies and KPI calculator

**Files:** Create `src/Procurement.Domain/Policies/{InvoiceNumberNormalizer,ApprovalRouter,RetroPoRule}.cs`, `Kpis/KpiCalculator.cs`; tests `tests/Procurement.Domain.Tests/{PolicyTests,KpiCalculatorTests}.cs`.

**Interfaces:**
- `static class InvoiceNumberNormalizer { static string Normalize(string raw); }` — uppercase, keep `[A-Z0-9]`, drop a leading run of letters, drop leading zeros (all-zero → `"0"`); may return `""`.
- `record DoaTier(decimal MaxAmount, string ApproverRole)`; `static class ApprovalRouter { static string Route(decimal total, IReadOnlyList<DoaTier> tiers); }` — first tier (ascending) with `total <= MaxAmount`.
- `static class RetroPoRule { static bool IsRetro(DateOnly invoiceDate, DateOnly poDate); }` — `invoiceDate < poDate`.
- `record InvoiceFact(decimal Total, bool HasPo, bool IsPoExemptCategory, bool? FirstMatchPassed, bool IsRetroPo, bool IsBlocked)`; `record KpiSnapshot(decimal PoComplianceBySpendPct, decimal PoComplianceByCountPct, decimal FirstPassMatchRatePct, decimal RetroPoRatePct, int OpenExceptions, int InvoiceCount)`; `static class KpiCalculator { static KpiSnapshot Calculate(IReadOnlyCollection<InvoiceFact> facts); }` — formulas per spec, `Math.Round(x, 1, MidpointRounding.AwayFromZero)`, 0 on empty denominator, OpenExceptions = count IsBlocked.

- [ ] **Step 1: Write the failing tests:**

```csharp
[Theory] Normalize_Variants_SameKey("INV-001","1"), ("inv001","1"), ("1","1"), ("INV-77812","77812"), ("A12B","12B"), ("000","0")
[Fact] Normalize_OnlyPrefix_ReturnsEmpty()        // "INV-" → "", "---" → ""
[Theory] Route_ByTotal(1000→"Cost center owner"), (5000→"Cost center owner"), (5000.01→"Director"), (25000→"Director"), (30000→"VP Finance"), (100000.01→"CFO")
[Fact] IsRetro_InvoiceBeforePo_True()             // 2026-09-30 vs 2026-10-01 → true; same day → false
[Fact] Calculate_Empty_AllZero()
[Fact] Calculate_ExcludesExemptSpend()            // facts (1020,PO,¬ex,false,¬retro,blocked), (980,¬PO,¬ex,null,…), (5000,¬PO,EXEMPT,…), (2000,PO,¬ex,true,retro,¬blocked) → 75.0, 66.7, 50.0, 50.0, OpenExceptions 1, InvoiceCount 4
[Fact] Calculate_SeedData()                       // (1020,PO,…,false,…,blocked), (260,PO,…,true,…) → 100.0, 100.0, 50.0, 0.0, 1, 2
```

- [ ] **Step 2: Run, confirm they fail.** Expected: compile errors.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run, confirm they pass.** `dotnet test tests/Procurement.Domain.Tests` → 14 + all policy/KPI tests passed.
- [ ] **Step 5: Commit** `feat(procurement): invoice-number normalizer, DoA routing, retro-PO, KPIs`.

### Task 4: API foundation, reference data, suppliers, purchase orders

**Files:** Create `src/Procurement.Api/{Procurement.Api.csproj,Program.cs,Errors.cs,Data/{Db,ReferenceStore,SupplierStore,PurchaseOrderStore,AuditLog}.cs,Features/{Reference,Suppliers,PurchaseOrders}/*}`; tests `tests/Procurement.Api.Tests/{ApiFactory.cs,SupplierEndpointTests.cs,PurchaseOrderEndpointTests.cs}`.

**Interfaces:**
- Config: `ConnectionStrings:Procurement`, `Actor:Name` (default `Demo User`).
- `Db`: opens `SqlConnection`, `Task<T> InTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>>)`.
- `AuditLog.WriteAsync(SqlConnection, SqlTransaction, string action, string entityType, Guid entityId, string? from, string? to, string? detail)`.
- Endpoints (all JSON, camelCase):
  - `GET /api/reference` → `{ categories[], glAccounts[], costCenters[], doaTiers[] }`
  - `GET /api/suppliers`, `GET /api/suppliers/{id}`, `POST /api/suppliers` body `{ code, legalName, categoryCode, paymentTermsDays, taxId?, bankAccount? }` → stores last 4 only; response exposes `taxIdMasked: "•••• 4421"`, `bankMasked`. Duplicate code → 409.
  - `GET /api/purchase-orders`, `GET /api/purchase-orders/{id}` → PO + lines + receipts + invoices (summary rows), `POST /api/purchase-orders` body `{ supplierId, requesterName, costCenterCode, currency, poDate, lines[{ itemCode, description, uom, quantity, unitPrice, glAccount, receiptRequired }] }` → Draft, number from `po_number_seq`; `POST /api/purchase-orders/{id}/approve` → sets status Approved, `approver_role` via `ApprovalRouter`, `approved_by` = actor, audit `PoApproved`; non-Draft → 422 "PO 4500123 is Approved; only Draft POs can be approved".
- `ApiFactory : WebApplicationFactory<Program>` gets a fresh database per test class from `SqlServerFixture`.

- [ ] **Step 1:** `gh stack add procurement/03-api`; create projects.
- [ ] **Step 2: Write the failing tests:**

```csharp
[Fact] Reference_ReturnsSeededLists()                 // 8 categories, CC-200, 4 DoA tiers
[Fact] Suppliers_List_IncludesSeededMasked()          // V-10442 taxIdMasked "•••• 4421", bankMasked "•••• 6630"; no full numbers anywhere in the JSON
[Fact] Supplier_Create_StoresLast4Only()              // taxId "98-7654321" → taxIdMasked "•••• 4321"
[Fact] Supplier_DuplicateCode_Returns409()
[Fact] Supplier_MissingName_Returns400Problem()
[Fact] Po_Create_IsDraftWithNextNumber()              // number "4500124", status Draft, total 1000.00
[Fact] Po_Approve_SetsTierByTotal()                   // 1,000 → "Cost center owner"; a 30,000 PO → "VP Finance"; audit_event row PoApproved Draft→Approved
[Fact] Po_ApproveTwice_Returns422()
[Fact] Po_Detail_GuideExample_HasReceiptAndInvoice()  // 4500123: 1 receipt 5000987 (98), 1 invoice INV-77812 Blocked
[Fact] Po_UnknownId_Returns404()
```

- [ ] **Step 3: Run, confirm they fail.** `dotnet test tests/Procurement.Api.Tests`.
- [ ] **Step 4: Implement** stores (Dapper, explicit SQL), services, endpoint groups, `ProblemDetails` mapping in `Errors.cs`.
- [ ] **Step 5: Run, confirm they pass.** Expected: 10 passed.
- [ ] **Step 6: Commit** `feat(procurement): minimal API for reference data, suppliers and purchase orders`.

### Task 5: Invoices, receipts with auto re-match, KPIs

**Files:** Create `src/Procurement.Api/Data/{ReceiptStore,InvoiceStore}.cs`, `Features/{Receipts,Invoices,Kpis}/*`; tests `tests/Procurement.Api.Tests/{InvoiceEndpointTests,ReceiptEndpointTests,KpiEndpointTests}.cs`.

**Interfaces:**
- `InvoiceService.MatchAsync(conn, tx, Guid invoiceId) -> MatchResult`: loads PO, lines, received totals, **previously invoiced = Matched invoices other than this one**, runs `MatchEngine`, replaces `match_check` rows, sets status/reason, sets `first_match_passed` only if NULL, audits `InvoiceMatched` / `InvoiceBlocked` with from/to status.
- `POST /api/invoices` body `{ supplierId, invoiceNumber, invoiceDate, currency, poNumber, taxAmount, lines[{ poLineNo, itemCode, uom, description, quantity, unitPrice }] }`:
  - normalized empty → 400; duplicate (supplier, normalized) → 409 "Invoice INV-77812 from Bluepeak Medical Supply is already recorded".
  - unknown PO → saved Blocked, reason `PO: PO {n} not found`, no checks; blocked supplier → Blocked `Supplier: supplier V-… is blocked`; otherwise `MatchAsync`. `due_date` = invoice date + supplier terms; `is_retro_po` via `RetroPoRule`.
- `POST /api/invoices/{id}/rematch` → `MatchAsync`; `GET /api/invoices?status=Blocked`, `GET /api/invoices/{id}` (header, lines, checks, total = Σ lines + tax).
- `POST /api/purchase-orders/{id}/receipts` body `{ receivedBy, packingSlip?, lines[{ poLineNo, quantity }] }` → in one transaction: lock PO lines (`WITH (UPDLOCK, HOLDLOCK)`), reject cumulative over-receipt (422 "Line 1: receiving 3 would bring the total to 101, more than the 100 ordered"), insert GR (number from `gr_number_seq`), then `MatchAsync` every Blocked invoice on that PO; audit `GoodsReceived`.
- `GET /api/kpis` → `KpiSnapshot` from `InvoiceFact`s (spend category = supplier's).

- [ ] **Step 1: Write the failing tests:**

```csharp
[Fact] Rematch_SeededGuideInvoice_ReproducesSeededChecks()   // drift guard: status, reason and all 7 match_check rows identical before/after
[Fact] Receipt_LastTwoBoxes_AutoRematchesToMatched()          // POST receipt line 1 = 2 → INV-77812 Matched, reason null, first_match_passed still false, audit rows GoodsReceived + InvoiceMatched
[Fact] Receipt_CumulativeOverReceipt_Returns422()             // 98 + 3 > 100
[Fact] Receipt_ConcurrentOverReceipt_OnlyOneSucceeds()        // two parallel receipts of 2 → one 201, one 422; total received 100
[Fact] Invoice_DuplicateNumberVariant_Returns409()            // Bluepeak "inv77812"
[Fact] Invoice_SameNumberOtherSupplier_Accepted()             // Northline "INV-77812" → 201
[Fact] Invoice_NumberNormalizesToEmpty_Returns400()           // "INV-"
[Fact] Invoice_UnknownPo_SavedBlockedWithReason()             // "PO: PO 4599999 not found"
[Fact] Invoice_DraftPo_Blocked()                              // "PO status: PO is Draft, not approved"
[Fact] Invoice_BlockedSupplier_Blocked()
[Fact] Invoice_BeforePoDate_FlaggedRetro()
[Fact] Invoice_SecondInvoice_CountsMatchedOnly()              // after INV-77812 is still Blocked, a new invoice for 98 on 4500123 → Matched
[Fact] Invoice_Detail_TotalIncludesTax()                      // lines 1020.00 + tax 12.50 → total 1032.50
[Fact] Kpis_SeedData()                                        // 100.0, 100.0, 50.0, 0.0, openExceptions 1, invoiceCount 2
```

- [ ] **Step 2: Run, confirm they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run, confirm they pass.** `dotnet test tests/Procurement.Api.Tests` → 24 passed.
- [ ] **Step 5: Commit** `feat(procurement): invoices with 3-way match, receipts with auto re-match, KPIs`.

### Task 6: BFF

**Files:** Create `src/Procurement.Bff/{Procurement.Bff.csproj,Program.cs,ApiClient.cs,Views/*.cs,wwwroot/index.html}` (placeholder `index.html` until Task 9 copies the Angular build); tests `tests/Procurement.Bff.Tests/{FakeApi.cs,BffTests.cs}`.

**Interfaces:**
- Config `Api:BaseUrl`. YARP route `api` = `/api/{**catch-all}` → cluster `api` (same path). Later Stack 2 adds `/hubs/{**catch-all}`.
- `GET /bff/dashboard` → `{ kpis: KpiSnapshot, exceptions: [{ id, invoiceNumber, supplierName, total, blockReason }] (Blocked, newest first, max 5) }`.
- `GET /bff/purchase-orders/{id}` → `{ po, supplier }`; `GET /bff/invoices/{id}` → `{ invoice, po?, supplier }`.
- API unreachable or 5xx on a `/bff` call → 502 `ProblemDetails` title "The procurement API is unavailable". API 404 → 404.
- Static files from `wwwroot`; `MapFallbackToFile("index.html")` only for paths not starting `/api` or `/bff`.
- `FakeApi`: an in-process Kestrel app on a random port with canned responses, started per test class.

- [ ] **Step 1:** `gh stack add procurement/04-bff`.
- [ ] **Step 2: Write the failing tests:**

```csharp
[Fact] Api_PassThrough_ForwardsMethodPathQueryBody()     // POST /api/invoices?x=1 reaches FakeApi unchanged
[Fact] Api_Error_PreservesStatusAndProblemDetails()      // FakeApi 409 problem → client sees 409 same body
[Fact] Dashboard_CombinesKpisAndExceptions()
[Fact] Dashboard_ApiDown_Returns502Problem()
[Fact] InvoiceView_IncludesPoAndSupplier()
[Fact] SpaFallback_DeepLink_ReturnsIndex()               // /invoices/abc → index.html
[Fact] UnknownApiPath_Returns404NotIndex()               // /bff/nope and /api/nope (FakeApi 404) → 404, not HTML
```

- [ ] **Step 3: Run, confirm they fail.**
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run, confirm they pass.** Expected: 7 passed.
- [ ] **Step 6: Commit** `feat(procurement): BFF with YARP pass-through and screen views`.

### Task 7: Angular shell, API client, stores, dashboard and suppliers

**Files:** Create `web/` via `npx @angular/cli@22.2 new web --routing --style=css --ssr=false --skip-git --skip-install` then `npm install @ngrx/signals@22.0`; `web/proxy.conf.json` (`/api`, `/bff` → `http://localhost:5300`); `src/app/core/{procurement-api.ts,models.ts}`, `src/app/dashboard/{dashboard.store.ts,dashboard.page.ts}`, `src/app/suppliers/{suppliers.store.ts,supplier-list.page.ts,supplier-new.page.ts}`, `src/app/shared/{status-badge.ts,error-banner.ts}`, `app.routes.ts`, layout with nav (Dashboard, Suppliers, Purchase orders, Invoices).

**Interfaces:**
- `ProcurementApi` (injectable, `HttpClient`): `dashboard()`, `suppliers()`, `createSupplier(body)`, `reference()`, `purchaseOrders()`, `purchaseOrder(id)` (via `/bff/purchase-orders/{id}`), `createPurchaseOrder`, `approve(id)`, `receive(id, body)`, `invoices(status?)`, `invoice(id)` (via `/bff/invoices/{id}`), `createInvoice`, `rematch(id)`. Returns `Observable`s; models in `models.ts` mirror the API JSON.
- Stores: `signalStore({ providedIn: 'root' }, withState({ items, loading, error }), withComputed(...), withMethods(...))`. Every store exposes `error: string | null` set from `ProblemDetails.detail` (or the 502 title) on failure.

- [ ] **Step 1:** `gh stack add procurement/05-ui`; scaffold and install.
- [ ] **Step 2: Write the failing tests** (`*.spec.ts`, `HttpTestingController`):

```ts
it('DashboardStore loads kpis and exceptions')              // GET /bff/dashboard → kpis().firstPassMatchRatePct === 50, exceptions().length === 1
it('DashboardStore_LoadFails_SetsError')                    // 502 problem → error() === 'The procurement API is unavailable'
it('SuppliersStore create adds supplier and clears error')
it('SuppliersStore create 409 sets error from problem detail')
it('StatusBadge renders Blocked in the danger style')
it('Dashboard page shows the open exception INV-77812 with its reason')
```

- [ ] **Step 3: Run, confirm they fail.** `cd web && npx ng test --watch=false`.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run, confirm they pass**, and `npx ng build` succeeds.
- [ ] **Step 6: Commit** `feat(procurement): Angular shell, NgRx Signals stores, dashboard and suppliers`.

### Task 8: Angular purchase orders, receipts, invoices and match detail

**Files:** Create `src/app/purchase-orders/{purchase-orders.store.ts,po-list.page.ts,po-new.page.ts,po-detail.page.ts,receive-form.ts}`, `src/app/invoices/{invoices.store.ts,invoice-list.page.ts,invoice-new.page.ts,invoice-detail.page.ts}`, `src/app/shared/match-table.ts`.

**Interfaces:**
- `MatchTable` input `checks: MatchCheck[]`; renders columns Check / Purchase order / Goods receipt / Invoice / Result, one row per check, Fail rows marked, message under the result.
- PO detail: lines, Approve button (Draft only), receipts list, Receive form (per line quantity), invoices list; after receive, reloads PO and shows "Invoice INV-77812 is now Matched" when a linked invoice changed status.
- Invoice new: supplier, number, date, currency, PO number, tax, lines (PO line no, item, uom, qty, price); on 409/400 shows the problem detail inline. Invoice detail: header, total, status badge, block reason, `MatchTable`, Re-match button when Blocked.

- [ ] **Step 1: Write the failing tests:**

```ts
it('MatchTable renders the guide example rows in order with Quantity and Amount failed')
it('PurchaseOrdersStore approve updates status and approver role')
it('PurchaseOrdersStore receive 422 sets error "Line 1: receiving 3 would bring the total to 101, more than the 100 ordered"')
it('InvoicesStore create 409 sets duplicate error')
it('Invoice detail shows Re-match only when Blocked')
it('PO detail after receiving reports the invoice that changed to Matched')
```

- [ ] **Step 2: Run, confirm they fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run, confirm they pass**; `npx ng build` succeeds.
- [ ] **Step 5: Commit** `feat(procurement): purchase orders, receipts, invoices and match detail UI`.

### Task 9: Containers, compose, README and smoke test

**Files:** Create `docker/{api,bff,db-deploy}.Dockerfile`, `docker-compose.yml`, `.env.example`, `README.md`, `tools/smoke.sh` (`.gitignore` already exists from Task 1; extend it only if needed).

**Interfaces:**
- `sqlserver`: image pinned, `platform: linux/amd64`, `ACCEPT_EULA=Y`, `MSSQL_SA_PASSWORD=${SA_PASSWORD}`, port `14330:1433`, volume `procurement-sqldata`, healthcheck `/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1"` (interval 10s, retries 20, start_period 60s).
- `db-deploy`: built from `mcr.microsoft.com/dotnet/sdk:10.0` with `dotnet tool install -g microsoft.sqlpackage --version 170.5.96` and `dotnet build` of the sqlproj; runs `sqlpackage /Action:Publish /SourceFile:… /TargetConnectionString:"…TrustServerCertificate=True;Database=procurement"`; `depends_on: sqlserver: service_healthy`; `restart: "no"`.
- `api`: `depends_on: db-deploy: service_completed_successfully`; env `ConnectionStrings__Procurement`; no host port.
- `bff`: multi-stage (node 22 builds `web` → copied into BFF `wwwroot`), `Api__BaseUrl=http://api:8080`, port `5300:8080`, `depends_on: api`.
- `tools/smoke.sh`: `docker compose up -d --build --wait`; asserts `GET :5300/bff/dashboard` `kpis.openExceptions == 1`; finds INV-77812, asserts Blocked with the guide reason; POSTs a receipt of 2 on PO 4500123 line 1; asserts INV-77812 is Matched and `openExceptions == 0`; `GET :5300/invoices/<id>` returns HTML; exits non-zero on any failure; `docker compose down -v` in a trap.

- [ ] **Step 1:** `gh stack add procurement/06-infra-docs`.
- [ ] **Step 2: Write `tools/smoke.sh` first; run it.** Expected: fails (no compose file).
- [ ] **Step 3: Implement** Dockerfiles, compose, `.env.example` (`SA_PASSWORD=ChangeMe_Str0ng!`), README (prereqs, `cp .env.example .env`, `docker compose up --build`, open http://localhost:5300, run tests, Apple Silicon note, layer map).
- [ ] **Step 4: Run** `bash tools/smoke.sh`. Expected: `SMOKE PASS`, exit 0.
- [ ] **Step 5: Full suite on the top branch:** `dotnet test Procurement.sln` and `(cd web && npx ng test --watch=false)` → all pass.
- [ ] **Step 6: Commit** `feat(procurement): docker compose, dacpac deploy, README and smoke test`.

### Finish (via stacked-superpowers Step 5)

- [ ] Developer asked for PRs? Then `gh stack submit --auto` and `gh stack view --json`, report URLs. Otherwise stop with the stack local (or pushed with `gh stack push` if asked) and offer to submit.
