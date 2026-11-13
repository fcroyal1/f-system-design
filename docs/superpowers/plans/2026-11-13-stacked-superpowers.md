# stacked-superpowers Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A self-contained Claude Code skill, `stacked-superpowers`, that checks its prerequisites, proposes layers for any repo, and ships work as a gh-stack of draft PRs, with or without Superpowers.

**Architecture:** One skill folder at `.claude/skills/stacked-superpowers/`: a short `SKILL.md` (the workflow), focused references (`layering.md`, `split.md`, bundled `gh-stack/` docs), and `scripts/check-prereqs.sh`. Tests live outside the folder in `tests/stacked-superpowers/` so the folder stays clean to copy: a plain-bash test runner with fake `git`/`gh` for the script, and subagent pressure scenarios for the skill text (superpowers:writing-skills). This plan is itself shipped as a stack (see **Stack**), dogfooding the skill's own rules.

**Tech Stack:** Bash 3.2 (macOS `/bin/bash`) and Linux bash; `gh` CLI with `github/gh-stack` v0.3.0; Git ≥ 2.36. No bats or shellcheck (not installed); tests are plain bash.

**Spec:** [docs/superpowers/specs/2026-11-13-stacked-superpowers-design.md](../specs/2026-11-13-stacked-superpowers-design.md)

## Global Constraints

- Skill name `stacked-superpowers`; folder `.claude/skills/stacked-superpowers/` in f-system-design, committed.
- Bundled docs come from `github/gh-stack` tag `v0.3.0`, tree SHA `1f58c48379bc9fcab68e8a37cb41aad3ad937419`. Version compare uses `v0.3.0`.
- Script output statuses exactly: `OK`, `FIXED`, `WARN`, `MISSING`. Exit non-zero only when something is MISSING.
- Never run `gh auth login`, plugin installs, or `git config --global`. Auto-fixes are only: `gh extension install github/gh-stack`, repo-local `rerere.enabled=true`, repo-local `remote.pushDefault=origin`.
- Never document or run bare interactive `gh stack` commands: `view`, `submit`, `init`, `add`, `checkout`, `switch`, `modify`.
- Branch naming `<topic>/<nn>-<concern>`; a repo's own convention overrides it.
- Default tier order `schema → domain → api → bff → ui → infra-docs`; concern order `prep-refactor → core → callers-ui → tests-docs`.
- Full mode: bottom layer `<topic>/00-design` holds only spec + plan; docs stay uncommitted until layers are approved.
- PRs submitted as drafts (`gh stack submit --auto`); the skill never merges unless asked.
- Superpowers is optional; Quick mode must work without it.
- Bash 3.2 compatible: no associative arrays, no `${var,,}`, no `mapfile`, BSD `sed`/`grep` flags only.
- Live tests on `fcroyal1/f-system-design` use topic prefix `sandbox/`, and close their PRs and delete their branches afterwards.

## Review Focus

1. **Repo with no commits on trunk** (this repo today). `gh stack init` needs a trunk commit on the remote; expected: `MISSING: trunk 'main' has no commits on origin` with a hint, not a gh-stack crash. Test: `test_empty_trunk_missing` (Task 2).
2. **Run from a subdirectory or outside a git repo.** Expected: checks resolve the repo root; outside a repo, `MISSING: not a git repository`, exit 1. Tests: `test_runs_from_subdir`, `test_not_a_repo_missing` (Task 2).
3. **Extension install fails** (offline, rate-limited). Expected: `MISSING` with the install command, not `FIXED`. Test: `test_ext_install_fails_missing` (Task 2).
4. **macOS bash 3.2.** Expected: the script and tests run under `/bin/bash` 3.2. Test: the runner itself is invoked with `/bin/bash` (Task 2, Step 4).
5. **A dev skips straight to coding** ("just make the change, I'll stack it later"). Expected: the skill still proposes layers and gets approval before the first commit, or offers Split mode afterwards. Test: pressure scenario `P5-skip-approval` (Task 3).

---

## Stack

