# DEC-086+: JSON-Driven Seed Data

This directory contains seed data for the `RealisticSeed` BackgroundService.

## Status (DEC-089)

### Active (RealisticSeed reads from JSON)
- `seed_companies.json` (5 companies) — **DEC-087**
- `seed_vendors.json` (25 vendors) — **DEC-088**
- `seed_customers.json` (20 customers) — **DEC-088**
- `seed_items.json` (15 items) — **DEC-088**
- `seed_projects.json` (11 projects) — **DEC-088**

### Read-Only (data extracted but RealisticSeed still uses C#)
- `seed_gls.json` (64 accounts) — **DEC-089**
- `seed_employees.json` (20 employees) — **DEC-089**
- `seed_cost_centers.json` (3 cost centers) — **DEC-089**
- `seed_pos.json` (50 POs) — **DEC-089**
- `seed_grns.json` (50 GRs) — **DEC-089**
- `seed_bills.json` (50 bills) — **DEC-089**
- `seed_bill_lines.json` (50 bill lines) — **DEC-089**
- `seed_sales_invoices.json` (50 sales invoices) — **DEC-089**
- `seed_payments.json` (50 payments) — **DEC-089**
- `seed_journal_entries.json` (50 JEs) — **DEC-089**
- `seed_je_lines.json` (100 JE lines, 2 per JE) — **DEC-089**

### Reference
- `seed_meta.json` (tenant_id reference)

## JSON Schema

Each file has this structure:

```json
{
  "entity": "Vendor",
  "table": "vendors",
  "tenant_id": "f77dbedd-64ff-41ac-b77a-0731183ff744",
  "_comment": "Optional — human-readable note",
  "records": [
    { "code": "V-101", "name": "...", "...": "..." }
  ]
}
```

- `entity`: Display name (PascalCase)
- `table`: Database table (snake_case)
- `tenant_id`: Foreign key to `tenants.id`
- `records`: Array of objects

## Loader

`JsonSeedLoader` (in `Shared/SeedData/`) reads all `*.json` files on startup.

DEC-087+ use it directly in `RealisticSeedHostedService`.
DEC-089+ are read-only (RealisticSeed still uses C# — DEC-090 will refactor).

## Future Work (DEC-090+)

- Refactor `RealisticSeedHostedService` to use the remaining 11 JSONs
- Remove hardcoded C# data entirely
- DEC-091+: Source Generator for C# entity + DTO + repository from JSON
