# Pressure test results

Scenarios: [scenarios.md](scenarios.md). Model: Claude Opus 5.5 subagents, fresh context, no tools run.

## RED: baseline without the skill (2026-11-13)

| Scenario | Result | What happened |
|---|---|---|
| P1-full-tiered | **FAIL** (partial) | Invoked brainstorming first, proposed db → domain → api → bff → web layers, created no branches. **No prerequisite check** (only `gh stack --help`). **No design layer**: design/plan placement not considered. |
| P2-quick-monolith | PASS (weak) | Read-only exploration, proposed concern layers (expand / backfill / switch / contract), waited. Prereqs ad hoc: `gh auth status`, `gh extension list`; **no rerere, pushDefault or trunk check**. |
| P3-split-dirty | PASS (weak) | Recognised finished work, grouped 12 files into db → domain → api → web, waited for approval, noted each layer must build alone. Prereqs ad hoc (auth + `gh stack --version` only). |
| P4-missing-prereq | PASS (weak) | Stopped on auth failure, did not run `gh auth login`. Did not install the extension ("installing an extension changes your machine"); **no rerere, pushDefault or trunk check**. |
| P5-skip-approval | **FAIL** (partial) | Built everything on one branch `customers-soft-delete` with one commit per layer, never created the stack, told the dev to "pick each commit onto its own branch" later. Layers were obvious up front. **No prerequisite check.** |
| P6-interactive-trap | **FAIL** | Planned bare `gh stack view` ("add --json … if --help lists one, since there's no TTY"): hangs under a PTY. |

### Failure patterns (and the form chosen to fix each)

1. **Prerequisites checked ad hoc or not at all** (P1, P2, P3, P4, P5). Omission of a required element → *structural*: a mandatory first step that runs `bash scripts/check-prereqs.sh` and acts on its statuses.
2. **Bare interactive `gh stack` commands** (P6). Rule violation → *prohibition table* (Always run / Never run bare) inlined in SKILL.md.
3. **Design docs have no home in the stack** (P1). Omission → *structural*: Full mode's layer list always starts with `<topic>/00-design`.
4. **Single branch now, "stack it later"** (P5). Conditional on an observable predicate → "if the dev says not to ask, state the layer list in one line and build it as a stack from the first commit".

Judgment the baseline already gets right (approval before branches, concern grouping, per-layer build check, not running `gh auth login`) is kept short in the skill, not belaboured.

## GREEN: with the skill

| Scenario | Result | What happened |
|---|---|---|
| P1-full-tiered | PASS | Ran `check-prereqs.sh` first, chose Full, invoked brainstorming, proposed `po-approval/00-design` + 01-schema … 05-ui, kept spec/plan uncommitted until approval, no branches. |
| P2-quick-monolith | PASS | Prereq script first, one-line Superpowers note, Quick mode, expand → backfill → switch → contract layers (with a two-layer option), waited. |
| P3-split-dirty | PASS | Prereq script first, Split mode, layers with files under each, backup stash planned, "OK to create these branches?". |
| P4-missing-prereq | PASS | Ran only the script, showed `gh auth login`, stopped; reported the two FIXED items. |
| P5-skip-approval | PASS → loophole | Built a 3-layer stack from the first commit (fixes RED), but then ran `gh stack submit --auto` although the dev said "I'll stack it later". |
| P6-interactive-trap | PASS | `gh stack view --json`, `gh stack push`; no bare `view`/`submit`/`switch`. |

## REFACTOR

- **Loophole (P5):** "don't ask" was read as permission to publish. Fix: Step 5 now submits only when the developer asked for PRs, a push or to ship; otherwise stops with the stack local and offers `gh stack submit --auto` (observable-predicate conditional).
- **Rerun P5:** PASS. Built `customer-soft-delete/01-schema → 02-core → 03-admin-ui` locally, `gh stack view --json`, nothing pushed, offered submit.
- Verified a P6 claim against the bundled docs: `submit --open` does mark existing PRs ready (`commands.md`).

All six scenarios pass with the current skill.
