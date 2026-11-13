#!/bin/bash
# Checks what stacked-superpowers needs, fixing the safe items.
#
# Usage: check-prereqs.sh [--no-fix]
#
# Prints one line per check:  <STATUS> <check-id>: <detail>
#   STATUS is OK, FIXED, WARN or MISSING. A MISSING line ends with "run: <command>"
#   when one command fixes it.
# Exits 1 if anything is MISSING, else 0.
#
# Auto-fixes (skipped with --no-fix): installing the gh-stack extension,
# repo-local rerere.enabled=true, repo-local remote.pushDefault=origin.
# Never runs `gh auth login`, installs plugins, or writes global git config.
#
# Compatible with macOS /bin/bash 3.2 and BSD tools.

FIX=1
[ "$1" = "--no-fix" ] && FIX=0

MIN_GIT="2.36"
UPSTREAM="$(cd "$(dirname "$0")/.." && pwd)/references/gh-stack/UPSTREAM.md"
BUNDLED_TAG="$(sed -n 's/^tag: //p' "$UPSTREAM" 2>/dev/null)"
missing=0

say() {  # say STATUS id detail
  echo "$1 $2: $3"
  [ "$1" = MISSING ] && missing=1
}

# ver_ge A B: true if version A >= B (numeric, dot-separated, leading "v" ignored).
ver_ge() {
  local a="${1#v}" b="${2#v}" x y
  while [ -n "$a" ] || [ -n "$b" ]; do
    x="${a%%.*}"; y="${b%%.*}"
    x="${x:-0}"; y="${y:-0}"
    [ "$x" -gt "$y" ] 2>/dev/null && return 0
    [ "$x" -lt "$y" ] 2>/dev/null && return 1
    case "$a" in *.*) a="${a#*.}" ;; *) a="" ;; esac
    case "$b" in *.*) b="${b#*.}" ;; *) b="" ;; esac
  done
  return 0
}

# --- repo --------------------------------------------------------------------
ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
if [ -n "$ROOT" ]; then
  say OK repo "$ROOT"
  cd "$ROOT" || exit 1
else
  say MISSING repo "not a git repository; run this from inside the repo you want to stack"
fi

# --- git ---------------------------------------------------------------------
gv="$(git --version 2>/dev/null | awk '{print $3}')"
if [ -z "$gv" ]; then
  say MISSING git "git not found; install Git $MIN_GIT or newer"
elif ver_ge "$gv" "$MIN_GIT"; then
  say OK git "git $gv"
else
  say MISSING git "git $gv is older than $MIN_GIT; upgrade Git (e.g. brew install git)"
fi

# --- gh, auth, extension -----------------------------------------------------
if ! command -v gh >/dev/null 2>&1; then
  say MISSING gh "GitHub CLI not found; install it from https://cli.github.com (e.g. brew install gh)"
else
  say OK gh "$(gh --version 2>/dev/null | head -1)"

  if gh auth status >/dev/null 2>&1; then
    say OK gh-auth "logged in"
  else
    say MISSING gh-auth "not logged in to GitHub; run: gh auth login"
  fi

  ext_version() { gh extension list 2>/dev/null | awk -F'\t' '$2=="github/gh-stack"{print $3}'; }
  ev="$(ext_version)"
  if [ -n "$ev" ]; then
    say OK gh-stack "github/gh-stack $ev"
  elif [ "$FIX" = 0 ]; then
    say MISSING gh-stack "extension not installed; run: gh extension install github/gh-stack"
  elif gh extension install github/gh-stack >/dev/null 2>&1; then
    ev="$(ext_version)"
    say FIXED gh-stack "installed github/gh-stack ${ev:-}"
  else
    say MISSING gh-stack "install failed; run: gh extension install github/gh-stack"
  fi

  if [ -n "$ev" ] && [ -n "$BUNDLED_TAG" ]; then
    if [ "${ev#v}" != "${BUNDLED_TAG#v}" ] && ver_ge "$ev" "$BUNDLED_TAG"; then
      say WARN gh-stack-version "installed $ev is newer than the bundled docs ($BUNDLED_TAG); they may be stale, so check \`gh stack <command> --help\`"
    else
      say OK gh-stack-version "installed $ev, bundled docs $BUNDLED_TAG"
    fi
  fi
fi

# --- repo configuration ------------------------------------------------------
if [ -n "$ROOT" ]; then
  if [ "$(git config --local --get rerere.enabled)" = true ]; then
    say OK rerere "rerere.enabled=true"
  elif [ "$FIX" = 0 ]; then
    say MISSING rerere "conflict reuse is off; run: git config rerere.enabled true"
  else
    git config --local rerere.enabled true && say FIXED rerere "set rerere.enabled=true for this repo"
  fi

  remotes="$(git remote)"
  nremotes="$(printf '%s\n' "$remotes" | grep -c .)"
  pd="$(git config --get remote.pushDefault)"
  if [ "$nremotes" -le 1 ] || [ -n "$pd" ]; then
    say OK push-default "${pd:-single remote}"
  elif ! printf '%s\n' "$remotes" | grep -qx origin; then
    say MISSING push-default "several remotes and none is origin; run: git config remote.pushDefault <remote>"
  elif [ "$FIX" = 0 ]; then
    say MISSING push-default "several remotes; run: git config remote.pushDefault origin"
  else
    git config --local remote.pushDefault origin && say FIXED push-default "set remote.pushDefault=origin for this repo"
  fi

  # Raw configured URLs, not insteadOf-rewritten ones.
  gh_remote="$(git config --get-regexp '^remote\..*\.url$' | awk '$2 ~ /github\.com[:\/]/ {split($1,p,"."); print p[2]; exit}')"
  if [ -n "$gh_remote" ]; then
    say OK github-remote "$gh_remote -> $(git config --get "remote.$gh_remote.url")"
  else
    say MISSING github-remote "no remote points at github.com; run: git remote add origin https://github.com/<owner>/<repo>.git"
  fi

  trunk_remote="${pd:-origin}"
  if [ -n "$gh_remote" ] && ! printf '%s\n' "$remotes" | grep -qx "$trunk_remote"; then
    trunk_remote="$gh_remote"
  fi
  trunk="$(git symbolic-ref -q --short "refs/remotes/$trunk_remote/HEAD" 2>/dev/null)"
  trunk="${trunk#"$trunk_remote"/}"
  trunk="${trunk:-main}"
  if printf '%s\n' "$remotes" | grep -qx "$trunk_remote" \
     && [ -n "$(git ls-remote --heads "$trunk_remote" "$trunk" 2>/dev/null)" ]; then
    say OK trunk "'$trunk' exists on $trunk_remote"
  else
    say MISSING trunk "trunk '$trunk' has no commits on $trunk_remote; commit and run: git push -u $trunk_remote $trunk"
  fi
fi

# --- duplicate gh-stack skill ------------------------------------------------
dups=""
for d in ${ROOT:+"$ROOT/.claude/skills/gh-stack"} "$HOME/.claude/skills/gh-stack"; do
  [ -f "$d/SKILL.md" ] && dups="$dups $d"
done
if [ -n "$dups" ]; then
  say WARN duplicate-skill "another gh-stack skill is installed at:$dups. stacked-superpowers bundles it; remove the other copy so only one skill handles \"stack\""
else
  say OK duplicate-skill "no other gh-stack skill"
fi

exit "$missing"
