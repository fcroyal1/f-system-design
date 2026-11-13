# Design: `stacked-superpowers` skill

**Date:** 2026-11-13 · **Size:** project (full design) · **Status:** approved 2026-11-13

## Intent and why

A reusable Claude Code skill that lets a team ship work as **stacked pull requests** with `gh stack`, one reviewable PR per layer, in any repository: a greenfield app with clean architectural tiers, or an existing codebase where the layers are not obvious. When the Superpowers plugin is installed, the skill runs the full Superpowers workflow (brainstorm → design → plan → build → review → finish) and ships the result as a stack. When it is not, the skill still works for a quick change.

The user wants this for their developers, not only for themselves, so it must be easy to install from a repo, safe to run unattended, and clear about what it needs. The first real use is the Procurement & AP Workbench (Angular + NgRx, BFF, .NET API).

## Goals

### 1. Prerequisite check

`scripts/check-prereqs.sh` runs before anything else. Each item prints `OK`, `FIXED`, `WARN`, or `MISSING: <command to run>`. The script exits non-zero only if something is MISSING.

| Check | If not satisfied |
|---|---|
| Git ≥ 2.36 | MISSING, print upgrade hint |
| `gh` installed | MISSING, print install hint |
| `gh auth status` succeeds | MISSING: "run `gh auth login`" (never run by the skill) |
| `github/gh-stack` extension installed | **auto-install**: `gh extension install github/gh-stack` → FIXED |
| Installed gh-stack newer than the bundled docs' version | WARN: bundled docs may be stale |
| `rerere.enabled` (repo-local) | **auto-set** to `true` → FIXED |
| `remote.pushDefault` when the repo has >1 remote | **auto-set** to `origin` if it exists, else MISSING |
| Repo has a GitHub remote | MISSING |
| Another `gh-stack` skill installed (e.g. `.claude/skills/gh-stack`) | WARN: two skills trigger on "stack"; suggest removing the other copy |

Checked by the agent, not the script: whether `superpowers:*` skills appear in its skill list. Absent → one line recommending `/plugin install superpowers@claude-plugins-official`, then continue in Quick mode. Never install it.

If GitHub returns `gh stack` exit 9 (stacked PRs not enabled on the repository), the skill stops and tells the user.

### 2. Three entry points

| Mode | When | Flow |
|---|---|---|
| **Full** | Superpowers installed, and the work is a feature or larger | `superpowers:brainstorming` → design → `superpowers:writing-plans` with layer tags and a Stack section (Goal 4) → user approves layers → build per layer (Goal 5) → finish (Goal 6) |
| **Quick** | Superpowers absent, or a small change the dev wants to stack without a design | Dev describes the change → skill reads the relevant code → proposes layers (Goal 3) → dev approves → build per layer → finish |
| **Split** | Dev already has a large branch or uncommitted work | Skill reads the diff against trunk → groups files/hunks by concern → proposes layers → dev approves → creates the stack and commits each group to its layer → checks every layer builds and tests on its own → finish |

Full vs Quick is the skill's proposal, which the dev can override in one word. Split is offered when the skill finds unpushed commits or a dirty tree at the start, and always when asked for. Split is a recovery path: gh-stack's guidance is to plan layers before writing code, and the skill says so once.

### 3. Choosing layers

A layer is **one concern that the layer above depends on**, describable in one sentence. Layers are always proposed and approved by the dev before any branch exists.

- **Repo has clear tiers** (separate projects/folders for DB, domain, API, BFF, UI): propose one layer per tier the change touches, in dependency order, e.g. `schema → domain → api → bff → ui → infra-docs`. Empty tiers are skipped.
- **Repo has no clean tiers** (monolith, legacy service, single project): propose layers by concern, e.g. `prep refactor → core change → callers / UI → tests and docs`.
- **Change too small** for two layers: say so and suggest a single ordinary PR instead of a stack.

The skill reads the repository's layout (solution/project files, top-level folders, `package.json`/`angular.json`, etc.) and recent branch names to make the proposal and match conventions.

Branch naming: `<topic>/<nn>-<concern>`, e.g. `procurement/03-api`. A repository's own naming convention overrides this.

### 4. Layered plans (Full mode)

On top of what `superpowers:writing-plans` produces, the plan must:

- tag every task with exactly one layer;
- order tasks so a task never depends on a layer above its own;
- include a **Stack** section: layer → branch name → task numbers, bottom to top.

### 5. Building per layer

- Create the stack before writing files: `gh stack init <first-branch>`, then `gh stack add <branch>` for each next layer when its work starts.
- Full mode: execute the layer's tasks with `superpowers:subagent-driven-development` (or `executing-plans`), then `superpowers:requesting-code-review` and `superpowers:verification-before-completion` scoped to that layer's diff.
- Quick/Split mode: build, then run the repo's build and tests on that branch.
- A fix that belongs to a lower layer: `gh stack checkout`/`down` to that layer, commit there, `gh stack rebase --upstack`, `gh stack top`.
- Stage explicit paths (`git add <paths>`), never `git add -A`, so each commit stays in its layer.

### 6. Finishing

