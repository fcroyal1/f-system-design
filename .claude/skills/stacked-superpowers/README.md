# stacked-superpowers

A Claude Code skill for shipping work as **stacked pull requests**, one reviewable PR per layer, using GitHub's `gh stack`. It works in new projects with clean tiers (database, domain, API, BFF, UI) and in existing codebases without them. If the [Superpowers](https://github.com/obra/superpowers) plugin is installed, it runs Superpowers' brainstorm → plan → build → review flow and ships the result as a stack.

## Modes

- **Full**: Superpowers installed, feature-sized work. Design and plan first; the stack starts with a `00-design` layer.
- **Quick**: no Superpowers, or a small change. Describe it, approve the layers, build.
- **Split**: the work is already written. The skill groups it by concern and turns it into a stack.

In every mode the developer approves the layer list before any branch is created.

## Prerequisites

| Need | Install |
|---|---|
| Git 2.36+ | `brew install git` (or your OS package manager) |
| GitHub CLI, logged in | `brew install gh`, then `gh auth login` |
| gh-stack extension | `gh extension install github/gh-stack` (the skill installs it if missing) |
| Stacked PRs enabled on the GitHub repo | Repository settings; the skill tells you if they're off |
| Superpowers (optional) | In Claude Code: `/plugin install superpowers@claude-plugins-official` |

Check everything at once, from inside your repo:

```bash
bash .claude/skills/stacked-superpowers/scripts/check-prereqs.sh            # fixes the safe items
bash .claude/skills/stacked-superpowers/scripts/check-prereqs.sh --no-fix   # report only
```

## Install

- **For a team repo:** copy this folder to `<repo>/.claude/skills/stacked-superpowers/` and commit it. Everyone who opens the repo in Claude Code gets it.
- **For yourself, everywhere:** copy it to `~/.claude/skills/stacked-superpowers/`.

Remove any separately installed `gh-stack` skill (`.claude/skills/gh-stack` or `~/.claude/skills/gh-stack`): this skill bundles it, and two skills answering to "stack" confuse Claude. The prerequisite check warns if it finds one.

## Use

Ask Claude in plain words, for example:

- "Build the PO approval feature and ship it as stacked PRs."
- "Rename this column and update its callers, stacked."
- "I've finished this feature. Open it as a stack."

## Bundled gh-stack docs

`references/gh-stack/` is a copy of GitHub's gh-stack agent skill at `v0.3.0` (see `UPSTREAM.md`). The check warns when your installed extension is newer than the bundled docs.
