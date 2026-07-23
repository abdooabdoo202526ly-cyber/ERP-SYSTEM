using ERPSystem.Modules.Payroll.Domain.Calculators;
using FluentAssertions;

namespace ERPSystem.Tests.Payroll;

/// <summary>
/// اختبارات حاسبة نهاية الخدمة (EOS) حسب قانون العمل الليبي.
/// سنوات ≤ 5: salary × years × 1
/// سنوات > 5: salary × 5 + salary × 2 × (years - 5)
/// </summary>
public class EosCalculatorTests
{
    private readonly IEosCalculator _calc = new EosCalculator();

    // ---------- Edge cases ----------

    [Fact]
    public void Calculate_ZeroSalary_ReturnsZero()
    {
        _calc.Calculate(0m, 5m).Should().Be(0m);
    }

    [Fact]
    public void Calculate_NegativeSalary_ReturnsZero()
    {
        _calc.Calculate(-100m, 5m).Should().Be(0m);
    }

    [Fact]
    public void Calculate_ZeroYears_ReturnsZero()
    {
        _calc.Calculate(1_000m, 0m).Should().Be(0m);
    }

    [Fact]
    public void Calculate_NegativeYears_ReturnsZero()
    {
        _calc.Calculate(1_000m, -1m).Should().Be(0m);
    }

    // ---------- First bracket: years <= 5 ----------

    [Fact]
    public void Calculate_OneYear_FirstBracket()
    {
        // 1,000 × 1 × 1 = 1,000
        _calc.Calculate(1_000m, 1m).Should().Be(1_000m);
    }

    [Fact]
    public void Calculate_ThreeYears_FirstBracket()
    {
        // 1,000 × 3 × 1 = 3,000
        _calc.Calculate(1_000m, 3m).Should().Be(3_000m);
    }

    [Fact]
    public void Calculate_Exactly5Years_FirstBracketEdge()
    {
        // 1,000 × 5 × 1 = 5,000
        _calc.Calculate(1_000m, 5m).Should().Be(5_000m);
    }

    // ---------- Second bracket: years > 5 ----------

    [Fact]
    public void Calculate_SixYears_SecondBracketStarts()
    {
        // 1,000×5 + 1,000×2×(6-5) = 5,000 + 2,000 = 7,000
        _calc.Calculate(1_000m, 6m).Should().Be(7_000m);
    }

    [Fact]
    public void Calculate_TenYears()
    {
        // 1,000×5 + 1,000×2×(10-5) = 5,000 + 10,000 = 15,000
        _calc.Calculate(1_000m, 10m).Should().Be(15_000m);
    }

    [Fact]
    public void Calculate_TwentyYears()
    {
        // 1,000×5 + 1,000×2×(20-5) = 5,000 + 30,000 = 35,000
        _calc.Calculate(1_000m, 20m).Should().Be(35_000m);
    }

    [Fact]
    public void Calculate_LongService()
    {
        // 2,000 LYD × 30 yrs: 2,000×5 + 2,000×2×25 = 10,000 + 100,000 = 110,000
        _calc.Calculate(2_000m, 30m).Should().Be(110_000m);
    }

    // ---------- Fractional years ----------

    [Fact]
    public void Calculate_FractionalYears_FirstBracket()
    {
        // 1,000 × 0.5 = 500
        _calc.Calculate(1_000m, 0.5m).Should().Be(500m);
    }

    [Fact]
    public void Calculate_FractionalYears_SecondBracket()
    {
        // 1,000×5 + 1,000×2×(5.5-5) = 5,000 + 1,000 = 6,000
        _calc.Calculate(1_000m, 5.5m).Should().Be(6_000m);
    }

    // ---------- CalculateYearsOfService ----------

    [Fact]
    public void CalculateYearsOfService_TerminationBeforeHire_ReturnsZero()
    {
        var hire = new DateTime(2024, 6, 1);
        var term = new DateTime(2024, 1, 1);
        _calc.CalculateYearsOfService(hire, term).Should().Be(0m);
    }

    [Fact]
    public void CalculateYearsOfService_EqualDates_ReturnsZero()
    {
        var d = new DateTime(2024, 6, 1);
        _calc.CalculateYearsOfService(d, d).Should().Be(0m);
    }

    [Fact]
    public void CalculateYearsOfService_OneYear_ApproxOne()
    {
        var hire = new DateTime(2020, 1, 1);
        var term = new DateTime(2021, 1, 1);
        _calc.CalculateYearsOfService(hire, term).Should().BeApproximately(1m, 0.01m);
    }

    [Fact]
    public void CalculateYearsOfService_FiveYears_ApproxFive()
    {
        var hire = new DateTime(2019, 3, 15);
        var term = new DateTime(2024, 3, 15);
        _calc.CalculateYearsOfService(hire, term).Should().BeApproximately(5m, 0.01m);
    }

    [Fact]
    public void CalculateYearsOfService_TenYears_ApproxTen()
    {
        var hire = new DateTime(2014, 1, 1);
        var term = new DateTime(2024, 1, 1);
        _calc.CalculateYearsOfService(hire, term).Should().BeApproximately(10m, 0.01m);
    }

    [Fact]
    public void CalculateYearsOfService_HalfYear_Approx05()
    {
        var hire = new DateTime(2024, 1, 1);
        var term = new DateTime(2024, 7, 2);  // ~183 days
        var result = _calc.CalculateYearsOfService(hire, term);
        result.Should().BeApproximately(0.5m, 0.01m);
    }

    // ---------- Theory: سنوات متنوعة مع EOS ----------

    [Theory]
    [InlineData(500, 1, 500)]       // قوس 1
    [InlineData(500, 2, 1_000)]
    [InlineData(500, 5, 2_500)]     // حد القوس 1
    [InlineData(500, 6, 3_500)]     // قوس 2: 500×5 + 500×2×1 = 3,500
    [InlineData(500, 7, 4_500)]
    [InlineData(500, 10, 7_500)]
    [InlineData(1_000, 3, 3_000)]
    [InlineData(1_000, 5, 5_000)]
    [InlineData(1_000, 8, 11_000)]  // 5,000 + 2,000×3 = 11,000
    [InlineData(1_000, 15, 25_000)] // 5,000 + 2,000×10 = 25,000
    [InlineData(2_500, 12, 47_500)] // 12,500 + 5,000×7 = 47,500
    public void Calculate_VariousSalariesAndYears(double salary, double years, double expected)
    {
        _calc.Calculate((decimal)salary, (decimal)years).Should().Be((decimal)expected);
    }

    [Fact]
    public void Calculate_Formula_At5YearsBoundary_UsesFirstBracket()
    {
        // التوثيق يذكر: سنوات ≤ 5 → قوس 1
        var result5 = _calc.Calculate(1_000m, 5m);
        var result5Plus1 = _calc.Calculate(1_000m, 5.0001m);
        // 5,000 vs ~5,000.2 — الفارق يعكس انتقال القوس
        result5.Should().Be(5_000m);
        result5Plus1.Should().BeGreaterThan(5_000m);
    }
}
