#!/bin/bash
# Runs every test_* function in check-prereqs.test.sh, each in its own subshell.
# Usage: /bin/bash tests/stacked-superpowers/run.sh [name-filter]
HERE="$(cd "$(dirname "$0")" && pwd)"
. "$HERE/check-prereqs.test.sh"
pass=0; fail=0
for t in $(declare -F | awk '{print $3}' | grep '^test_' | grep -- "${1:-}"); do
  if msg="$( ( $t ) 2>&1 )"; then
    echo "PASS $t"; pass=$((pass+1))
  else
    echo "FAIL $t"; echo "$msg" | sed 's/^/     /'; fail=$((fail+1))
  fi
done
echo "$pass passed, $fail failed"
[ "$fail" -eq 0 ]
