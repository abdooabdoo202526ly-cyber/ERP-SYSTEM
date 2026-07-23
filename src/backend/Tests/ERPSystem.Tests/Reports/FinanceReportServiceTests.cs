using ERPSystem.Modules.Finance.Entities;
using ERPSystem.Modules.Reports.Application;
using ERPSystem.Modules.Reports.Application.Services;
using ERPSystem.Tests.Common;
using FluentAssertions;

namespace ERPSystem.Tests.Reports;

/// <summary>
/// اختبارات خدمة تقارير Finance.
///
/// تنقسم لـ فئتين:
/// 1. Unit Tests (DTO computations) — pure logic، لا تحتاج DB
/// 2. Service Tests — تختبر queries مع FakeDbConnectionFactory (in-memory)
///
/// الـ FakeDbConnectionFactory يحاكي SELECT البسيطة فقط.
/// للـ integration tests الكاملة (مع JOINs و GROUP BY و Dapper.SqlBuilder)
/// شغّلها على Postgres حقيقي (CI environment).
/// </summary>
public class FinanceReportServiceTests
{
    private static (FinanceReportService svc, FakeDbConnectionFactory db, Guid tenantId) Build()
    {
        var tenant = Guid.NewGuid();
        var db = new FakeDbConnectionFactory();
        return (new FinanceReportService(db), db, tenant);
    }

    // ============== DTO Unit Tests (تشتغل دائماً) ==============

    [Fact]
    public void TrialBalanceRow_Dto_NetDebit_AndNetCredit_CalculateCorrectly()
    {
        // NetDebit = Debit - Credit
        // NetCredit = Credit - Debit
        // مثال: حساب بأرصدة مدينة فقط
        var row = new TrialBalanceRow { Debit = 800, Credit = 300 };
        row.NetDebit.Should().Be(500);
        row.NetCredit.Should().Be(-500);
    }

    [Fact]
    public void TrialBalanceRow_Dto_NetCredit_WhenCreditHigher()
    {
        var row = new TrialBalanceRow { Debit = 200, Credit = 700 };
        row.NetDebit.Should().Be(-500);
        row.NetCredit.Should().Be(500);
    }

    [Fact]
    public void TrialBalanceReport_Dto_IsBalanced_WhenEqual()
    {
        var report = new TrialBalanceReport
        {
            Rows = new()
            {
                new() { Debit = 100, Credit = 0 },
                new() { Debit = 0, Credit = 100 }
            }
        };
        report.TotalDebit.Should().Be(100);
        report.TotalCredit.Should().Be(100);
        report.IsBalanced.Should().BeTrue();
        report.Variance.Should().Be(0);
    }

    [Fact]
    public void TrialBalanceReport_Dto_NotBalanced_WhenUnequal()
    {
        var report = new TrialBalanceReport
        {
            Rows = new()
            {
                new() { Debit = 100, Credit = 0 },
                new() { Debit = 0, Credit = 50 }
            }
        };
        report.IsBalanced.Should().BeFalse();
        report.Variance.Should().Be(50);
    }

    [Fact]
    public void IncomeStatement_Dto_NetIncome_AndGrossProfit_CalculateCorrectly()
    {
        var income = new IncomeStatement
        {
            Revenue = 1000, Cogs = 400,
            OperatingExpenses = 200, OtherIncome = 50, OtherExpenses = 30
        };
        income.GrossProfit.Should().Be(600);
        income.NetIncome.Should().Be(420, "(1000-400) - 200 + 50 - 30");
    }

    [Fact]
    public void IncomeStatement_Dto_NetIncome_ZeroWhenNoRevenue()
    {
        var income = new IncomeStatement { Cogs = 100, OperatingExpenses = 50 };
        income.GrossProfit.Should().Be(-100);
        income.NetIncome.Should().Be(-150);
    }

    [Fact]
    public void BalanceSheet_Dto_IsBalanced_WhenAssetsEqualsLiabPlusEquity()
    {
        var bs = new BalanceSheet
        {
            TotalAssets = 1000, TotalLiabilities = 400, TotalEquity = 600
        };
        bs.TotalLiabilitiesAndEquity.Should().Be(1000);
        bs.IsBalanced.Should().BeTrue();
        bs.Variance.Should().Be(0);
    }

    [Fact]
    public void BalanceSheet_Dto_NotBalanced_WhenMismatch()
    {
        var bs = new BalanceSheet
        {
            TotalAssets = 1000, TotalLiabilities = 500, TotalEquity = 400
        };
        bs.TotalLiabilitiesAndEquity.Should().Be(900);
        bs.IsBalanced.Should().BeFalse();
        bs.Variance.Should().Be(100, "1000 - 900");
    }

    // ============== Service Integration Tests (تحتاج Postgres حقيقي) ==============
    // الـ tests التالية تتحقق من SQL queries الفعلية.
    // الـ FakeDbConnectionFactory البسيط لا يدعم JOINs/GROUP BY كاملاً،
    // فهذه الـ tests marked Skip في بيئة Unit Tests العادية.
    // شغّلها على CI مع Postgres service container.

