# Architecture: Procurement & AP Workbench

A procurement and accounts-payable workbench: raise a purchase order, receive goods, enter the supplier's invoice and watch the automatic 3-way match pass or block. An Angular single-page app talks only to a .NET BFF; a .NET Minimal API owns the rules and the data; SQL Server's schema is owned by a dacpac. Everything runs with one `docker compose up`.

Spec: [docs/superpowers/specs/2026-11-13-procurement-app-design.md](docs/superpowers/specs/2026-11-13-procurement-app-design.md). Plan: [PLAN.md](PLAN.md).

## C4 Level 1: System context

```mermaid
flowchart LR
    user(["AP clerk / buyer<br/>[Person]<br/>Raises POs, receives goods,<br/>enters and reviews invoices"])
    system["Procurement &amp; AP Workbench<br/>[Software system]<br/>PO, receipt, invoice, 3-way match, KPIs"]
    guide["Procurement guide<br/>[Reference]<br/>procurement-overview.html:<br/>source of the domain rules"]

    user -- "uses in a browser (HTTP)" --> system
    guide -. "domain rules are taken from" .-> system
```

No authentication in scope: a configured demo actor name is recorded on audit rows. No ERP, payment or OCR integration.

## C4 Level 2: Containers

```mermaid
flowchart TB
    subgraph host["Docker compose (host)"]
        browser["Browser<br/>Angular SPA + NgRx Signals<br/>(signalStore per feature)"]

        subgraph bffbox["Procurement.Bff : host port 5300"]
            bff["ASP.NET Core BFF<br/>serves SPA static files + SPA fallback<br/>YARP forwards /api/*<br/>/bff/* view endpoints (no rules, no SQL)"]
        end

        api["Procurement.Api<br/>ASP.NET Core Minimal API + Dapper<br/>internal :8080, not published<br/>uses Procurement.Domain (pure rules)"]
        sql[("SQL Server 2025<br/>host port 14330")]
        deploy["db-deploy<br/>one-off sqlpackage container<br/>publishes Procurement.Database.dacpac, exits 0"]
    end

    browser -- "HTTP :5300 (only backend)" --> bff
    bff -- "YARP /api/* pass-through" --> api
    bff -- "typed HttpClient (/bff/* aggregation)" --> api
    api -- "Dapper + SqlClient, one transaction per command" --> sql
    deploy -- "sqlpackage publish (schema + seed)" --> sql
```

Compose order: `sqlserver` (healthy) then `db-deploy` (exits 0) then `api` then `bff`.

## Sequence: enter invoice

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant S as Angular store
    participant B as BFF :5300
    participant A as API :8080
    participant D as SQL Server

    U->>S: submit invoice form
    S->>B: POST /api/invoices
    B->>A: forward (YARP)
    A->>D: load PO, receipts, prior invoice lines, supplier
    D-->>A: rows
    Note over A,D: BEGIN TRANSACTION
    A->>A: normalize invoice number
    A->>D: duplicate check (supplier + normalized number)
    A->>A: run domain match (header, then per line)
    A->>D: insert invoice + lines
    A->>D: insert match_check rows
    A->>D: insert audit_event
    Note over A,D: COMMIT
    A-->>B: 201 invoice + match result (Matched or Blocked + reason)
    B-->>S: pass through
    S-->>U: store updates, match detail page renders checks
```

A duplicate returns 409; bad references (unknown or unapproved PO, blocked supplier, currency mismatch) yield a Blocked invoice with a readable reason, not an error page.

## Rules the diagram encodes

- The browser talks only to the BFF, never to the API or SQL Server.
- The BFF holds no business rules, no SQL and no connection string; it serves the SPA, forwards `/api/*` and shapes `/bff/*` screen payloads.
- The schema lives only in the dacpac (`db/Procurement.Database`); no EF, nothing in app startup changes tables.
- Money-moving status changes (approve PO, block/match invoice) write an `audit_event` in the same transaction as the change.
- Secrets (SA password, connection string) live only in a git-ignored `.env`; `.env.example` documents them.
- The API is internal; only the BFF (5300) and SQL Server (14330, for local tests and tooling) are published to the host.

## Layer to stack-branch map

Topic `procurement`, trunk `main`. One PR per layer; each builds and passes its own tests.

| # | Branch | Layer | Tasks |
|---|---|---|---|
| 0 | `procurement/00-design` | spec + plan + architecture | 0 |
| 1 | `procurement/01-schema` | schema (dacpac, seed, test support) | 1 |
| 2 | `procurement/02-domain` | domain | 2, 3 |
| 3 | `procurement/03-api` | api | 4, 5 |
| 4 | `procurement/04-bff` | bff | 6 |
| 5 | `procurement/05-ui` | ui | 7, 8 |
| 6 | `procurement/06-infra-docs` | infra + docs | 9 |
