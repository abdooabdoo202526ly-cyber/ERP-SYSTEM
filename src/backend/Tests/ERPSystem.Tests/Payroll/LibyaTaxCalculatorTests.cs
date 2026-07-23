using ERPSystem.Modules.Payroll.Domain.Calculators;
using FluentAssertions;

namespace ERPSystem.Tests.Payroll;

/// <summary>
/// اختبارات حاسبة ضريبة الدخل الليبية (GDT brackets).
/// الأقواس:
///   0 – 12,000 LYD/year @ 5%
///   12,001 – 24,000 LYD/year @ 10%
///   24,001+ LYD/year @ 10% (flat)
/// الراتب الشهري → سنوي → tax/12.
/// </summary>
public class LibyaTaxCalculatorTests
{
    private readonly ILibyaTaxCalculator _calc = new LibyaTaxCalculator();

    // ---------- Boundary / edge cases (Facts) ----------

    [Fact]
    public void CalculateMonthlyTax_ZeroSalary_ReturnsZero()
    {
        _calc.CalculateMonthlyTax(0m).Should().Be(0m);
    }

    [Fact]
    public void CalculateMonthlyTax_NegativeSalary_ReturnsZero()
    {
        _calc.CalculateMonthlyTax(-100m).Should().Be(0m);
    }

    [Fact]
    public void CalculateAnnualTax_Zero_ReturnsZero()
    {
        _calc.CalculateAnnualTax(0m).Should().Be(0m);
    }

    [Fact]
    public void CalculateAnnualTax_Negative_ReturnsZero()
    {
        _calc.CalculateAnnualTax(-1m).Should().Be(0m);
    }

    [Fact]
    public void CalculateAnnualTax_ExactlyAtBracket1Boundary()
    {
        // 12,000 LYD @ 5% = 600
        _calc.CalculateAnnualTax(12_000m).Should().Be(600m);
    }

    [Fact]
    public void CalculateAnnualTax_ExactlyAtBracket2Boundary()
    {
        // 5% × 12,000 = 600
        // + 10% × (24,000 - 12,000) = 1,200
        // = 1,800
        _calc.CalculateAnnualTax(24_000m).Should().Be(1_800m);
    }

    [Fact]
    public void CalculateAnnualTax_Bracket1Only()
    {
        // 6,000 LYD @ 5% = 300
        _calc.CalculateAnnualTax(6_000m).Should().Be(300m);
    }

    [Fact]
    public void CalculateAnnualTax_InBracket2()
    {
        // 18,000 LYD: 5%×12,000 + 10%×6,000 = 600 + 600 = 1,200
        _calc.CalculateAnnualTax(18_000m).Should().Be(1_200m);
    }

    [Fact]
    public void CalculateAnnualTax_InBracket3_30k()
    {
        // 30,000 LYD: 600 + 1,200 + 10%×6,000 = 600 + 1,200 + 600 = 2,400
        _calc.CalculateAnnualTax(30_000m).Should().Be(2_400m);
    }

    [Fact]
    public void CalculateAnnualTax_50k()
    {
        // 50,000: 600 + 1,200 + 10%×26,000 = 600 + 1,200 + 2,600 = 4,400
        _calc.CalculateAnnualTax(50_000m).Should().Be(4_400m);
    }

    [Fact]
    public void CalculateMonthlyTax_MatchesAnnualDividedBy12_ForTypicalSalary()
    {
        // الراتب الشهري 1,500 LYD → سنوي 18,000 → 1,200 سنوي → 100 شهرياً
        var annual = _calc.CalculateAnnualTax(18_000m);
        var monthly = _calc.CalculateMonthlyTax(1_500m);
        monthly.Should().Be(Math.Round(annual / 12m, 4, MidpointRounding.AwayFromZero));
    }

    // ---------- Theories: رواتب متنوعة ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 5)]          // annual=1,200 → 5%×1,200=60 → /12=5
    [InlineData(500, 25)]         // annual=6,000 → 5%×6,000=300 → /12=25
    [InlineData(1_000, 50)]       // annual=12,000 → 600 → /12=50
    [InlineData(1_500, 100)]      // annual=18,000 → 1,200 → /12=100
    [InlineData(2_000, 150)]      // annual=24,000 → 1,800 → /12=150
    [InlineData(3_000, 250)]      // annual=36,000 → 600+1,200+10%×12,000=3,000 → /12=250
    [InlineData(5_000, 450)]      // annual=60,000 → 600+1,200+10%×36,000=5,400 → /12=450
    [InlineData(10_000, 950)]     // annual=120,000 → 600+1,200+10%×96,000=11,400 → /12=950
    [InlineData(50_000, 4_950)]   // annual=600,000 → 600+1,200+10%×576,000=59,400 → /12=4,950
    public void CalculateMonthlyTax_VariousSalaries_ReturnsExpected(double monthlyGross, double expectedTax)
    {
        // المقارنة بـ decimal عالية الدقة مع tolerance صغير (4 منازل عشرية).
        var result = _calc.CalculateMonthlyTax((decimal)monthlyGross);
        var expected = (decimal)expectedTax;
        result.Should().BeApproximately(expected, 0.01m,
            $"Monthly gross {monthlyGross} LYD should produce ~{expected} LYD monthly tax");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(6_000, 300)]      // قوس 1 فقط
    [InlineData(12_000, 600)]     // حد القوس 1
    [InlineData(12_001, 600.1)]   // أول قرش في القوس 2
    [InlineData(18_000, 1_200)]
    [InlineData(24_000, 1_800)]
    [InlineData(24_001, 1_800.1)]
    [InlineData(36_000, 3_000)]
    [InlineData(60_000, 5_400)]
    [InlineData(120_000, 11_400)]
    public void CalculateAnnualTax_VariousAmounts_ReturnsExpected(double annualGross, double expectedTax)
    {
        var result = _calc.CalculateAnnualTax((decimal)annualGross);
        var expected = (decimal)expectedTax;
        result.Should().BeApproximately(expected, 0.05m);
    }

    [Fact]
    public void CalculateMonthlyTax_Progressive_NeverExceedsFlat10PercentOfGross()
    {
        // الـ effective rate لا يجب أن يتجاوز 10% (cap بمعدّل القوس الثالث).
        foreach (var salary in new[] { 1_000m, 5_000m, 10_000m, 50_000m, 100_000m })
        {
            var tax = _calc.CalculateMonthlyTax(salary);
            var effectiveRate = tax / salary;
            effectiveRate.Should().BeLessThanOrEqualTo(0.10m,
                $"effective rate at {salary} should not exceed 10% (GDT flat bracket cap)");
        }
    }
}
