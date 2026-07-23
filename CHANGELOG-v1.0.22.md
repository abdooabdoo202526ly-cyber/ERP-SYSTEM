# v1.0.22 — Form fixes + edit/delete everywhere

## What was broken (from screenshots)

| Screen | Bug | Fix |
|--------|-----|-----|
| `inventory/items/new` | `itemType: "Stock"` (string) sent to enum field → 400 | Send as numeric enum (1=RawMaterial, 2=FinishedGood, 3=Consumable, 4=Service) |
| `inventory/items/new` | `averageCost` field name wrong; backend expects `standardCost` | Renamed to `standardCost` |
| `projects/new` | `companyId: 00000000...` (empty GUID) → 400 | Made `CompanyId` nullable; service picks first company for tenant or auto-creates a default holding |
| `projects/new` | `costCenterId` was a free-form input | Replaced with a Select dropdown populated from `/api/cost-centers` |
| `procurement/vendors` list | No edit/delete buttons | Added edit (pencil) + delete (trash) actions per row |
| All entities | Various forms used raw `fetch()` and lost the 400 details | Forms now use `inventoryApi`/`projectsApi`/`procurementApi` etc. so the 400 error interceptor shows the real reason |
| `api.ts` | Many entities missing CRUD wrappers | Added complete CRUD for: items, vendors, projects, cost-centers, item categories, UoM, warehouses |

## What I added (api.ts)

```typescript
inventoryApi:
  - getItem, createItem, updateItem, deleteItem
  - listUoms, createUom, updateUom, deactivateUom
  - listItemCategories, createItemCategory
  - listWarehouses

procurementApi:
  - deleteVendor

projectsApi:
  - getProject, createProject, updateProject, deleteProject
  - listCostCenters, createCostCenter, updateCostCenter, deleteCostCenter

financeApi (already had):
  - listAccounts, getAccount, createAccount, updateAccount, deleteAccount
```

## How to use

```powershell
.\scripts\NUKE-AND-INSTALL.ps1
```

Then for each screen:
- Add new entity → should now POST 200 instead of 400
- Lists should have edit/delete buttons
- The error banner at the top of each form shows the actual 400 reason if validation fails

## Files changed
- `src/backend/Modules/Projects/Application/ProjectsDtos.cs` — CompanyId nullable
- `src/backend/Modules/Projects/Application/Validators.cs` — drop CompanyId rule
- `src/backend/Modules/Projects/Application/Services/ProjectService.cs` — auto-pick company
- `src/backend/Modules/Companies/Application/Services/CompanyService.cs` — EnsureDefaultHoldingAsync
- `src/frontend/lib/api.ts` — more CRUD wrappers + types
- `src/frontend/app/(authenticated)/inventory/items/new/page.tsx` — enum values
- `src/frontend/app/(authenticated)/projects/new/page.tsx` — cost center dropdown
- `src/frontend/app/(authenticated)/procurement/vendors/page.tsx` — edit/delete

## Verified
- Backend build: 0 errors
- Frontend build: 65/65 pages
