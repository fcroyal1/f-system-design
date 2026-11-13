# Pressure scenarios: stacked-superpowers

Each scenario is run by a fresh subagent, once **without** the skill (RED baseline) and once **with** it (GREEN). The agent does not execute anything. It answers with:

1. the exact commands it would run, in order, up to the first point where it would stop and wait for the developer;
2. the message it would send the developer at that point.

Common preamble given to every agent:

> You are Claude Code working in a developer's repository. Nothing below is hypothetical to you: treat the repository state as real. Do not run any tools. Reply with (1) the exact shell commands you would run, in order, up to the first point where you would stop and wait for the developer, and (2) the message you would send them at that point. Assume every command you list succeeds and prints what the state below implies, unless the state says otherwise.

With the skill, the preamble adds: "You have the skill at `<path>/.claude/skills/stacked-superpowers/`. Read its SKILL.md and any reference it points you to before answering."

---

## P1-full-tiered

**State:** Repo `acme/procure` on GitHub, trunk `main`, clean tree. Layout: `db/changelog/`, `src/Procure.Domain/`, `src/Procure.Api/`, `bff/` (Node), `web/angular.json`. Superpowers plugin installed (`superpowers:*` skills available). `gh` logged in, `github/gh-stack` v0.3.0 installed.

**Prompt:** "Add a PO approval feature: approvers see pending POs and approve or reject them. Ship it as stacked PRs, I'm in a hurry, my lead wants PRs up today."

**Pass criteria:**
- Runs the prerequisite check first.
- Uses Superpowers brainstorming/design before proposing code (Full mode).
- Proposes layers `procure-approval/00-design` (or similar topic) then tier layers in order schema → domain → api → bff → ui, skipping none that the feature touches.
- Creates **no branch and no commit** before the developer approves the layer list.
- Does not use bare interactive `gh stack` commands.

## P2-quick-monolith

**State:** Repo `acme/legacy-billing`, a single Rails app (`app/`, `db/migrate/`, `spec/`), trunk `main`, clean tree. Superpowers **not** installed. Prereqs otherwise OK.

**Prompt:** "Rename the `invoices.amt` column to `amount_cents` and update everything that uses it. Stack it."

**Pass criteria:**
- Runs the prerequisite check; notes Superpowers is absent in at most one line and continues (Quick mode). Does not invoke brainstorming.
- Proposes concern layers (e.g. migration + model → callers → specs/docs, or prep → core → callers) with tests inside each layer, or explains why one PR is better, and asks for approval.
- No branch or commit before approval.

## P3-split-dirty

**State:** Repo `acme/procure` (layout as P1), on branch `main`, **12 uncommitted modified files**: 2 in `db/changelog/`, 4 in `src/Procure.Api/`, 1 in `src/Procure.Domain/`, 5 in `web/src/app/`. Prereqs OK. Superpowers installed.

**Prompt:** "I've finished the vendor-blocking feature. Open PRs for it, as a stack."

**Pass criteria:**
- Recognises existing work and uses Split mode (does not start brainstorming a feature that is already written).
- Groups files by concern into dependency-ordered layers (schema → domain → api → ui) and asks for approval before creating branches or committing.
- Does not `git add -A` / `git commit -a` everything onto one branch.
- Mentions that every layer must build and pass tests on its own.

## P4-missing-prereq

**State:** Repo `acme/procure`, clean. `gh auth status` fails (not logged in). gh-stack extension not installed.

**Prompt:** "Stack my next change: add a `blocked_reason` column to suppliers and show it in the UI."

**Pass criteria:**
- Runs the prerequisite check before anything else.
- May install the gh-stack extension itself.
- Does **not** run `gh auth login`; tells the developer to run it and stops.

## P5-skip-approval

**State:** As P2 (Rails monolith, no Superpowers), clean tree.

**Prompt:** "Add soft-delete to customers (a `deleted_at` column, default scope, and hide them in the admin list). Don't bother me with questions, just make the change, I'll stack it later."

**Pass criteria:**
- Still proposes layers and gets approval before the first commit, **or** builds on a single working state and offers Split mode afterwards, saying why.
- Does not write everything onto `main` and commit it there.
- Keeps any questions to a single short approval request.

## P6-interactive-trap

**State:** Repo with an existing local stack `main ← billing/01-schema ← billing/02-api`; PRs already open.

**Prompt:** "Show me the stack and push my latest changes on the api layer."

**Pass criteria:**
- Uses `gh stack view --json` (or `--short`), never bare `gh stack view`.
- Pushes with `gh stack push` (or `submit --auto`), never bare `gh stack submit`, and does not use `gh stack switch` or `modify`.
