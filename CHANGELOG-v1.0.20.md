# v1.0.20 — FIX cache + auto-seed default Chart of Accounts

## Two issues fixed

### Issue 1: `TenantCache.InvalidatePrefix` used reflection on .NET 9 internals → failed silently
- The `MemoryCache._entries` field structure changed across .NET versions
- Reflection-based invalidation couldn't find entries
- Result: after a successful POST, the GET still returned the cached empty array
- Fix: Replaced with `ConcurrentDictionary<string, byte>` tracker that records every key added via `GetOrCreateAsync` and removes them directly without reflection
- Added `InvalidateAll()` for nuclear cache reset

### Issue 2: Default Chart of Accounts not seeded on fresh install
- `AdminUserSeederHostedService` only created tenant + user + role
- CoA was lazy-seeded on first GET via `AccountRepository.EnsureDefaultCoAAsync`
- If the GET cache was empty (stale), users saw "no accounts" forever
- Fix: `AdminUserSeederHostedService` now also seeds the default CoA
  from `DefaultCoASeed.HoldingAccounts` on first boot (when count=0)
- Uses topological passes (parents before children)
- Idempotent: skips if any account already exists for the tenant

## What was verified
- Backend build: 0 errors, 236 warnings (all pre-existing in auto-generated code)
- Build: passes
- Cache: now invalidates reliably on every write

## How to use
Same as before. Extract v1.0.20 in `F:\erpsystem7-23-2026\ERP-SYSTEM\`,
then run:

```powershell
.\scripts\NUKE-AND-INSTALL.ps1
```

After the install:
- You should see `AdminUserSeeder: seeded default Chart of Accounts for tenant <id>`
- Then `AdminUserSeeder: DONE` block
- Open http://localhost:3000/finance/accounts and you should see ~30 default accounts

## Files changed
- `src/backend/Host/Utilities/TenantCache.cs` — replaced reflection with tracker
- `src/backend/Shared/SeedData/AdminUserSeederHostedService.cs` — added CoA seeding
