# 💰 src/backend/Modules/Finance/AGENTS.md

> Finance Module — ✅ Phase 1 مكتمل (Chart of Accounts + Journal Entries + General Ledger + Rules Engine).
>
> محدّث: 2026-06-24 — إضافة Phase 4 (Payroll uses CoA 4200/1210)

## شو فيه

```
Finance/
├── Entities/
│   ├── Account.cs          # Account, AccountType, NormalBalance
│   ├── JournalEntry.cs     # JournalEntry, JournalLine, JournalEntryStatus
│   └── PostingRule.cs      # PostingRule, TriggeringEvent, PostingRuleTemplate
├── Application/
│   ├── FinanceDtos.cs      # كل DTOs (Account, JournalEntry, Ledger, PostingRule)
│   ├── Validators.cs       # FluentValidation rules
│   └── Services/
│       ├── IChartOfAccountsService.cs    # + FinanceResult<T>, FinanceErrorCode
│       ├── ChartOfAccountsService.cs     # CRUD + Delete
│       ├── IJournalEntryService.cs
│       ├── JournalEntryService.cs       # CreateDraft + Post + List/Get
│       ├── IGeneralLedgerService.cs
│       ├── GeneralLedgerService.cs      # TrialBalance + AccountLedger
│       ├── IPostingRulesService.cs      # + EventPayload
│       └── PostingRulesService.cs       # ApplyRulesAsync (StockReceived → JE)
└── Infrastructure/
    ├── IRepositories.cs
    ├── AccountRepository.cs         # + EnsureDefaultCoAAsync (شجرة 17 حساب)
    ├── JournalEntryRepository.cs    # مع transaction للـ header + lines
    └── PostingRuleRepository.cs
```

## Domain Model

### Account (CoA)
- هيرارشي: `parent_account_id` (self-FK)
- 5 أنواع: Asset, Liability, Equity, Revenue, Expense
- `NormalBalance` يُحسب تلقائياً (Asset/Expense → Debit، الباقي Credit)
- `IsPostable = false` → حساب تجميعي، لا يقبل قيود مباشرة
- **Default CoA لكل tenant جديد** (17 حساب: 1100 النقدية، 4100 إيرادات، إلخ)

### JournalEntry + JournalLine
- رأس القيد: `entry_number` تسلسلي (`JE-2026-0001`)
- سطور: كل سطر `debit` أو `credit` (XOR، لا كلاهما، لا صفر)
- حالات: Draft → Posted → Reversed
- **Double-Entry validation إجباري**: Σ debit = Σ credit (مرتين: عند الإنشاء وعند الترحيل)

### PostingRule (Rules Engine)
- MVP: 1 حدث → N سطور debit/credit
- Template بصيغة JSON (deserializable لـ `PostingRuleTemplate`)
- صيغ مبسطة: `{amount}` → payload.Amount، أرقام خام
- **القاعدة الافتراضية للـ tenant جديد**: StockReceived → 1300 المخزون (مدين) / 2100 الدائنون (دائن)

## Endpoints

| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/finance/accounts` | دليل الحسابات (postable فقط افتراضياً) |
| GET | `/api/finance/accounts/{id}` | تفاصيل حساب |
| GET | `/api/finance/accounts/by-code/{code}` | بالكود (e.g., `1100`) |
| POST | `/api/finance/accounts` | إنشاء حساب |
| DELETE | `/api/finance/accounts/{id}` | soft-delete (IsActive=false) |
| GET | `/api/finance/journal-entries` | قائمة القيود (pagination + filters) |
| GET | `/api/finance/journal-entries/{id}` | تفاصيل قيد + سطوره |
| POST | `/api/finance/journal-entries` | إنشاء Draft (يتحقق من التوازن) |
| POST | `/api/finance/journal-entries/{id}/post` | ترحيل (Draft → Posted) |
| GET | `/api/finance/ledger/trial-balance` | كل الحسابات + أرصدتها |
| GET | `/api/finance/ledger/accounts/{accountId}` | دفتر أستاذ حساب |
| GET | `/api/finance/posting-rules` | قائمة القواعد |
| POST | `/api/finance/posting-rules` | إنشاء قاعدة |
| POST | `/api/finance/posting-rules/trigger/{eventType}` | تشغيل حدث (للاختبار) |

## لما تشتغل هنا

- إضافة نوع حساب جديد: عدّل `AccountType` enum + حدّث `ComputeBalance` في GeneralLedgerService
- إضافة نوع حدث جديد: عدّل `TriggeringEvent` + أضف قاعدة افتراضية في `EnsureDefaultRulesAsync`
- إضافة endpoint جديد: أنشئ method في الـ Service interface + Controller
- **عند إضافة event**: حدّث قسم "Event Integration" أدناه

## بعد التعديل

- شغّل `dotnet test` (28 tests، كلها تخصّ Finance + Identity)
- إذا غيّرت الـ CoA defaults، أضف migration جديدة (لا تعدّل القديمة)
- إذا أضفت field على entity: migration جديدة + update DTOs + validators

## Event Integration (مستقبلي)

- `StockReceived` (من Inventory) → يستدعي `IPostingRulesService.ApplyRulesAsync(tenantId, userId, StockReceived, payload)`
- `StockIssued` → قيد COGS
- `InvoiceCreated` → قيد إيراد
- `PaymentReceived` → قيد تحصيل

التكامل الفعلي (Inventory module) يأتي في Phase 2. حالياً الـ `POST /api/finance/posting-rules/trigger/{eventType}` يتيح المحاكاة.

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Identity/AGENTS.md`](../Identity/AGENTS.md) — أنماط متبعة
- [`../Inventory/AGENTS.md`](../Inventory/AGENTS.md) — التكامل المستقبلي
- [`../../Shared/AGENTS.md`](../../Shared/AGENTS.md) — Migrations (002_CreateFinanceTables)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