| # | Branch | Layer | Tasks |
|---|---|---|---|
| 0 | `stacked-superpowers/00-design` | spec + plan | 0 |
| 1 | `stacked-superpowers/01-gh-stack-docs` | bundled gh-stack docs | 1 |
| 2 | `stacked-superpowers/02-prereqs` | prerequisite script + tests | 2 |
| 3 | `stacked-superpowers/03-skill` | SKILL.md, references, README, pressure tests | 3 |
| 4 | `stacked-superpowers/04-live-e2e` | live end-to-end test record | 4 |

## File Structure

```
.claude/skills/stacked-superpowers/
  SKILL.md                         workflow: prereqs → mode → layers → build → finish   (Task 3)
  README.md                        how devs install it and what it needs               (Task 3)
  references/
    layering.md                    tier detection, concern template, naming, plan Stack section (Task 3)
    split.md                       Split mode procedure                                (Task 3)
    gh-stack/
      GH-STACK.md                  upstream gh-stack SKILL.md body, frontmatter removed (Task 1)
      commands.md, stack-design.md, troubleshooting.md                                 (Task 1)
      UPSTREAM.md                  repo, tag, tree SHA, date copied                    (Task 1)
  scripts/check-prereqs.sh                                                             (Task 2)
tests/stacked-superpowers/
  run.sh                           runs every test_* function, prints PASS/FAIL, exit 1 on any FAIL (Task 2)
  check-prereqs.test.sh            test functions                                      (Task 2)
  fakes/git, fakes/gh              stubs driven by env vars, log calls to $FAKE_LOG    (Task 2)
  pressure/scenarios.md            P1–P6 prompts and pass criteria                     (Task 3)
  pressure/results.md              baseline (RED) and with-skill (GREEN) transcripts summary (Task 3)
  live-e2e.md                      record of the Task 4 run                             (Task 4)
Removed: .claude/skills/gh-stack/ (moved into references/gh-stack in Task 1)
```

---

### Task 0: Bootstrap trunk and the design layer

**Files:**
- Create: `README.md` (one paragraph: what f-system-design is)
- Commit on layer 0: `docs/superpowers/specs/2026-11-13-stacked-superpowers-design.md`, `docs/superpowers/plans/2026-11-13-stacked-superpowers.md`

- [ ] **Step 1: Ask the user** before committing to `main` and pushing it: the repo has no commits, and gh-stack needs a trunk commit on `origin`. Proceed only on yes.
- [ ] **Step 2: Trunk commit.** `git add README.md && git commit -m "chore: initial commit" && git push -u origin main`. Leave `PLAN.md`, `ARCHITECTURE.md` and `.claude/` uncommitted (they belong to the procurement work).
- [ ] **Step 3: Install and verify gh-stack config.** `git config rerere.enabled true`. Run `gh stack init stacked-superpowers/00-design`. Expected: exit 0. Exit 9 → stop and ask the user to enable stacked PRs on the repo.
- [ ] **Step 4: Commit the docs.** `git add docs/superpowers && git commit -m "docs: stacked-superpowers design and plan"`.

### Task 1: Bundle the gh-stack docs

**Files:**
- Move: `.claude/skills/gh-stack/references/{commands,stack-design,troubleshooting}.md` → `.claude/skills/stacked-superpowers/references/gh-stack/`
- Move + edit: `.claude/skills/gh-stack/SKILL.md` → `references/gh-stack/GH-STACK.md` with the YAML frontmatter removed (so it is never discovered as a second skill); fix its `references/…` links to sibling paths.
- Create: `references/gh-stack/UPSTREAM.md`
- Delete: `.claude/skills/gh-stack/`

**Interfaces:**
- Produces: `UPSTREAM.md` containing a line exactly `tag: v0.3.0` (Task 2 parses it with `sed -n 's/^tag: //p'`), plus `repo: github/gh-stack`, `tree-sha: 1f58c48379bc9fcab68e8a37cb41aad3ad937419`, `copied: 2026-11-13`.

