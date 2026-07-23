# 🧪 src/backend/Tests/AGENTS.md

> Unit + Integration tests.
>
> محدّث: 2026-06-24 — إضافة Phase 3+ test coverage

## شو فيه

```
Tests/
└── ERPSystem.Tests/
    ├── ERPSystem.Tests.csproj
    └── Auth/
        ├── JwtTokenServiceTests.cs    # Token generation/validation
        └── ValidatorsTests.cs          # FluentValidation rules
```

## Conventions

- **xUnit** (لا NUnit ولا MSTest)
- **FluentAssertions** للـ assertions (`result.Should().Be...`)
- **Naming**: `<Class>Tests` لكل class يُختبر
- **Arrange-Act-Assert** pattern بوضوح
- **Tests مستقلة** — لا تعتمد على ترتيب التنفيذ
- **Mock dependencies** عبر Moq أو NSubstitute (عند الحاجة لـ DB)

## Types of Tests

### Unit Tests (سريع، لا IO)

- Pure logic: Validators، Token generation، Password hashing
- الـ Target: `Application/`, `Shared/`

### Integration Tests (يحتاج DB)

- Repository queries
- Auth flow كامل
- الـ Target: `Infrastructure/`, `Host/Controllers/`
- **يحتاج** Postgres test DB (موجود في CI)

## لما تضيف feature جديد

1. اكتب unit tests للـ services
2. اكتب integration tests للـ endpoints (إن أمكن)
3. تأكد: `dotnet test` يمر محلياً + على CI

## 🆕 Phase 3 / 3.5 / 4 Test Coverage

- **Reports (Phase 2.5):** 20 tests (FinanceReport × 7 + InventoryReport × 7 + ProjectReport × 6)
- **Procurement (Phase 3):** ⚠️ الـ workers ركّزوا على الـ E2E (PowerShell) بدل unit tests — Tests folder لا يحوي procurement tests بعد
- **HR (Phase 3.5):** ⚠️ نفس — يعتمد على E2E
- **Payroll (Phase 4):** ⚠️ الـ 3 Calculators (`LibyaTaxCalculator`, `EosCalculator`, `SocialInsuranceCalculator`) pure logic ومناسبة لـ unit tests لكن لم تُكتب بعد

**التوصية للـ Phase 5:** اكتب unit tests للـ Calculators أولاً (أسهل مكسب — pure logic بدون DB).

## CI Integration

- `dotnet test` يعمل على CI مع Postgres + Redis services
- Test results تُرفع كـ artifact
- Coverage report (مستقبلي)

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`../Host/AGENTS.md`](../Host/AGENTS.md)
- [`../Modules/Identity/AGENTS.md`](../Modules/Identity/AGENTS.md) — Patterns مستهدفة
- [`../Modules/Payroll/AGENTS.md`](../Modules/Payroll/AGENTS.md) — Phase 4 (Calculators)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
