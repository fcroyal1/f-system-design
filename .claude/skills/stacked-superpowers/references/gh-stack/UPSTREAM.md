# Upstream source

These files are a bundled copy of GitHub's gh-stack agent skill, so `stacked-superpowers` needs no other skill installed.

repo: github/gh-stack
path: skills/gh-stack
tag: v0.3.0
tree-sha: 1f58c48379bc9fcab68e8a37cb41aad3ad937419
copied: 2026-11-13

Changes from upstream: `SKILL.md` renamed to `GH-STACK.md` with its YAML frontmatter removed (so it is never discovered as a second skill), and its `references/…` links pointed at sibling files. The other three files are unchanged.

To refresh: copy `skills/gh-stack/` from a newer tag, repeat the changes above, and update `tag` and `tree-sha`. `scripts/check-prereqs.sh` warns when the installed extension is newer than `tag`.