- [ ] **Step 1:** `gh stack add stacked-superpowers/01-gh-stack-docs`.
- [ ] **Step 2: Move and edit** as listed.
- [ ] **Step 3: Verify.** `ls .claude/skills` → only `stacked-superpowers`. `grep -c '^---' references/gh-stack/GH-STACK.md` → `0`. `grep -o 'references/[a-z-]*\.md' references/gh-stack/GH-STACK.md` → no output.
- [ ] **Step 4: Commit.** `git add .claude/skills/stacked-superpowers/references/gh-stack && git commit -m "feat(stacked-superpowers): bundle gh-stack v0.3.0 docs"`.

### Task 2: Prerequisite check script

**Files:**
- Create: `.claude/skills/stacked-superpowers/scripts/check-prereqs.sh` (executable)
- Test: `tests/stacked-superpowers/{run.sh,check-prereqs.test.sh,fakes/git,fakes/gh}`

**Interfaces:**
- Consumes: `references/gh-stack/UPSTREAM.md` `tag:` line (Task 1).
- Produces: `check-prereqs.sh [--no-fix]`. Output one line per check: `<STATUS> <check-id>: <detail>`, where STATUS ∈ `OK|FIXED|WARN|MISSING`. For MISSING, `<detail>` ends with `run: <command>` when a command fixes it. Exit 0 unless any MISSING. Check ids, in order: `repo`, `git`, `gh`, `gh-auth`, `gh-stack`, `gh-stack-version`, `rerere`, `push-default`, `github-remote`, `trunk`, `duplicate-skill`. Task 3's SKILL.md relies on these ids. After a failed `repo` or `gh` check, later checks that need them are skipped.
- Fakes: `fakes/git` passes everything to the real git except `--version` (prints `git version $FAKE_GIT_VERSION`, default `2.50.1`). `fakes/gh` responds to `--version`, `auth status` (`$FAKE_GH_AUTH=fail` → exit 1), `extension list` (prints `gh stack\tgithub/gh-stack\t$FAKE_GH_EXT_VERSION` unless empty), `extension install` (`$FAKE_GH_INSTALL=fail` → exit 1); every call is appended to `$FAKE_LOG`. Tests build a temp repo with a temp `HOME` and a bare repo as `origin` whose URL is rewritten to contain `github.com` via `url.<bare>.insteadOf`.

Trunk check: `git ls-remote --heads origin <trunk>` non-empty, trunk = `origin/HEAD` target or `main`. Duplicate-skill check: a `gh-stack/SKILL.md` under `<repo>/.claude/skills/` or `$HOME/.claude/skills/`.

- [ ] **Step 1:** `gh stack add stacked-superpowers/02-prereqs`.
- [ ] **Step 2: Write the failing tests** in `check-prereqs.test.sh`. Each sets up a repo, runs the script with `PATH=fakes:$PATH`, and asserts on output lines, exit code and `$FAKE_LOG`:

```bash
test_all_ok_exits_zero              # every line starts OK; exit 0
test_not_a_repo_missing             # run in a temp non-repo dir → "MISSING repo:"; exit 1
test_runs_from_subdir               # run from repo/src/deep → same result as from root
test_old_git_missing                # FAKE_GIT_VERSION=2.30.0 → "MISSING git:"; exit 1
test_gh_absent_missing              # PATH without gh → "MISSING gh:"; gh-auth/gh-stack skipped
test_gh_unauth_never_logs_in        # FAKE_GH_AUTH=fail → "MISSING gh-auth: ... run: gh auth login"; FAKE_LOG has no "auth login"
test_ext_absent_installs            # FAKE_GH_EXT_VERSION= → "FIXED gh-stack:"; FAKE_LOG has "extension install github/gh-stack"
test_ext_install_fails_missing      # plus FAKE_GH_INSTALL=fail → "MISSING gh-stack: ... run: gh extension install github/gh-stack"
test_ext_newer_warns                # FAKE_GH_EXT_VERSION=v0.4.0 → "WARN gh-stack-version:"; exit 0
test_rerere_set_locally             # → "FIXED rerere:"; repo config true; $HOME/.gitconfig absent
test_two_remotes_sets_push_default  # remotes origin+upstream → "FIXED push-default:"; config = origin
test_two_remotes_no_origin_missing  # remotes a+b → "MISSING push-default:"
test_no_github_remote_missing       # origin URL without github.com → "MISSING github-remote:"
test_empty_trunk_missing            # origin has no main → "MISSING trunk: trunk 'main' has no commits on origin"
test_duplicate_skill_warns          # repo/.claude/skills/gh-stack/SKILL.md exists → "WARN duplicate-skill:"
test_no_fix_changes_nothing         # --no-fix with ext absent, rerere unset → both MISSING; no install in FAKE_LOG; config unchanged
```

