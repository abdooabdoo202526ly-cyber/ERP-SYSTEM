# v1.0.21 — POST 400 fix + better error handling

## What was wrong
`POST /api/finance/accounts` returned 400 because the frontend form
sends `normalBalance` but the backend `CreateAccountRequest` DTO did
not have a `NormalBalance` field. The JSON deserializer ignored the
extra property, but the form submission never set the value, so the
form's "saved" state and the server's view were inconsistent.

Additionally, the form used raw `fetch()` so any 400 response was
treated as a generic error and the actual validation message was hidden.

## What was fixed

### 1. Backend: accept NormalBalance in CreateAccountRequest
- `CreateAccountRequest` now has an optional `NormalBalance?` field
- `ChartOfAccountsService.CreateAsync` uses the supplied value if
  provided, otherwise derives from `Type` (Asset/Expense -> Debit,
  others -> Credit) as before

### 2. Frontend: use axios for create
- `finance/accounts/new/page.tsx` now calls `financeApi.createAccount`
  instead of raw `fetch`, so the response interceptor can surface
  validation details

### 3. Frontend: surface 400 details
- `api.ts` response interceptor now decodes ASP.NET `ValidationProblemDetails`
  and exposes the field-level error messages via `err.message`
- Forms display these messages in their existing error banner

### 4. financeApi: complete CRUD
- Added `getAccount(id)` and `deleteAccount(id)` to `financeApi`
- Edit page now uses `financeApi.getAccount` and `financeApi.deleteAccount`

## How to use
Same as before. Extract v1.0.21 in `F:\erpsystem7-23-2026\ERP-SYSTEM\`,
then run:

```powershell
.\scripts\NUKE-AND-INSTALL.ps1
```

After install:
1. Login as `admin@alfajr.local / Demo1234`
2. Navigate to `/finance/accounts` — you should see ~30 default accounts
3. Click "+ حساب جديد" — fill code, name, type; submit
4. The form should now succeed and the new account should appear in the list

## Files changed
- `src/backend/Modules/Finance/Application/FinanceDtos.cs` — added NormalBalance
- `src/backend/Modules/Finance/Application/Services/ChartOfAccountsService.cs` — use it
- `src/frontend/lib/api.ts` — interceptor + financeApi.getAccount/deleteAccount
- `src/frontend/app/(authenticated)/finance/accounts/new/page.tsx` — use financeApi
- `src/frontend/app/(authenticated)/finance/accounts/[id]/edit/page.tsx` — use financeApi

## Verified
- Backend build: 0 errors
- Frontend build: 65/65 pages
