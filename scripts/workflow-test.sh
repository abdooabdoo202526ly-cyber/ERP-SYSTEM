#!/bin/bash
# workflow-test.sh — End-to-end business workflow smoke test (DEC-099 / DL 67)
# Actually logs in + exercises 3 key workflows.
# Usage: ./scripts/workflow-test.sh [URL]

set -e

BASE_URL="${1:-https://Anas-Assaket-erp-system.hf.space}"
EMAIL="${TEST_EMAIL:-admin@alfajr.local}"
PASSWORD="${TEST_PASSWORD:-Demo1234}"

# ANSI colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

PASS=0
FAIL=0
RESULTS=()

echo "════════════════════════════════════════════════════════════"
echo "🧪 WORKFLOW SMOKE TEST — ERP-SYSTEM"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "🌐 Target: $BASE_URL"
echo "👤 User:   $EMAIL"
echo "⏰ Time:  $(date -u +'%Y-%m-%d %H:%M:%S UTC')"
echo ""

# 1️⃣ Login + JWT token acquisition
echo "═══ ${BLUE}1️⃣ Authentication${NC} ═══"
LOGIN_RESPONSE=$(curl -s -X POST -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}" \
  "$BASE_URL/api/auth/login" --max-time 30 2>/dev/null)

TOKEN=$(echo "$LOGIN_RESPONSE" | python3 -c "
import sys, json
try:
    d = json.load(sys.stdin)
    print(d.get('token') or d.get('accessToken') or d.get('access_token') or '')
except: print('')
" 2>/dev/null)

if [ -z "$TOKEN" ]; then
  echo -e "  ${RED}❌ Login failed — response:${NC}"
  echo "  $LOGIN_RESPONSE" | head -c 200
  echo ""
  RESULTS+=("FAIL: Login returned no JWT token")
  FAIL=$((FAIL+1))
else
  echo -e "  ${GREEN}✅${NC} Login successful — token acquired ($(echo $TOKEN | wc -c) chars)"
  PASS=$((PASS+1))
fi

if [ -z "$TOKEN" ]; then
  echo ""
  echo "════════════════════════════════════════════════════════════"
  echo "❌ Cannot proceed without token"
  echo "════════════════════════════════════════════════════════════"
  exit 1
fi

# Helper for authenticated GETs
get_api() {
  local label="$1"
  local url="$2"
  local code=$(curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Bearer $TOKEN" \
    --max-time 15 "$url" 2>/dev/null)
  if [ "$code" = "200" ]; then
    echo -e "  ${GREEN}✅${NC} $label: $code"
    PASS=$((PASS+1))
    return 0
  else
    echo -e "  ${RED}❌${NC} $label: $code"
    RESULTS+=("FAIL: $label returned $code")
    FAIL=$((FAIL+1))
    return 1
  fi
}

echo ""
echo "═══ ${BLUE}2️⃣ Workflow A: Procurement${NC} (Vendor → PO → GR → Bill → Payment) ═══"
echo ""
echo "  ${BLUE}Step 1:${NC} List vendors"
get_api "Vendors list" "$BASE_URL/api/procurement/vendors"
echo "  ${BLUE}Step 2:${NC} List purchase orders"
get_api "POs list" "$BASE_URL/api/procurement/pos"
echo "  ${BLUE}Step 3:${NC} List goods receipts"
get_api "GRs list" "$BASE_URL/api/procurement/grs"
echo "  ${BLUE}Step 4:${NC} List bills"
get_api "Bills list" "$BASE_URL/api/procurement/bills"
echo "  ${BLUE}Step 5:${NC} List payments"
get_api "Payments list" "$BASE_URL/api/payments"

echo ""
echo "═══ ${BLUE}3️⃣ Workflow B: Sales${NC} (Customer → SalesInvoice → Receipt) ═══"
echo ""
echo "  ${BLUE}Step 1:${NC} List customers"
get_api "Customers list" "$BASE_URL/api/ar/customers"
echo "  ${BLUE}Step 2:${NC} List sales invoices"
get_api "SalesInvoices list" "$BASE_URL/api/ar/sales-invoices"
echo "  ${BLUE}Step 3:${NC} List receipts"
get_api "Receipts list" "$BASE_URL/api/ar/receipts"
echo "  ${BLUE}Step 4:${NC} Aging AR report"
get_api "Aging AR" "$BASE_URL/api/ar/aging"

echo ""
echo "═══ ${BLUE}4️⃣ Workflow C: Inventory${NC} (Item → Stock Movement → Reservation) ═══"
echo ""
echo "  ${BLUE}Step 1:${NC} List items"
get_api "Items list" "$BASE_URL/api/inventory/items"
echo "  ${BLUE}Step 2:${NC} List stock levels"
get_api "StockLevels" "$BASE_URL/api/inventory/levels"
echo "  ${BLUE}Step 3:${NC} Low stock"
get_api "Low stock" "$BASE_URL/api/inventory/levels/low-stock"
echo "  ${BLUE}Step 4:${NC} Stock movements"
get_api "Stock movements" "$BASE_URL/api/inventory/movements"
echo "  ${BLUE}Step 5:${NC} Reservations"
get_api "Reservations" "$BASE_URL/api/inventory/reservations"

echo ""
echo "═══ ${BLUE}5️⃣ Workflow D: Finance Reports${NC} ═══"
echo ""
echo "  ${BLUE}Step 1:${NC} Chart of Accounts"
get_api "Accounts (CoA)" "$BASE_URL/api/finance/accounts"
echo "  ${BLUE}Step 2:${NC} Journal Entries"
get_api "JournalEntries" "$BASE_URL/api/finance/journal-entries"
echo "  ${BLUE}Step 3:${NC} Cost Centers"
get_api "CostCenters" "$BASE_URL/api/cost-centers"
echo "  ${BLUE}Step 4:${NC} Trial Balance"
get_api "Trial Balance" "$BASE_URL/api/reports/finance/trial-balance?asOfDate=2025-12-31"
echo "  ${BLUE}Step 5:${NC} Ledger"
get_api "Ledger trial-balance" "$BASE_URL/api/finance/ledger/trial-balance"

echo ""
echo "═══ ${BLUE}6️⃣ Workflow E: HR + Projects${NC} ═══"
echo ""
echo "  ${BLUE}Step 1:${NC} Employees"
get_api "Employees" "$BASE_URL/api/hr/employees"
echo "  ${BLUE}Step 2:${NC} Departments"
get_api "Departments" "$BASE_URL/api/hr/departments"
echo "  ${BLUE}Step 3:${NC} Projects"
get_api "Projects" "$BASE_URL/api/projects"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "📊 Workflow Test Results"
echo "════════════════════════════════════════════════════════════"
echo ""

TOTAL=$((PASS+FAIL))
echo "Total API calls:  $TOTAL"
echo -e "  ${GREEN}Passed:${NC}          $PASS"
echo -e "  ${RED}Failed:${NC}          $FAIL"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}❌ FAILURES:${NC}"
  for r in "${RESULTS[@]}"; do
    echo "  - $r"
  done
fi

echo ""
echo "════════════════════════════════════════════════════════════"

if [ $FAIL -eq 0 ]; then
  echo -e "${GREEN}🎉 All workflow steps passed!${NC}"
else
  echo -e "${YELLOW}⚠ Some workflow steps failed. See above.${NC}"
  exit 1
fi