- [ ] **Step 3: Run and confirm they fail.** `/bin/bash tests/stacked-superpowers/run.sh`. Expected: every test FAIL (script missing), exit 1.
- [ ] **Step 4: Implement `check-prereqs.sh`** to the interface above. Resolve the repo root with `git rev-parse --show-toplevel`; locate `UPSTREAM.md` relative to the script (`$(dirname "$0")/../references/gh-stack/UPSTREAM.md`). Compare versions numerically per dot-part (no `sort -V`; BSD sort lacks it).
- [ ] **Step 5: Run and confirm they pass.** `/bin/bash tests/stacked-superpowers/run.sh`. Expected: 16 PASS, exit 0. Also run on the real repo: `.claude/skills/stacked-superpowers/scripts/check-prereqs.sh --no-fix` → every line OK or WARN.
- [ ] **Step 6: Commit.** `git add .claude/skills/stacked-superpowers/scripts tests/stacked-superpowers/run.sh tests/stacked-superpowers/check-prereqs.test.sh tests/stacked-superpowers/fakes && git commit -m "feat(stacked-superpowers): prerequisite check script"`.

### Task 3: SKILL.md, references and pressure tests

Follow **superpowers:writing-skills** throughout (RED baseline before writing, GREEN with the skill, REFACTOR loopholes).

**Files:**
- Create: `.claude/skills/stacked-superpowers/{SKILL.md,README.md,references/layering.md,references/split.md}`
- Test: `tests/stacked-superpowers/pressure/{scenarios.md,results.md}`

**Interfaces:**
- Consumes: `scripts/check-prereqs.sh` and its check ids (Task 2); `references/gh-stack/GH-STACK.md` and siblings (Task 1).
- Produces: frontmatter `name: stacked-superpowers`; a `description` starting "Use when" that triggers on: shipping work as stacked PRs, `gh stack`, splitting a large branch or uncommitted work into reviewable PRs, Superpowers + stacked PRs. Keep SKILL.md under 300 lines; detail goes in references.

**What SKILL.md must say** (the implementer writes the prose):
1. Run `scripts/check-prereqs.sh` first; on MISSING, show the `run:` commands and stop. Then check its own skill list for `superpowers:*`; if absent, recommend `/plugin install superpowers@claude-plugins-official` in one line and continue in Quick mode.
2. Pick the mode (Full / Quick / Split) per the spec's table; offer Split when the tree is dirty or the branch has unpushed commits; the dev can override in one word.
3. Propose layers using `references/layering.md`; **no branch or commit before the dev approves the layer list**. Too small for two layers → suggest one ordinary PR.
4. Full mode: docs uncommitted until approval; bottom layer `<topic>/00-design`; worktree (if any) created after approval; plan has a layer tag per task and a Stack section in the format of this plan's **Stack** table.
5. Build loop per layer: `gh stack init`/`add <branch>`; stage explicit paths; tests in the same layer; per-layer build + tests on touched projects; Full mode uses subagent-driven-development / executing-plans, requesting-code-review, verification-before-completion scoped to the layer; lower-layer fixes via `gh stack down`/`checkout <branch>`, commit, `gh stack rebase --upstack`, `gh stack top`.
6. Finish: full build + tests on top branch, `gh stack submit --auto`, `gh stack view --json`, report PR URLs. Never merge unless asked. Exit 9 → tell the user to enable stacked PRs.
7. The non-interactive command table from `GH-STACK.md` (inline the "Always run / Never run bare" table; link the rest).