`gh stack submit --auto` (draft PRs), then `gh stack view --json` to report each PR's URL. In Full mode this replaces the merge/PR step of `superpowers:finishing-a-development-branch`. Merging stays with the dev (`gh stack merge <pr> --yes` is documented, never run by the skill unless asked).

### 7. Self-contained (approach B) and shareable

- Bundles the gh-stack skill's instructions and its references (`commands`, `stack-design`, `troubleshooting`) under `references/gh-stack/`, with `UPSTREAM.md` recording repo `github/gh-stack`, tag `v0.3.0`, tree SHA `1f58c48379bc9fcab68e8a37cb41aad3ad937419`.
- Distributed by committing the skill folder into a team repo's `.claude/skills/stacked-superpowers/`; a dev who wants it everywhere copies it to `~/.claude/skills/`. A README section in the skill folder says both.

## Non-goals

Merging stacks automatically; bundling or replacing Superpowers; agent tools other than Claude Code; parallel stacks for independent work (separate stacks, one at a time); a plugin/marketplace package (can wrap this later); a session-start hook.

## Anti-goals (failure even if it works)

- A layer PR that does not build and pass its tests on its own.
- A PR that mixes two concerns' code.
- Branches or commits created before the dev approved the layers.
- Running `gh auth login`, plugin installs, or global git config changes without asking.
- Interactive `gh stack` commands that hang the agent (bare `view`, `submit`, `init`, `add`, `checkout`, `switch`, `modify`).
- Requiring Superpowers for a quick change.

## Constraints

- Claude Code only. Bash script must run on macOS (bash 3.2, BSD tools) and Linux.
- With Superpowers' `using-git-worktrees`: one worktree per stack, never one per layer.
- Built and tested with `superpowers:writing-skills`: pressure scenarios run by subagents for each mode (Full on a tiered repo, Quick on a monolith, Split on a dirty branch, missing-prereq run) before it is called done.

## Approaches

| | Approach | Trade-off | Decision |
|---|---|---|---|
| A | Skill that calls the installed `gh-stack` skill by name | Always current upstream; devs must install two skills | Not chosen |
| **B** | **Self-contained skill bundling the gh-stack docs** | **One folder to share; bundled docs can drift, mitigated by the version WARN** | **Chosen by the user** |
| C | Plugin with a session-start hook | Auto-check every session, marketplace install; more moving parts | Not chosen; B can be wrapped later |

Decided in conversation: one PR per architectural layer when the repo has tiers; layers must also work for existing codebases without clean tiers; Superpowers optional.

## Settled after the builder check

- **Design and plan docs (Full mode).** `brainstorming` and `writing-plans` leave their docs uncommitted until the dev approves the layers. The stack's bottom layer is then `<topic>/00-design`, holding only the spec and plan, so reviewers see the intent first. If `using-git-worktrees` is used, the worktree is created after layer approval and `gh stack init <topic>/00-design` runs inside it; no other branch is created. This overrides Superpowers' default of committing docs on the current branch.
- **Tests live with their code.** Each layer carries its own unit tests (TDD as Superpowers does it), so each PR builds and passes on its own. Config or DI wiring that a layer needs to build goes in that layer. The top `tests and docs` / `infra-docs` layer holds only cross-layer integration/e2e tests, deployment files and docs, and is skipped when empty.
- **Per-layer check.** On each layer's branch, build and test the projects that layer touches (e.g. `dotnet test` on the affected projects, `ng test` for the UI). Before `submit`, run the full build and test suite once on the top branch.
- **"Unattended" means it never hangs or does anything unsafe**, not that it runs headless. The skill always stops for layer approval. Headless/CI use is a non-goal for now. Pressure-test subagents get the approval in their prompt.
- **Where it's built (user's answer).** In f-system-design at `.claude/skills/stacked-superpowers/`, committed so devs can copy it. The old `.claude/skills/gh-stack` is removed first, so only one skill responds to "stack".
- **Test repo (user's answer).** Pressure tests that push and submit use `fcroyal1/f-system-design` itself (currently no commits). If GitHub returns exit 9, stacked PRs must be enabled on it before the Finish test can pass. Test stacks use a `sandbox/` topic prefix and are closed and their branches deleted after each run.
- **Version compare.** The stale-docs warning compares the installed extension with `v0.3.0` (the tag the docs came from), not the skill metadata's `0.2.0`.

## Left to the builder

Exact SKILL.md wording and trigger description; script implementation details; how Split groups hunks within a file that spans two concerns; the pressure-test scenarios' exact prompts.

## Calls Claude made (check these)

- Skill name `stacked-superpowers`; branch naming `<topic>/<nn>-<concern>`.
- Default tier order `schema → domain → api → bff → ui → infra-docs`; concern order `prep refactor → core change → callers/UI → tests and docs`.
- Safe auto-fixes: gh-stack extension install, repo-local `rerere`, `remote.pushDefault=origin`.
- Split mode included as a recovery path, with a warning.
- PRs submitted as drafts; the dev merges.
- Shared by committing to each repo's `.claude/skills/`.
- The existing project-level `.claude/skills/gh-stack` in f-system-design becomes redundant; the check warns rather than deleting it.
