using ERPSystem.Modules.Payroll.Domain.Calculators;
using FluentAssertions;

namespace ERPSystem.Tests.Payroll;

/// <summary>
/// اختبارات حاسبة التأمينات الاجتماعية الليبية.
/// حصة الموظف: 3.75% من الـ Gross.
/// حصة صاحب العمل: 7.5% من الـ Gross (لا تخصم من الموظف).
/// الإجمالي: 11.25%.
/// </summary>
public class SocialInsuranceCalculatorTests
{
    private readonly ISocialInsuranceCalculator _calc = new SocialInsuranceCalculator();

    // ---------- Edge cases ----------

    [Fact]
    public void EmployeeContribution_ZeroSalary_ReturnsZero()
    {
        _calc.EmployeeContribution(0m).Should().Be(0m);
    }

    [Fact]
    public void EmployeeContribution_NegativeSalary_ReturnsZero()
    {
        _calc.EmployeeContribution(-100m).Should().Be(0m);
    }

    [Fact]
    public void EmployerContribution_ZeroSalary_ReturnsZero()
    {
        _calc.EmployerContribution(0m).Should().Be(0m);
    }

    [Fact]
    public void TotalContribution_ZeroSalary_ReturnsZero()
    {
        _calc.TotalContribution(0m).Should().Be(0m);
    }

    // ---------- Spec compliance ----------

    [Fact]
    public void EmployeeContribution_1kSalary_Equals375()
    {
        // 3.75% × 1,000 = 37.5
        _calc.EmployeeContribution(1_000m).Should().Be(37.5m);
    }

    [Fact]
    public void EmployerContribution_1kSalary_Equals75()
    {
        // 7.5% × 1,000 = 75
        _calc.EmployerContribution(1_000m).Should().Be(75m);
    }

    [Fact]
    public void TotalContribution_1kSalary_EqualsEmployeePlusEmployer()
    {
        var total = _calc.TotalContribution(1_000m);
        total.Should().Be(112.5m, "11.25% من الراتب");
        total.Should().Be(_calc.EmployeeContribution(1_000m) + _calc.EmployerContribution(1_000m));
    }

    [Fact]
    public void EmployerContribution_AlwaysDoubleEmployee()
    {
        foreach (var salary in new[] { 500m, 1_000m, 2_500m, 10_000m })
        {
            _calc.EmployerContribution(salary).Should().Be(2m * _calc.EmployeeContribution(salary),
                $"employer rate (7.5%) is exactly 2x employee rate (3.75%)");
        }
    }

    // ---------- Theories مع رواتب متنوعة ----------

    [Theory]
    [InlineData(500, 18.75, 37.5, 56.25)]
    [InlineData(1_000, 37.5, 75, 112.5)]
    [InlineData(2_000, 75, 150, 225)]
    [InlineData(2_500, 93.75, 187.5, 281.25)]
    [InlineData(5_000, 187.5, 375, 562.5)]
    [InlineData(10_000, 375, 750, 1125)]
    [InlineData(25_000, 937.5, 1875, 2812.5)]
    public void Contributions_VariousSalaries_ApplyCorrectRates(
        double salary, double employee, double employer, double total)
    {
        var s = (decimal)salary;
        _calc.EmployeeContribution(s).Should().Be((decimal)employee);
        _calc.EmployerContribution(s).Should().Be((decimal)employer);
        _calc.TotalContribution(s).Should().Be((decimal)total);
    }

    [Fact]
    public void TotalContribution_AlwaysEquals1125Percent()
    {
        // مجموع المعدّلين: 3.75% + 7.5% = 11.25%
        foreach (var salary in new[] { 100m, 1_000m, 10_000m, 100_000m })
        {
            var total = _calc.TotalContribution(salary);
            total.Should().Be(salary * 0.1125m,
                $"total rate = 11.25% regardless of salary");
        }
    }

    [Fact]
    public void EmployeeContribution_RoundsToFourDecimalPlaces()
    {
        // salary=333.33 → 333.33 × 0.0375 = 12.499875 → يقرب إلى 12.4999
        var result = _calc.EmployeeContribution(333.33m);
        result.Should().Be(12.4999m);
    }
}