`layering.md` covers: tier signals (solution/csproj names, `angular.json`, `package.json` workspaces, `db/`/`migrations/` folders, `src/*` project names), the two default orders, the too-small rule, naming, and the Stack section format. `split.md` covers: diff against trunk, group by concern, propose, then `gh stack init` + explicit-path commits per layer, using `git add -p` only for files that span two concerns; verify each layer builds and tests; warn once that planning first is preferred. README covers: what it does, prerequisites, install (commit to a repo's `.claude/skills/` or copy to `~/.claude/skills/`), and the three modes in one line each.

- [ ] **Step 1:** `gh stack add stacked-superpowers/03-skill`.
- [ ] **Step 2: Write `pressure/scenarios.md`**, each with prompt, repo setup and pass criteria:
  - `P1-full-tiered`: Superpowers present, repo with `db/`, `src/*.Api`, `bff/`, `web/angular.json`; "add a PO approval feature" → proposes `00-design` + tier layers, waits for approval.
  - `P2-quick-monolith`: no Superpowers, single-project repo; "rename a column and update its callers" → concern layers or one-PR suggestion; no brainstorming invoked.
  - `P3-split-dirty`: 12 uncommitted files across schema/api/ui → offers Split, proposes groups, waits.
  - `P4-missing-prereq`: `gh` unauthenticated → stops with `gh auth login` hint, never runs it.
  - `P5-skip-approval`: "just make the change, I'll stack it later" → still proposes layers before the first commit, or offers Split after.
  - `P6-interactive-trap`: "show me the stack" → uses `gh stack view --json`, never bare `view`.
- [ ] **Step 3: RED.** Run each scenario with a fresh subagent **without** the skill (approval given in the prompt where the scenario needs it). Record violations and rationalizations verbatim in `results.md`. Expected: at least P1, P3, P5, P6 violate.
- [ ] **Step 4: Write** `SKILL.md`, `layering.md`, `split.md`, `README.md` addressing the recorded violations.
- [ ] **Step 5: GREEN.** Rerun all six with the skill. Expected: all pass their criteria. Record in `results.md`.
- [ ] **Step 6: REFACTOR.** For any new rationalization, tighten the text and rerun that scenario until it passes.
- [ ] **Step 7: Commit.** `git add .claude/skills/stacked-superpowers/{SKILL.md,README.md,references/layering.md,references/split.md} tests/stacked-superpowers/pressure && git commit -m "feat(stacked-superpowers): skill workflow, layering, split mode, pressure tests"`.

### Task 4: Live end-to-end run

**Files:**
- Create: `tests/stacked-superpowers/live-e2e.md`

- [ ] **Step 1:** `gh stack add stacked-superpowers/04-live-e2e`.
- [ ] **Step 2: Run Quick mode for real** with a fresh subagent using the skill on `fcroyal1/f-system-design`, from a temporary clone, topic `sandbox/e2e`: a 2-layer toy change (a text file, then a file referencing it). Approval given in the prompt. Expected: `gh stack submit --auto` exits 0; `gh stack view --json` shows 2 branches each with `pr.state == "OPEN"`, drafts.
- [ ] **Step 3: Clean up.** Close both PRs with `gh pr close <n> --delete-branch`; `gh stack unstack --local` in the temp clone; delete the clone. Verify `git ls-remote --heads origin 'sandbox/*'` is empty.
- [ ] **Step 4: Record** commands, exit codes, PR numbers and cleanup result in `live-e2e.md`.
- [ ] **Step 5: Commit.** `git add tests/stacked-superpowers/live-e2e.md && git commit -m "test(stacked-superpowers): live end-to-end run"`.

### Finish

- [ ] Full test run on the top branch: `/bin/bash tests/stacked-superpowers/run.sh` → all PASS.
- [ ] `gh stack submit --auto`, then `gh stack view --json`; report the five draft PR URLs to the user. Do not merge.
