# Tests for .claude/skills/stacked-superpowers/scripts/check-prereqs.sh
# Sourced by run.sh. Each test_* runs in its own subshell; return non-zero to fail.

TEST_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$TEST_DIR/../../.claude/skills/stacked-superpowers/scripts/check-prereqs.sh"
FAKES="$TEST_DIR/fakes"
REAL_GIT="$(command -v git)"
export REAL_GIT

# --- helpers -----------------------------------------------------------------

# Builds an isolated world: temp HOME, a bin dir with fake git + gh, a bare
# "GitHub" origin with main pushed, and a work repo at $T/repo.
setup() {
  T="$(mktemp -d)"
  export HOME="$T/home" FAKE_LOG="$T/log" FAKE_STATE="$T/state" GIT_CONFIG_NOSYSTEM=1
  mkdir -p "$HOME" "$T/bin" "$FAKE_STATE"
  : > "$FAKE_LOG"
  ln -s "$FAKES/git" "$T/bin/git"
  ln -s "$FAKES/gh" "$T/bin/gh"
  "$REAL_GIT" init -q --bare "$T/origin.git"
  "$REAL_GIT" init -q -b main "$T/repo"
  g config user.email t@example.com
  g config user.name Test
  g config url."$T/origin.git".insteadOf https://github.com/test/repo.git
  g remote add origin https://github.com/test/repo.git
  echo hi > "$T/repo/README.md"
  g add README.md
  g commit -q -m init
  g push -q origin main 2>/dev/null
}

# git in the work repo, bypassing the fake.
g() { "$REAL_GIT" -C "$T/repo" "$@"; }

# Runs the script from $1 (default: the work repo) with remaining args.
# Sets OUT and CODE.
run_check() {
  local dir="${1:-$T/repo}"; shift || true
  OUT="$(cd "$dir" && PATH="$T/bin:/usr/bin:/bin" /bin/bash "$SCRIPT" "$@" 2>&1)"
  CODE=$?
}

has()     { echo "$OUT" | grep -qF -- "$1" || { echo "expected line containing: $1"; echo "$OUT"; return 1; }; }
hasnt()   { ! echo "$OUT" | grep -qF -- "$1" || { echo "unexpected: $1"; echo "$OUT"; return 1; }; }
code_is() { [ "$CODE" = "$1" ] || { echo "expected exit $1, got $CODE"; echo "$OUT"; return 1; }; }
logged()  { grep -qF -- "$1" "$FAKE_LOG" || { echo "expected gh call: $1"; cat "$FAKE_LOG"; return 1; }; }
not_logged() { ! grep -qF -- "$1" "$FAKE_LOG" || { echo "unexpected gh call: $1"; return 1; }; }

# --- tests -------------------------------------------------------------------

test_all_ok_exits_zero() {
  setup; run_check
  code_is 0 || return 1
  has "OK repo:" && has "OK git:" && has "OK gh:" && has "OK gh-auth:" && has "OK gh-stack:" \
    && has "OK gh-stack-version:" && has "FIXED rerere:" && has "OK push-default:" \
    && has "OK github-remote:" && has "OK trunk:" && has "OK duplicate-skill:" || return 1
  run_check
  [ "$(echo "$OUT" | grep -cv '^OK ')" = 0 ] || { echo "second run not all OK"; echo "$OUT"; return 1; }
}

test_not_a_repo_missing() {
  setup; mkdir "$T/plain"; run_check "$T/plain"
  code_is 1 && has "MISSING repo: not a git repository" && hasnt "trunk:"
}

test_runs_from_subdir() {
  setup; mkdir -p "$T/repo/src/deep"; run_check "$T/repo/src/deep"
  code_is 0 && has "OK repo:" && has "OK trunk:"
}

test_old_git_missing() {
  setup; FAKE_GIT_VERSION=2.30.0 run_check
  code_is 1 && has "MISSING git: git 2.30.0 is older than 2.36"
}

test_gh_absent_missing() {
  setup; rm "$T/bin/gh"; run_check
  code_is 1 && has "MISSING gh:" && hasnt "gh-auth:" && hasnt "gh-stack:"
}

test_gh_unauth_never_logs_in() {
  setup; FAKE_GH_AUTH=fail run_check
  code_is 1 && has "MISSING gh-auth:" && has "run: gh auth login" && not_logged "auth login"
}

test_ext_absent_installs() {
  setup; FAKE_GH_EXT_VERSION= run_check
  code_is 0 && has "FIXED gh-stack:" && logged "extension install github/gh-stack"
}

test_ext_install_fails_missing() {
  setup; FAKE_GH_EXT_VERSION= FAKE_GH_INSTALL=fail run_check
  code_is 1 && has "MISSING gh-stack:" && has "run: gh extension install github/gh-stack"
}

test_ext_newer_warns() {
  setup; FAKE_GH_EXT_VERSION=v0.4.0 run_check
  code_is 0 && has "WARN gh-stack-version:" && has "v0.4.0" && has "v0.3.0"
}

test_rerere_set_locally() {
  setup; run_check
  has "FIXED rerere:" || return 1
  [ "$(g config --local --get rerere.enabled)" = true ] || { echo "rerere not set locally"; return 1; }
  [ ! -e "$HOME/.gitconfig" ] || { echo "global config was written"; return 1; }
}

test_two_remotes_sets_push_default() {
  setup; g remote add upstream https://github.com/other/repo.git; run_check
  has "FIXED push-default:" || return 1
  [ "$(g config --get remote.pushDefault)" = origin ] || { echo "pushDefault not origin"; return 1; }
}

test_two_remotes_no_origin_missing() {
  setup; g remote rename origin a; g remote add b https://github.com/other/repo.git; run_check
  code_is 1 && has "MISSING push-default:"
}

test_no_github_remote_missing() {
  setup; g config remote.origin.url "$T/origin.git"; run_check
  code_is 1 && has "MISSING github-remote:"
}

test_empty_trunk_missing() {
  setup; "$REAL_GIT" -C "$T/origin.git" update-ref -d refs/heads/main; run_check
  code_is 1 && has "MISSING trunk: trunk 'main' has no commits on origin"
}

test_duplicate_skill_warns() {
  setup; mkdir -p "$T/repo/.claude/skills/gh-stack"; echo x > "$T/repo/.claude/skills/gh-stack/SKILL.md"; run_check
  has "WARN duplicate-skill:" && has ".claude/skills/gh-stack"
}

test_no_fix_changes_nothing() {
  setup; FAKE_GH_EXT_VERSION= run_check "$T/repo" --no-fix
  code_is 1 && has "MISSING gh-stack:" && has "MISSING rerere:" || return 1
  not_logged "extension install" || return 1
  [ -z "$(g config --local --get rerere.enabled)" ] || { echo "rerere changed under --no-fix"; return 1; }
}
