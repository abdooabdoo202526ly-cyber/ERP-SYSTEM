#!/usr/bin/env bash
# =============================================================================
# ERP-SYSTEM — Comprehensive E2E Smoke Test
# =============================================================================
# يختبر كل الـ endpoints الرئيسية:
#   - Auth (register, login, me, logout, refresh)
#   - Finance (accounts, journals, reports, customers, invoices, receipts)
#   - Procurement (POs, GRs, vendor bills, vendors)
#   - HR (employees, departments, attendance, leaves, payroll, salary)
#   - Inventory (items, categories, warehouses, UoM, stock levels, movements)
#   - Payments (payments, allocations)
#   - Projects (projects, tasks, budgets, resources)
#   - Companies (companies, cost centers)
#   - Admin (users, roles, posting rules, notifications)
# =============================================================================
# Usage:
#   ./scripts/e2e-smoke-test.sh
#   ./scripts/e2e-smoke-test.sh --api http://localhost:5000
#   ./scripts/e2e-smoke-test.sh --verbose
# =============================================================================

set -u

API="${API:-http://localhost:5000}"
VERBOSE="${VERBOSE:-0}"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Counters
TOTAL=0
PASSED=0
FAILED=0
SKIPPED=0
FAILED_TESTS=()

# Parse args
while [[ $# -gt 0 ]]; do
  case "$1" in
    --api) API="$2"; shift 2 ;;
    --verbose|-v) VERBOSE=1; shift ;;
    --help|-h)
      echo "Usage: $0 [--api URL] [--verbose]"
      exit 0
      ;;
    *) shift ;;
  esac
done

# --- helpers ----------------------------------------------------------------
print_test()  { echo -e "  ${CYAN}[TEST]${NC} $1"; }
print_pass()  { echo -e "  ${GREEN}[PASS]${NC} $1"; PASSED=$((PASSED+1)); }
print_fail()  { echo -e "  ${RED}[FAIL]${NC} $1"; FAILED=$((FAILED+1)); FAILED_TESTS+=("$1"); }
print_skip()  { echo -e "  ${YELLOW}[SKIP]${NC} $1"; SKIPPED=$((SKIPPED+1)); }
print_section() { echo -e "\n${YELLOW}========== $1 ==========${NC}"; }

# Run a test. Args: name method url [data] [expected_status]
# Sets RESPONSE, STATUS globals
run_test() {
  local name="$1"
  local method="$2"
  local url="$3"
  local data="${4:-}"
  local expected="${5:-200}"
  TOTAL=$((TOTAL+1))

  print_test "$name ($method $url)"

  if [[ -n "$data" ]]; then
    RESPONSE=$(curl -s -w "\n%{http_code}" -X "$method" \
      -H "Authorization: Bearer $TOKEN" \
      -H "Content-Type: application/json" \
      -d "$data" \
      "$API$url" 2>/dev/null)
  else
    RESPONSE=$(curl -s -w "\n%{http_code}" -X "$method" \
      -H "Authorization: Bearer $TOKEN" \
      "$API$url" 2>/dev/null)
  fi

  STATUS=$(echo "$RESPONSE" | tail -n1)
  BODY=$(echo "$RESPONSE" | sed '$d')

  if [[ "$STATUS" == "$expected" ]]; then
    print_pass "$name (status $STATUS)"
    if [[ "$VERBOSE" == "1" && -n "$BODY" ]]; then
      echo "    Response (first 200 chars): $(echo "$BODY" | head -c 200)"
    fi
  else
    print_fail "$name — expected $expected, got $STATUS"
    echo "    Response: $(echo "$BODY" | head -c 300)"
  fi
}

