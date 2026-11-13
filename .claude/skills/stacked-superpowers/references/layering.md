# Choosing layers

A layer is **one concern that the layer above depends on**, describable in one sentence. Dependencies only point down: code in a layer may use its own layer and the ones below it, never the ones above.

## 1. Does the repo have tiers?

Look for separate homes for each tier. Signals:

| Tier | Signals |
|---|---|
| schema | `db/`, `migrations/`, `db/migrate/`, `changelog/`, `*.sql` changesets, `prisma/`, Liquibase/Flyway config |
| domain | `*.Domain` / `*.Core` projects, `domain/`, `core/`, `lib/` with no web framework imports |
| api | `*.Api` / `*.Web` projects, `api/`, `server/`, controllers, `Program.cs` with endpoints |
| bff | `bff/`, `gateway/`, a Node/Express or YARP service sitting between UI and API |
| ui | `angular.json`, `web/`, `client/`, `frontend/`, `src/app/` with components |
| infra-docs | `deploy/`, `charts/`, `docker-compose*.yml`, `.github/workflows/`, `docs/` |

Also read `*.sln`, `package.json` workspaces and `git log --oneline -20` (to match the repo's branch and commit naming).

**Tiers found** → one layer per tier the change touches, in this order, skipping the rest:

`schema → domain → api → bff → ui → infra-docs`

**No clean tiers** (one project, a monolith, a legacy service) → layer by concern:

`prep-refactor → core → callers-ui → tests-docs`

- `prep-refactor`: behaviour-neutral moves/renames/extractions the change needs.
- `core`: the change itself, with its unit tests.
- `callers-ui`: code that starts using it (controllers, views, jobs), with their tests.
- `tests-docs`: only cross-layer integration/e2e tests and docs. Skip if empty.

Risky data changes (renames, type changes on live tables) may need expand → backfill → switch → contract layers instead; propose that when a plain migration would break a running deploy.

## 2. Rules for every layer

- Each layer builds and passes its tests on its own. Unit tests live in the layer of the code they test.
- Config or DI wiring a layer needs to build goes in that layer, not the top.
- A layer you can't describe in one sentence is two layers.
- Fewer than two non-empty layers → no stack; one ordinary PR.
- Unrelated work (a separate bug fix, another feature) → a separate stack, not another layer.

## 3. Naming

`<topic>/<nn>-<concern>`, e.g. `po-approval/00-design`, `po-approval/01-schema`, `po-approval/02-domain`. `<topic>` is short kebab-case for the feature. Numbers keep the order visible in branch lists. The repository's own branch convention wins over this.

## 4. The layer proposal (what to send for approval)

```
Stack for po-approval (bottom to top):
  00-design   spec + plan                                  (Full mode only)
  01-schema   approval columns on purchase_order + rollback
  02-domain   approve/reject rules + unit tests
  03-api      pending list + approve/reject endpoints + tests
  04-bff      pass-through routes + tests
  05-ui       pending-approvals page + component tests
OK to create these branches?
```

Split mode adds the files under each layer.

## 5. The Stack table (Full mode plans)

Every plan task carries exactly one layer. The plan ends with:

```markdown
## Stack

| # | Branch | Layer | Tasks |
|---|---|---|---|
| 0 | `po-approval/00-design` | spec + plan | — |
| 1 | `po-approval/01-schema` | schema | 1 |
| 2 | `po-approval/02-domain` | domain | 2, 3 |
```

Tasks are ordered so no task depends on a layer above its own.