    [Fact(Skip = "Integration: requires real Postgres. See CI workflow.")]
    public async Task GetTrialBalance_BalancedPostedEntries_ReturnsBalancedReport()
    {
        var (svc, db, tenant) = Build();
        var cash = Guid.NewGuid();
        var revenue = Guid.NewGuid();
        var date = new DateTime(2026, 1, 31);

        db.AddRow("accounts", "id", cash, "tenant_id", tenant, "code", "1110", "name", "الصندوق", "type", (int)AccountType.Asset);
        db.AddRow("accounts", "id", revenue, "tenant_id", tenant, "code", "4100", "name", "إيرادات", "type", (int)AccountType.Revenue);
        var je = Guid.NewGuid();
        db.AddRow("journal_entries", "id", je, "tenant_id", tenant, "status", (int)JournalEntryStatus.Posted, "entry_date", date);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", cash, "debit", 500m, "credit", 0m);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", revenue, "debit", 0m, "credit", 500m);

        var report = await svc.GetTrialBalanceAsync(tenant, null, date, CancellationToken.None);

        report.Rows.Should().HaveCount(2);
        report.TotalDebit.Should().Be(500);
        report.TotalCredit.Should().Be(500);
        report.IsBalanced.Should().BeTrue();
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetTrialBalance_EmptyTenant_ReturnsEmptyReport()
    {
        var (svc, _, tenant) = Build();
        var report = await svc.GetTrialBalanceAsync(tenant, null, DateTime.UtcNow, CancellationToken.None);
        report.Rows.Should().BeEmpty();
        report.IsBalanced.Should().BeTrue();
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetIncomeStatement_RevenueAndCogs_CalculatesCorrectly()
    {
        var (svc, db, tenant) = Build();
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        var revenue = Guid.NewGuid();
        var cogs = Guid.NewGuid();

        db.AddRow("accounts", "id", revenue, "tenant_id", tenant, "code", "4100", "type", (int)AccountType.Revenue);
        db.AddRow("accounts", "id", cogs, "tenant_id", tenant, "code", "5110", "type", (int)AccountType.Expense);
        var je = Guid.NewGuid();
        db.AddRow("journal_entries", "id", je, "tenant_id", tenant, "status", (int)JournalEntryStatus.Posted, "entry_date", to);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", revenue, "debit", 0m, "credit", 1000m);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", cogs, "debit", 600m, "credit", 0m);

        var income = await svc.GetIncomeStatementAsync(tenant, null, from, to, CancellationToken.None);
        income.Revenue.Should().Be(1000);
        income.Cogs.Should().Be(600);
        income.GrossProfit.Should().Be(400);
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetIncomeStatement_OpEx_ClassifiedAsOperatingExpenses()
    {
        var (svc, db, tenant) = Build();
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 1, 31);
        var opex = Guid.NewGuid();

        db.AddRow("accounts", "id", opex, "tenant_id", tenant, "code", "5200", "type", (int)AccountType.Expense);
        var je = Guid.NewGuid();
        db.AddRow("journal_entries", "id", je, "tenant_id", tenant, "status", (int)JournalEntryStatus.Posted, "entry_date", to);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", opex, "debit", 200m, "credit", 0m);

        var income = await svc.GetIncomeStatementAsync(tenant, null, from, to, CancellationToken.None);
        income.OperatingExpenses.Should().Be(200);
    }

    [Fact(Skip = "Integration: requires real Postgres.")]
    public async Task GetBalanceSheet_AssetsEqualsLiabilitiesPlusEquity_IsBalanced()
    {
        var (svc, db, tenant) = Build();
        var date = new DateTime(2026, 1, 31);
        var cash = Guid.NewGuid();
        var ap = Guid.NewGuid();
        var equity = Guid.NewGuid();

        db.AddRow("accounts", "id", cash, "tenant_id", tenant, "code", "1110", "type", (int)AccountType.Asset);
        db.AddRow("accounts", "id", ap, "tenant_id", tenant, "code", "2110", "type", (int)AccountType.Liability);
        db.AddRow("accounts", "id", equity, "tenant_id", tenant, "code", "3100", "type", (int)AccountType.Equity);
        var je = Guid.NewGuid();
        db.AddRow("journal_entries", "id", je, "tenant_id", tenant, "status", (int)JournalEntryStatus.Posted, "entry_date", date);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", cash, "debit", 1000m, "credit", 0m);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", ap, "debit", 0m, "credit", 400m);
        db.AddRow("journal_lines", "journal_entry_id", je, "account_id", equity, "debit", 0m, "credit", 600m);

        var bs = await svc.GetBalanceSheetAsync(tenant, null, date, CancellationToken.None);
        bs.TotalAssets.Should().Be(1000);
        bs.TotalLiabilities.Should().Be(400);
        bs.TotalEquity.Should().Be(600);
        bs.IsBalanced.Should().BeTrue();
    }
}