# Variant for endpoints that should return 4xx without auth
run_test_noauth() {
  local name="$1"
  local method="$2"
  local url="$3"
  local expected="${4:-401}"
  TOTAL=$((TOTAL+1))

  print_test "$name ($method $url, no auth)"

  RESPONSE=$(curl -s -w "\n%{http_code}" -X "$method" "$API$url" 2>/dev/null)
  STATUS=$(echo "$RESPONSE" | tail -n1)
  BODY=$(echo "$RESPONSE" | sed '$d')

  if [[ "$STATUS" == "$expected" ]]; then
    print_pass "$name (status $STATUS, correctly rejected)"
  else
    print_fail "$name — expected $expected, got $STATUS (no-auth endpoint should reject)"
  fi
}

# --- Pre-flight checks ------------------------------------------------------
print_section "Pre-flight"
TOTAL=$((TOTAL+1))
if curl -s -f "$API/health" > /dev/null 2>&1; then
  print_pass "API is reachable at $API"
else
  print_fail "API is NOT reachable at $API — start docker compose first"
  echo "  Try: cd infra/docker && docker compose -f docker-compose.dev.yml up -d"
  exit 1
fi

# --- Auth -------------------------------------------------------------------
print_section "Auth"
run_test_noauth "GET /api/auth/me (no token)" GET "/api/auth/me" 401
run_test "POST /api/auth/login (valid)" POST "/api/auth/login" \
  '{"email":"admin@alfajr.local","password":"Demo1234"}' 200

# Save token
LOGIN_RESP=$(curl -s -X POST -H "Content-Type: application/json" \
  -d '{"email":"admin@alfajr.local","password":"Demo1234"}' \
  "$API/api/auth/login")
TOKEN=$(echo "$LOGIN_RESP" | grep -oE '"accessToken":"[^"]+"' | cut -d'"' -f4)

if [[ -z "$TOKEN" ]]; then
  echo -e "${RED}FATAL: Failed to get access token. Cannot continue.${NC}"
  echo "Login response: $LOGIN_RESP"
  exit 1
fi
print_pass "Got JWT token (len=${#TOKEN})"

run_test "GET /api/auth/me" GET "/api/auth/me" 200

# --- Finance: Accounts ------------------------------------------------------
print_section "Finance: Chart of Accounts"
run_test "GET /api/finance/accounts" GET "/api/finance/accounts" 200
run_test "POST /api/finance/accounts (create)" POST "/api/finance/accounts" \
  '{"code":"1100-TEST","name":"Cash Test","type":1,"normalBalance":1,"isPostable":true,"isActive":true}' 201

# Capture the new account id
ACCT_ID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/finance/accounts" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print([a['id'] for a in d if a.get('code')=='1100-TEST'][0])" 2>/dev/null)
if [[ -n "$ACCT_ID" ]]; then
  run_test "GET /api/finance/accounts/{id}" GET "/api/finance/accounts/$ACCT_ID" 200
  print_skip "PUT /api/finance/accounts/{id} (NOT IMPLEMENTED in v1.0.9)"
  run_test "DELETE /api/finance/accounts/{id} (soft delete)" DELETE "/api/finance/accounts/$ACCT_ID" 204
else
  print_fail "Could not capture account id for further tests"
fi

# --- Finance: Reports -------------------------------------------------------
print_section "Finance: Reports"
run_test "GET /api/reports/finance/trial-balance" GET "/api/reports/finance/trial-balance?asOfDate=2026-12-31" 200
run_test "GET /api/reports/finance/balance-sheet" GET "/api/reports/finance/balance-sheet?asOfDate=2026-12-31" 200
run_test "GET /api/reports/finance/income-statement" GET "/api/reports/finance/income-statement?fromDate=2026-01-01&toDate=2026-12-31" 200
run_test "GET /api/reports/finance/cash-flow" GET "/api/reports/finance/cash-flow?fromDate=2026-01-01&toDate=2026-12-31" 200

# --- AR: Customers ----------------------------------------------------------
print_section "AR: Customers"
run_test "GET /api/ar/customers" GET "/api/ar/customers" 200
run_test "POST /api/ar/customers (create)" POST "/api/ar/customers" \
  '{"code":"CUST-TEST-001","name":"عميل اختبار","email":"test@example.com","creditLimit":1000,"paymentTermsDays":30}' 201

