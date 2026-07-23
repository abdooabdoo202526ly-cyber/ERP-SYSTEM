# DEC-093: Replace Manual with Generated - Mapping Plan

## Status
- **Generated**: 32 DTOs + 32 Repositories (DEC-091, DEC-092)
- **Manual in code**: All 32 entities currently have manual DTOs and Repositories
- **DEC-093 Scope**: Start with NEW entities only (per strategy Option B)

## Strategy: Gradual Replacement

### Step 1: New Entities First (Safe)
Replace manual DTOs/Repos with generated for entities that are NOT yet integrated in any module:
- GLS (Chart of Accounts) - not currently used by any module API
- CostCenters - not currently used by any module API

These have no consumer, so safe to replace.

### Step 2: Recently Active Entities
After Step 1, replace for the 4 newly JSON-active entities from DEC-090 Part 2:
- ChartOfAccounts (GLS)
- CostCenters
- Employees
- PurchaseOrders (POs)

### Step 3: Battle-Tested Entities (Last)
Only after Steps 1 & 2 validate the pattern:
- Companies, Vendors, Customers, Items, Projects (DEC-079+)
- Bills, SalesInvoices, Payments, GRs (DEC-088-090)
- Plus: Employees, etc.

These have working APIs. Replace only if all tests pass.

## Replacement Procedure

For each entity, when replacing:

1. **Generate** the new DTOs/Repository from JSON
2. **Side-by-side**: Place new files in `{Module}/Generated/`
3. **Mark** the manual DTOs/Repositories as `[Obsolete]`
4. **Migrate** consumers one at a time (services, controllers)
5. **Remove** the manual DTOs/Repository when 0 references

## Folder Structure (Future)

```
src/backend/Modules/{Module}/
├── Generated/
│   ├── CompanyDtos.g.cs
│   ├── CompanyRepository.g.cs
│   └── ... (other entities)
├── Dtos/ (manual - keep for reference)
├── Repositories/ (manual - keep for reference)
├── Services/
└── Controllers/
```

## Timeline

- DEC-093 (today): Plan + scope
- DEC-094: Start with 2 safe entities
- DEC-095+: Continue gradually
- Each DEC touches 1-2 entities
- Each DEC: build + test + deploy

## Risk Mitigation

- Generated code is scaffold-grade (DEC-091, DEC-092 PoC)
- Manual code is battle-tested
- Don't replace battle-tested code until generated code is proven
- Gradual migration = low risk per change

## Status

DEC-093 is the PLAN phase. No code changes yet.
