#!/usr/bin/env bash
# End-to-end smoke test: builds and starts the whole stack with docker compose, walks the guide example
# (INV-77812 blocked on quantity -> receive the last 2 boxes on PO 4500123 -> Matched) through the BFF,
# then tears everything down, volumes included. Prints "SMOKE PASS" and exits 0 on success.
# Bash 3.2 compatible (macOS default shell).
set -euo pipefail

cd "$(dirname "$0")/.."

BASE_URL="http://localhost:5300"
GUIDE_REASON="Quantity: billed 2 more than received (line 1)"

fail() {
    echo "SMOKE FAIL: $*" >&2
    exit 1
}

cleanup() {
    echo "--- tearing down (docker compose down -v)"
    docker compose down -v --remove-orphans >/dev/null 2>&1 || true
}

# json <expression> : reads JSON on stdin, prints the value. The expression is a jq filter;
# the python fallback understands the small subset used below.
if command -v jq >/dev/null 2>&1; then
    json() { jq -r "$1"; }
else
    json() {
        python3 -c '
import json, sys
expr = sys.argv[1]
data = json.load(sys.stdin)
def walk(node, path):
    for part in [p for p in path.split(".") if p]:
        node = node[part] if isinstance(node, dict) else None
        if node is None:
            break
    return node
if expr.startswith(".[] | select(.invoiceNumber == "):
    number = expr.split("\"")[1]
    field = expr.rsplit(".", 1)[1]
    hits = [i for i in data if i.get("invoiceNumber") == number]
    value = hits[0].get(field) if hits else None
else:
    value = walk(data, expr)
print("null" if value is None else (str(value).lower() if isinstance(value, bool) else value))
' "$1"
    }
fi

# get <path> : GET through the BFF, fails the smoke test on a non-2xx status.
get() {
    curl -fsS "$BASE_URL$1" || fail "GET $1 failed"
}

expect() { # expect <label> <actual> <expected>
    if [ "$2" != "$3" ]; then
        fail "$1: expected '$3' but got '$2'"
    fi
    echo "ok   $1 = $2"
}

[ -f .env ] || fail ".env is missing. Run: cp .env.example .env"
[ -f docker-compose.yml ] || fail "docker-compose.yml not found in $(pwd)"

trap cleanup EXIT

echo "--- docker compose up -d --build --wait"
docker compose up -d --build --wait || fail "docker compose up did not reach a healthy state"

echo "--- guide example"
dashboard="$(get /bff/dashboard)"
expect "dashboard kpis.openExceptions (before)" "$(printf '%s' "$dashboard" | json '.kpis.openExceptions')" "1"

invoices="$(get '/api/invoices?status=Blocked')"
invoice_id="$(printf '%s' "$invoices" | json '.[] | select(.invoiceNumber == "INV-77812") | .id')"
[ -n "$invoice_id" ] && [ "$invoice_id" != "null" ] || fail "INV-77812 is not in the Blocked invoice list"
po_id="$(printf '%s' "$invoices" | json '.[] | select(.invoiceNumber == "INV-77812") | .poId')"
expect "INV-77812 poNumber" "$(printf '%s' "$invoices" | json '.[] | select(.invoiceNumber == "INV-77812") | .poNumber')" "4500123"

invoice="$(get "/api/invoices/$invoice_id")"
expect "INV-77812 status (before)" "$(printf '%s' "$invoice" | json '.status')" "Blocked"
expect "INV-77812 blockReason" "$(printf '%s' "$invoice" | json '.blockReason')" "$GUIDE_REASON"

receipt_status="$(curl -sS -o /dev/null -w '%{http_code}' -X POST "$BASE_URL/api/purchase-orders/$po_id/receipts" \
    -H 'Content-Type: application/json' \
    -d '{"receivedBy":"Smoke test","packingSlip":"PS-SMOKE","lines":[{"poLineNo":1,"quantity":2}]}')" \
    || fail "POST receipt request failed"
expect "POST receipt of 2 on PO 4500123 line 1 (HTTP status)" "$receipt_status" "201"

invoice="$(get "/api/invoices/$invoice_id")"
expect "INV-77812 status (after)" "$(printf '%s' "$invoice" | json '.status')" "Matched"

dashboard="$(get /bff/dashboard)"
expect "dashboard kpis.openExceptions (after)" "$(printf '%s' "$dashboard" | json '.kpis.openExceptions')" "0"

content_type="$(curl -fsS -o /dev/null -w '%{content_type}' "$BASE_URL/invoices/$invoice_id")" \
    || fail "GET /invoices/$invoice_id failed"
case "$content_type" in
    text/html*) echo "ok   GET /invoices/<id> content type = $content_type" ;;
    *) fail "GET /invoices/<id>: expected text/html but got '$content_type'" ;;
esac

echo "SMOKE PASS"