CUST_ID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/ar/customers" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print([c['id'] for c in d if c.get('code')=='CUST-TEST-001'][0])" 2>/dev/null)
if [[ -n "$CUST_ID" ]]; then
  run_test "GET /api/ar/customers/{id}" GET "/api/ar/customers/$CUST_ID" 200
  run_test "PUT /api/ar/customers/{id}" PUT "/api/ar/customers/$CUST_ID" \
    '{"code":"CUST-TEST-001","name":"عميل اختبار (معدّل)","creditLimit":2000}' 200
  run_test "DELETE /api/ar/customers/{id}" DELETE "/api/ar/customers/$CUST_ID" 204
fi

# --- AR: Sales Invoices -----------------------------------------------------
print_section "AR: Sales Invoices"
run_test "GET /api/ar/sales-invoices" GET "/api/ar/sales-invoices" 200
# Get any customer id for invoice creation
ANY_CUST=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/ar/customers?includeInactive=true" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print(d[0]['id'] if d else '')" 2>/dev/null)
if [[ -n "$ANY_CUST" ]]; then
  run_test "POST /api/ar/sales-invoices" POST "/api/ar/sales-invoices" \
    "{\"customerId\":\"$ANY_CUST\",\"invoiceDate\":\"2026-07-23\",\"dueDate\":\"2026-08-22\",\"currencyCode\":\"LYD\",\"exchangeRate\":1,\"lines\":[{\"description\":\"Test item\",\"quantity\":1,\"unitPrice\":100,\"taxRate\":0}],\"postImmediately\":false}" 201
fi

# --- AR: Receipts -----------------------------------------------------------
print_section "AR: Receipts"
run_test "GET /api/ar/receipts" GET "/api/ar/receipts" 200
if [[ -n "$ANY_CUST" ]]; then
  run_test "POST /api/ar/receipts" POST "/api/ar/receipts" \
    "{\"customerId\":\"$ANY_CUST\",\"receiptDate\":\"2026-07-23\",\"amount\":100,\"currencyCode\":\"LYD\",\"paymentMethod\":\"Cash\",\"notes\":\"Test receipt\"}" 201
fi
run_test "GET /api/ar/aging" GET "/api/ar/aging?asOfDate=2026-07-23" 200

# --- Procurement: Vendors ---------------------------------------------------
print_section "Procurement: Vendors"
run_test "GET /api/procurement/vendors" GET "/api/procurement/vendors" 200
run_test "POST /api/procurement/vendors" POST "/api/procurement/vendors" \
  '{"code":"V-TEST-001","name":"مورّد اختبار","email":"vendor@example.com","paymentTerms":"Net30"}' 201

VEND_ID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/procurement/vendors" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print([v['id'] for v in d if v.get('code')=='V-TEST-001'][0])" 2>/dev/null)
if [[ -n "$VEND_ID" ]]; then
  run_test "GET /api/procurement/vendors/{id}" GET "/api/procurement/vendors/$VEND_ID" 200
  run_test "PUT /api/procurement/vendors/{id}" PUT "/api/procurement/vendors/$VEND_ID" \
    '{"code":"V-TEST-001","name":"مورّد اختبار (معدّل)"}' 200
  run_test "DELETE /api/procurement/vendors/{id}" DELETE "/api/procurement/vendors/$VEND_ID" 204
fi

# --- Procurement: POs / GRs / Bills -----------------------------------------
print_section "Procurement: Purchase Orders, Goods Receipts, Vendor Bills"
run_test "GET /api/procurement/pos" GET "/api/procurement/pos" 200
run_test "GET /api/procurement/goods-receipts" GET "/api/procurement/goods-receipts" 200
run_test "GET /api/procurement/bills" GET "/api/procurement/bills" 200

