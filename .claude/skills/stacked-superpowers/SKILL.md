---
name: stacked-superpowers
description: Use when work should ship as stacked pull requests (gh stack, gh-stack, dependent or layered PRs, one PR per layer), when splitting a large branch or uncommitted changes into reviewable PRs, when a stack is checked out, or when combining Superpowers planning with stacked PRs, in greenfield or existing codebases.
---

# Stacked Superpowers

Ship work as a **stack** of pull requests with `gh stack`: an ordered chain of branches on trunk, one PR per layer, each reviewable on its own. Works with or without the Superpowers plugin, in new or existing repos.

**Core rule:** decide the layers first, get them approved, then write each layer's code on that layer's branch. Every layer PR builds and passes its tests on its own.

## Step 1: Check prerequisites (always first)

Run this before reading code, brainstorming or branching:

```bash
bash <this-skill-dir>/scripts/check-prereqs.sh
```

| Line | Do |
|---|---|
| `OK` / `FIXED` | Nothing. (FIXED = the script installed the gh-stack extension or set repo-local git config.) |
| `WARN` | Mention it in one line and carry on. |
| `MISSING` | Show the developer the `run:` command from that line and **stop**. Never run `gh auth login` yourself. |

Then look at your own skill list. If no `superpowers:*` skills appear, say once: "Superpowers isn't installed (optional): `/plugin install superpowers@claude-plugins-official`" and use Quick or Split mode.

If any `gh stack` command exits **9**, stacked PRs are not enabled on the repository: tell the developer and stop.

## Step 2: Pick the mode

| Mode | When | First move |
|---|---|---|
| **Split** | The tree is dirty or the branch has commits not on trunk, and the developer says the work is done | Read `references/split.md` |
| **Full** | Superpowers is installed and the work is a feature or larger | `superpowers:brainstorming`, then Step 3 |
| **Quick** | Superpowers absent, or a small change | Read the code it touches, then Step 3 |

Say which mode you picked in one line; the developer can override it in one word.

## Step 3: Propose the layers, get approval

Read `references/layering.md`, then send the layer list: branch name, one-sentence concern, and (Split) the files, bottom to top.

- **No branch, no commit, no `gh stack init` until the developer approves the list.**
- **Full mode:** the bottom layer is always `<topic>/00-design`, holding only the spec and plan. Keep those docs uncommitted until approval, then commit them as the first layer. If you use a worktree, create it after approval: one worktree for the whole stack, never one per layer. The plan tags every task with one layer and ends with a **Stack** table (format in `layering.md`).
- **Developer said not to ask** ("just make the change", "I'll stack it later"): their instruction is the approval. State the layer list in one line and build it as a stack from the first commit. Do not build on one branch to split later.
- **Too small for two layers:** say so and make one ordinary PR instead.

## Step 4: Build layer by layer

```bash
gh stack init <topic>/01-<concern>        # bottom layer (or 00-design in Full mode)
git add <explicit paths> && git commit -m "..."
gh stack add <topic>/02-<concern>         # next layer, branched from the current one
```

- Stage explicit paths. Never `git add -A` or `git commit -a`.
- Each layer carries its own tests. Before moving up, build and test the projects that layer touches.
- Full mode: run the layer's tasks with `superpowers:subagent-driven-development` or `superpowers:executing-plans`, then `superpowers:requesting-code-review` and `superpowers:verification-before-completion` on that layer's diff.
- A fix that belongs lower down: `gh stack down` (or `gh stack checkout <branch>`), commit there, `gh stack rebase --upstack`, `gh stack top`.

## Step 5: Finish

Submitting pushes branches and opens PRs on GitHub. Do it only if the developer asked for PRs, a push, or to ship ("open PRs", "ship it as stacked PRs", "push"). Otherwise ("just make the change", "I'll stack it later") stop with the stack built locally, report the branches, and offer `gh stack submit --auto`.

```bash
# full build + tests on the top branch first
gh stack submit --auto      # pushes every branch, opens draft PRs
gh stack view --json        # report each branch's PR URL
```

Later pushes: `gh stack push`. Full mode: this replaces the merge/PR step of `superpowers:finishing-a-development-branch`. Never merge unless asked; then `gh stack merge <pr> --yes`.

## gh stack without a TTY

Agent shells may get a PTY, where these open prompts or a TUI and hang forever. Use the left column only.

| Always run | Never run bare |
|---|---|
| `gh stack view --json` (or `--short`) | `gh stack view` |
| `gh stack submit --auto` | `gh stack submit` |
| `gh stack init <branch>...` | `gh stack init` |
| `gh stack add <branch>` | `gh stack add` |
| `gh stack checkout <target>` | `gh stack checkout` |
| `gh stack up` / `down` / `top` / `bottom` | `gh stack switch` |
| `gh stack merge <target> --yes` | `gh pr merge` |
| — | `gh stack modify` (TUI only) |

Do not run `--help` first "to check": the table is authoritative for v0.3.0. With several remotes and no `remote.pushDefault`, pass `--remote <name>` to `push`, `submit`, `sync`, `rebase` and `link`.

## Reference

- `references/layering.md`: choosing layers in tiered and untiered repos, naming, the Stack table.
- `references/split.md`: turning finished work into a stack.
- `references/gh-stack/GH-STACK.md`: the full gh-stack guide (exit codes, sync, merging). Its siblings `commands.md`, `stack-design.md` and `troubleshooting.md` cover command details, layer design and recovery (rebase conflicts, diverged stacks, squash merges).

## Common mistakes

| Mistake | Fix |
|---|---|
| Checking `gh auth status` by hand and skipping the rest | Run `check-prereqs.sh`; it also checks rerere, pushDefault and that trunk exists on the remote. |
| Building on one branch with "one commit per layer" to stack later | Create the stack first; each layer gets its own branch. |
| Design docs committed on trunk or the first code layer | Full mode: `<topic>/00-design`. |
| Tests in a final "tests" layer | Tests go with their code; the top layer holds only cross-layer integration tests and docs. |
| Bare `gh stack view` "just to look" | `gh stack view --json`. |
