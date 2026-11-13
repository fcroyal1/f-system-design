# Split mode: turning finished work into a stack

Use when the code is already written: uncommitted changes, or a branch with commits not on trunk. Planning layers before coding is better (gh-stack can't reorder a stack non-interactively), so say once that next time the stack should come first, then do the split.

## 1. Read the change

```bash
git status --porcelain
git fetch origin
git diff --stat origin/<trunk>...HEAD     # committed work on this branch
git diff --stat                           # plus uncommitted work
```

Read the diff of each area. Note what depends on what (a migration the domain uses, an endpoint the UI calls).

## 2. Group by concern and propose

Group files into layers using `layering.md` (tiers if the repo has them, concerns if not). A file that serves two concerns is split by hunk with `git add -p` in its lower layer, the rest committed above. Send the layer list **with files under each layer** and wait for approval.

## 3. Build the stack

Keep the work safe first: if it's uncommitted, `git stash push -u -m split-backup` and `git stash apply` so a copy exists. If it's on a branch, note its tip SHA (`git rev-parse HEAD`) so it can be recovered.

Then, starting from trunk with the changes in the working tree:

```bash
git switch <trunk>                         # uncommitted changes come along
#   committed branch instead: bring its whole diff (incl. deletions/renames) over as uncommitted changes
#   git switch <trunk> && git diff --binary <trunk>...<branch-tip> | git apply
gh stack init <topic>/01-<concern>
git add <layer-1 paths> && git commit -m "..."
<build + test the projects layer 1 touches>
gh stack add <topic>/02-<concern>
git add <layer-2 paths> && git commit -m "..."
<build + test>
...
```

`gh stack add` without `-Am` leaves the working tree alone, so the remaining changes carry up to each new layer.

## 4. Each layer must stand alone

If a layer fails to build or test without a higher layer's files, a dependency was missed: move the needed file or hunk down (commit it on the lower layer, `gh stack rebase --upstack`) or merge the two layers. Do not submit a layer that only builds with the layer above it.

## 5. Finish

When the top layer's working tree is clean and `git diff <original-tip>` is empty (nothing lost), continue with Step 5 of SKILL.md. Drop the backup stash only after the PRs are up.