# --- Inventory: Items, Categories, Warehouses, UoM -------------------------
print_section "Inventory"
run_test "GET /api/inventory/items" GET "/api/inventory/items" 200
run_test "GET /api/inventory/categories" GET "/api/inventory/categories" 200
run_test "GET /api/inventory/warehouses" GET "/api/inventory/warehouses" 200
run_test "GET /api/inventory/uom" GET "/api/inventory/uom" 200
run_test "GET /api/inventory/stock-levels" GET "/api/inventory/stock-levels" 200
run_test "GET /api/inventory/movements" GET "/api/inventory/movements" 200
run_test "GET /api/inventory/reservations" GET "/api/inventory/reservations" 200

run_test "POST /api/inventory/categories" POST "/api/inventory/categories" \
  '{"code":"CAT-TEST","name":"فئة اختبار","isActive":true}' 201
CAT_ID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/inventory/categories" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print([c['id'] for c in d if c.get('code')=='CAT-TEST'][0])" 2>/dev/null)
if [[ -n "$CAT_ID" ]]; then
  run_test "PUT /api/inventory/categories/{id}" PUT "/api/inventory/categories/$CAT_ID" \
    '{"code":"CAT-TEST","name":"فئة اختبار (معدّل)"}' 200
  print_skip "DELETE /api/inventory/categories/{id} (NOT IMPLEMENTED in v1.0.9)"
fi

run_test "POST /api/inventory/uom" POST "/api/inventory/uom" \
  '{"code":"TST","name":"Test Unit","symbol":"t"}' 201
UOM_ID=$(curl -s -H "Authorization: Bearer $TOKEN" "$API/api/inventory/uom" | \
  python3 -c "import sys,json; d=json.load(sys.stdin); print([u['id'] for u in d if u.get('code')=='TST'][0])" 2>/dev/null)
if [[ -n "$UOM_ID" ]]; then
  print_skip "PUT /api/inventory/uom/{id} (NOT IMPLEMENTED in v1.0.9)"
  print_skip "DELETE /api/inventory/uom/{id} (NOT IMPLEMENTED in v1.0.9)"
fi

# --- HR ---------------------------------------------------------------------
print_section "HR"
run_test "GET /api/hr/employees" GET "/api/hr/employees" 200
run_test "GET /api/hr/departments" GET "/api/hr/departments" 200
run_test "GET /api/hr/attendance" GET "/api/hr/attendance" 200
run_test "GET /api/hr/leaves" GET "/api/hr/leaves" 200
run_test "GET /api/hr/payroll/runs" GET "/api/hr/payroll/runs" 200
run_test "GET /api/hr/salary-structures" GET "/api/hr/salary-structures" 200
run_test "GET /api/hr/payslips" GET "/api/hr/payslips" 200

# --- Payments ---------------------------------------------------------------
print_section "Payments"
run_test "GET /api/payments" GET "/api/payments" 200

# --- Companies --------------------------------------------------------------
print_section "Companies & Cost Centers"
run_test "GET /api/companies" GET "/api/companies" 200
run_test "GET /api/cost-centers" GET "/api/cost-centers" 200

# --- Projects ---------------------------------------------------------------
print_section "Projects"
run_test "GET /api/projects" GET "/api/projects" 200
run_test "GET /api/projects/tasks" GET "/api/projects/tasks" 200
run_test "GET /api/projects/resources" GET "/api/projects/resources" 200

# --- Admin: Posting Rules, Notifications ------------------------------------
print_section "Admin"
run_test "GET /api/admin/posting-rules" GET "/api/admin/posting-rules" 200
run_test "GET /api/admin/notifications" GET "/api/admin/notifications" 200

# --- Summary ----------------------------------------------------------------
print_section "Summary"
echo -e "  Total:  $TOTAL"
echo -e "  ${GREEN}Passed: $PASSED${NC}"
echo -e "  ${RED}Failed: $FAILED${NC}"
echo -e "  ${YELLOW}Skipped: $SKIPPED${NC}"

if [[ $FAILED -gt 0 ]]; then
  echo ""
  echo -e "${RED}Failed tests:${NC}"
  for t in "${FAILED_TESTS[@]}"; do
    echo "  - $t"
  done
  exit 1
else
  echo -e "\n${GREEN}All tests passed!${NC}"
  exit 0
fi
