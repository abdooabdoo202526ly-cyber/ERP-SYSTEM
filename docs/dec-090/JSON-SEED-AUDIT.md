# DEC-090 Audit: JSON-Driven RealisticSeed (Post-Retro)

## 📊 Final Score: 14/16 entities actively JSON-driven (87.5%)

Audit date: 2026-07-09 (post-retro)

---

## ✅ ACTIVE — Uses `JsonSeedLoader` (14 methods)

| # | Method | Entity | JSON File | Records |
|---|---|---|---|---|
| 1 | `SeedCompaniesAsync` | companies | seed_companies.json | 5 |
| 2 | `SeedVendorsAsync` | vendors | seed_vendors.json | 25 |
| 3 | `SeedCustomersAsync` | customers | seed_customers.json | 20 |
| 4 | `SeedItemsAsync` | items | seed_items.json | 15 |
| 5 | `SeedProjectsAsync` | projects | seed_projects.json | 11 |
| 6 | `SeedGlsAsync` | gls | seed_gls.json | 64 |
| 7 | `SeedEmployeesAsync` | employees | seed_employees.json | 20 |
| 8 | `SeedCostCentersAsync` | cost_centers | seed_cost_centers.json | 3 |
| 9 | `SeedPOsAsync` | purchase_orders | seed_pos.json | 50 |
| 10 | `SeedGoodsReceiptsAsync` | goods_receipts | seed_grns.json | 50 |
| 11 | `SeedBillsAsync` | vendor_bills | seed_bills.json | 50 |
| 12 | `SeedPaymentsAsync` | payments | seed_payments.json | 50 |
| 13 | `SeedSalesInvoicesAsync` | sales_invoices | seed_sales_invoices.json | 50 |
| 14 | `SeedJournalEntriesAsync` | journal_entries | seed_journal_entries.json (try-catch wrapped) | ~50 |

**Active coverage**: 14/16 = **87.5%**

---

## ⚠️ DEFERRED — Still in C# (2 entities sub-methods)

These are typically **line items** for parent entities, not standalone entities:

| Sub-Method | Entity Type | Why Deferred | Estimated Effort |
|---|---|---|---|
| `SeedBillLinesAsync` | bill_lines (child) | Per-bill context, complex FK lookups | 1.5h |
| `SeedJournalLinesAsync` | je_lines (child) | Per-JE context, balance-dependent | 1h |

**Plus**: UserRoles, PostingRules, Notifications, StockLevels, StockReservations, SalaryStructureLines, PayrollItems, PayslipComponents, PaymentAllocations — line items or rarely-seeded entities.

---

## 📂 Seed JSON Files Inventory

```
src/backend/Host/data-types/seeds/
├── seed_meta.json              (meta info)
├── seed_companies.json         ✅ active (5)
├── seed_vendors.json           ✅ active (25)
├── seed_customers.json         ✅ active (20)
├── seed_items.json             ✅ active (15)
├── seed_projects.json          ✅ active (11)
├── seed_gls.json               ✅ active (64)
├── seed_employees.json         ✅ active (20)
├── seed_cost_centers.json      ✅ active (3)
├── seed_pos.json               ✅ active (50)
├── seed_grns.json              ✅ active (50)
├── seed_bills.json             ✅ active (50)
├── seed_bill_lines.json        ⏸️ read-only (50)
├── seed_sales_invoices.json    ✅ active (50)
├── seed_payments.json          ✅ active (50)
├── seed_journal_entries.json   ✅ active (~50)
└── seed_je_lines.json          ⏸️ read-only (100)
```

**17 JSON files** total: **14 active** + **3 read-only** (16 entities covered).

---

## 🎯 Recommendations

### Sprint-4+ (Future)
1. **BillLines to JSON** — Requires nested structure support; effort ~1.5h
2. **JELines to JSON** — Requires balance-check validation during seed; effort ~1h
3. **Convert PostingRules to JSON** — Currently in C# for one-off setup; minor benefit

### Never (Not Worth It)
1. UserRoles, StockLevels, StockReservations — runtime-generated
2. Notifications — test fixtures only
3. PaymentAllocations — same as bill lines pattern

---

## 🛡️ Defense Layer 64: DEC-090 Audit Complete

**Coverage matrix**:
- Active JSON: 14/16 entities (87.5%)
- Read-only JSON: 16/16 entities (100% — all have JSON files for inspection)
- Sprint-3 deliverables: 100% (per sprint charter)

**Audit verdict**: Sprint-3 (JSON Migration) is **functionally complete** for the 14 primary entities.
The 2 deferred sub-entities are scoped to future sprints.

---

Refs: DEC-082 (Batch 1-4), DEC-083 (Composite PK), DEC-085 (Schema fixes), DEC-086 (JsonSeedLoader), DEC-087-090 (RealisticSeed conversion)
