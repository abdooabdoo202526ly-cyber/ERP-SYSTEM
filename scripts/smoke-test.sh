#!/bin/bash
# smoke-test.sh — Live deployment smoke tests (DEC-099 / DL 66)
# Tests all major API endpoints + UI routes via HTTP probes.
# Usage: ./scripts/smoke-test.sh [URL] (default: https://Anas-Assaket-erp-system.hf.space)

set -e

BASE_URL="${1:-https://Anas-Assaket-erp-system.hf.space}"
RESULTS=()
PASS=0
FAIL=0

# ANSI colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

probe() {
  local label="$1"
  local url="$2"
  local expected="$3"
  local code=$(curl -s -o /dev/null -w "%{http_code}" --max-time 8 "$url" 2>/dev/null || echo "000")

  if [[ "$code" == "$expected" || ("$expected" == "2xx/3xx/4xx" && "$code" =~ ^[234][0-9][0-9]$) ]]; then
    if [[ "$code" =~ ^[45] ]]; then
      echo -e "  ${YELLOW}⚠${NC} $label: $code (expected auth wall - 401/403 acceptable for protected routes)"
      PASS=$((PASS+1))
    else
      echo -e "  ${GREEN}✅${NC} $label: $code"
      PASS=$((PASS+1))
    fi
  elif [[ "$code" == "200" && "$expected" == "401" ]]; then
    echo -e "  ${YELLOW}⚠${NC} $label: $code (auth bypass — verify protection expected)"
    FAIL=$((FAIL+1))
    RESULTS+=("AUTH_BYPASS: $label returned 200 instead of 401")
  else
    echo -e "  ${RED}❌${NC} $label: $code (expected $expected)"
    FAIL=$((FAIL+1))
    RESULTS+=("FAIL: $label $code (expected $expected)")
  fi
}

echo "════════════════════════════════════════════════════════════"
echo "🧪 SMOKE TEST — ERP-SYSTEM"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "🌐 Target: $BASE_URL"
echo "⏰ Time: $(date -u +'%Y-%m-%d %H:%M:%S UTC')"
echo ""

echo "═══ 1️⃣ System Health ═══"
probe "Health check (liveness)" "$BASE_URL/api/health/live" 200
probe "Startup check (DB ping)" "$BASE_URL/api/health/startup-deep" 200
probe "Root UI (Next.js)" "$BASE_URL/" 200

echo ""
echo "═══ 2️⃣ Authentication ═══"
probe "Login page" "$BASE_URL/login" 200
probe "Auth API /api/auth/login (GET → 405 Method Not Allowed expected)" "$BASE_URL/api/auth/login" 405
probe "Auth API /api/auth/register (GET → 405 Method Not Allowed expected)" "$BASE_URL/api/auth/register" 405

echo ""
echo "═══ 3️⃣ Identity API (protected, expect 401 or 200 for public login) ═══"
probe "Companies list" "$BASE_URL/api/companies" 401

echo ""
echo "═══ 4️⃣ Finance API ═══"
probe "Accounts (CoA)" "$BASE_URL/api/finance/accounts" 401
probe "JournalEntries" "$BASE_URL/api/finance/journal-entries" 401
probe "CostCenters" "$BASE_URL/api/cost-centers" 401
probe "PostingRules" "$BASE_URL/api/finance/posting-rules" 401
probe "Ledger trial-balance" "$BASE_URL/api/finance/ledger/trial-balance" 401

echo ""
echo "═══ 5️⃣ Reports ═══"
probe "Trial Balance" "$BASE_URL/api/reports/finance/trial-balance?asOfDate=2025-12-31" 401
probe "Aging AR" "$BASE_URL/api/ar/aging" 401

echo ""
echo "═══ 6️⃣ AR (Accounts Receivable) ═══"
probe "Customers" "$BASE_URL/api/ar/customers" 401
probe "SalesInvoices" "$BASE_URL/api/ar/sales-invoices" 401
probe "Receipts" "$BASE_URL/api/ar/receipts" 401

echo ""
echo "═══ 7️⃣ Procurement ═══"
probe "Vendors" "$BASE_URL/api/procurement/vendors" 401
probe "PurchaseOrders (POs)" "$BASE_URL/api/procurement/pos" 401
probe "GoodsReceipts (GRs)" "$BASE_URL/api/procurement/grs" 401
probe "Bills" "$BASE_URL/api/procurement/bills" 401

echo ""
echo "═══ 8️⃣ Inventory ═══"
probe "Items" "$BASE_URL/api/inventory/items" 401
probe "Categories" "$BASE_URL/api/inventory/categories" 401
probe "UnitOfMeasure (UOM)" "$BASE_URL/api/inventory/uom" 401
probe "Warehouses" "$BASE_URL/api/inventory/warehouses" 401
probe "Stock Levels" "$BASE_URL/api/inventory/levels" 401
probe "Low Stock" "$BASE_URL/api/inventory/levels/low-stock" 401
probe "Stock Movements" "$BASE_URL/api/inventory/movements" 401
probe "Reservations" "$BASE_URL/api/inventory/reservations" 401
probe "Notifications" "$BASE_URL/api/inventory/notifications" 401

echo ""
echo "═══ 9️⃣ Payments ═══"
probe "Payments" "$BASE_URL/api/payments" 401

echo ""
echo "═══ 🔟 Projects ═══"
probe "Projects" "$BASE_URL/api/projects" 401
probe "Tasks" "$BASE_URL/api/tasks" 405
probe "Resources" "$BASE_URL/api/resources" 401

echo ""
echo "═══ 1️⃣1️⃣ HR/Payroll ═══"
probe "Departments" "$BASE_URL/api/hr/departments" 401
probe "Employees" "$BASE_URL/api/hr/employees" 401
probe "Leaves" "$BASE_URL/api/hr/leaves" 401
probe "Attendance" "$BASE_URL/api/hr/attendance" 401
probe "Payroll Runs" "$BASE_URL/api/hr/payroll/runs" 401

echo ""
echo "═══ 1️⃣2️⃣ Companies ═══"
probe "Companies (multicompany)" "$BASE_URL/api/companies" 401

echo ""
echo "════════════════════════════════════════════════════════════"
echo "📊 Results Summary"
echo "════════════════════════════════════════════════════════════"
echo ""
TOTAL=$((PASS+FAIL))
echo "Total probes:  $TOTAL"
echo -e "  ${GREEN}Passed:${NC}       $PASS"
echo -e "  ${RED}Failed:${NC}       $FAIL"
echo ""

if [ $FAIL -gt 0 ]; then
  echo -e "${RED}❌ FAILURES:${NC}"
  for r in "${RESULTS[@]}"; do
    echo "  - $r"
  done
  echo ""
fi

echo "════════════════════════════════════════════════════════════"

# Exit code based on actual failures (not auth-wall failures which are expected)
if [ $FAIL -gt 0 ]; then
  exit 1
fi

echo -e "${GREEN}🎉 All probes passed (auth walls expected for protected routes)${NC}"
