# Data API builder (read-only)

[Data API builder](https://learn.microsoft.com/azure/data-api-builder/) (DAB) serves **read-only** REST and
GraphQL endpoints straight from the procurement tables. The service has no code to build: one JSON file says
which tables it exposes and who may do what.

DAB is **read-only on purpose**. Writes go through `Procurement.Api`, which runs the match engine and approval
routing, assigns PO and receipt numbers, and writes the audit trail. If DAB wrote to the tables directly, it
would skip all of that.

## Folder structure

```text
dab/
├── dab-config.json   # the whole service: data source, runtime settings, entities, permissions
└── README.md         # this file
```

The compose service `dab` lives in [`../docker-compose.yml`](../docker-compose.yml). It mounts
`dab-config.json` read-only into the official image (`mcr.microsoft.com/azure-databases/data-api-builder:2.1.5`).

### How `dab-config.json` is laid out

| Section | What it sets |
|---|---|
| `$schema` | The DAB 2.1.5 JSON schema, so VS Code autocompletes and validates the file. |
| `data-source` | `mssql`, with the connection string read from the `DAB_CONNECTION_STRING` environment variable (`@env(...)`), which compose builds from `SA_PASSWORD` in `.env`. The password is never in this file. |
| `runtime.rest` | REST under `/api`. |
| `runtime.graphql` | GraphQL under `/graphql`, with introspection on so the GraphQL UI can show the schema. |
| `runtime.host` | `development` mode turns on Swagger and the GraphQL UI. The `cors.origins` list is empty, so browsers on other sites can't call it. |
| `entities` | One entry per table (below). |

Every entity has the same shape:

```json
"PurchaseOrder": {
  "source":      { "object": "dbo.purchase_order", "type": "table" },
  "rest":        { "enabled": true, "path": "/purchase-orders" },
  "graphql":     { "enabled": true, "type": { "singular": "PurchaseOrder", "plural": "PurchaseOrders" } },
  "permissions": [ { "role": "anonymous", "actions": [ "read" ] } ],
  "relationships": {
    "supplier": { "cardinality": "one",  "target.entity": "Supplier" },
    "lines":    { "cardinality": "many", "target.entity": "PoLine" }
  }
}
```

- **`permissions`** gives only `read` to every entity. That's what makes the service read-only: REST
  `POST`/`PUT`/`PATCH`/`DELETE` return `403`, and the GraphQL schema has no mutations at all.
- **`relationships`** are used by GraphQL only, for nested queries. DAB works out the join columns from
  the foreign keys in the database, so they aren't listed here.

### Entities

| Entity | Table | REST path | Relationships (GraphQL) |
|---|---|---|---|
| `SpendCategory` | `spend_category` | `/api/spend-categories` | |
| `CostCenter` | `cost_center` | `/api/cost-centers` | |
| `GlAccount` | `gl_account` | `/api/gl-accounts` | |
| `DoaTier` | `doa_tier` | `/api/doa-tiers` | |
| `Supplier` | `supplier` | `/api/suppliers` | `category`, `purchaseOrders`, `invoices` |
| `PurchaseOrder` | `purchase_order` | `/api/purchase-orders` | `supplier`, `costCenter`, `lines`, `receipts`, `invoices` |
| `PoLine` | `po_line` | `/api/po-lines` | `purchaseOrder`, `glAccount` |
| `GoodsReceipt` | `goods_receipt` | `/api/goods-receipts` | `purchaseOrder`, `lines` |
| `GrLine` | `gr_line` | `/api/gr-lines` | `receipt` |
| `Invoice` | `invoice` | `/api/invoices` | `supplier`, `purchaseOrder`, `lines`, `matchChecks` |
| `InvoiceLine` | `invoice_line` | `/api/invoice-lines` | `invoice` |
| `MatchCheck` | `match_check` | `/api/match-checks` | `invoice` |
| `AuditEvent` | `audit_event` | `/api/audit-events` | |

Field names are the table's column names (`po_number`, `legal_name`, ...).

## Run it

DAB starts with the rest of the stack, after `db-deploy` has published the schema:

```bash
cd procurement-app
docker compose up -d            # whole stack, including dab
docker compose up -d dab        # or just dab (starts sqlserver and db-deploy first if needed)
docker compose logs -f dab
```

| URL | What |
|---|---|
| <http://localhost:5400/api/suppliers> | REST |
| <http://localhost:5400/swagger> | Swagger UI for the REST endpoints |
| <http://localhost:5400/api/openapi> | OpenAPI document |
| <http://localhost:5400/graphql> | GraphQL endpoint; opening it in a browser shows the GraphQL UI |

The port is bound to `127.0.0.1` only, like the other services. **Apple Silicon:** the DAB image is
amd64-only, so it runs under Rosetta (`platform: linux/amd64`), same as SQL Server.

## Query examples

### REST

```bash
B=http://localhost:5400

# List (paged: 100 rows by default; follow "nextLink" in the response for more)
curl "$B/api/suppliers"

# One row by primary key
curl "$B/api/suppliers/id/A1B2C3D4-0001-4000-8000-000000010442"

# Composite key: list every key column
curl "$B/api/po-lines/po_id/<po-guid>/line_no/1"

# Filter, choose columns, sort, limit (OData-style; -G + --data-urlencode handles the spaces and quotes)
curl -G "$B/api/purchase-orders" \
  --data-urlencode "\$filter=status eq 'Approved'" \
  --data-urlencode '$select=po_number,status' \
  --data-urlencode '$orderby=po_number desc' \
  --data-urlencode '$first=3'
```

### GraphQL

```bash
curl http://localhost:5400/graphql -H 'content-type: application/json' -d '{
  "query": "{ purchaseOrders(first: 5, filter: { status: { eq: \"Approved\" } }) { items { po_number status supplier { legal_name } lines { items { line_no description quantity unit_price } } } } }"
}'
```

## Changing the config

Edit `dab-config.json`, then restart the service. DAB reads the file at start-up:

```bash
docker compose restart dab
```

- **Expose a new table:** add an entity in the same shape as the others, with `"actions": ["read"]`.
- **Hide columns** (for example `supplier.tax_id_last4` and `bank_last4`): add
  `"fields": { "exclude": ["tax_id_last4", "bank_last4"] }` to that permission entry.
- **Keep it read-only:** never add `create`, `update`, `delete` or `*` to `actions`. Data changes belong
  in `Procurement.Api`.

## Limits

- **The read-only rule lives in `dab-config.json`, not in the database.** DAB connects as `sa`, the same
  login the API uses. A config that granted more actions would be able to write. For stronger isolation,
  give DAB its own SQL login with only `db_datareader`.
- **No authentication.** Every request runs as the `anonymous` role. That's fine on localhost, but don't
  publish the port beyond `127.0.0.1` without adding authentication.
- **`development` mode** is for local use: it shows the Swagger and GraphQL UIs and includes detailed
  errors in responses. Set `runtime.host.mode` to `production` anywhere else.
