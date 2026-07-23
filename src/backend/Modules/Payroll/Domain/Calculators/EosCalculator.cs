namespace ERPSystem.Modules.Payroll.Domain.Calculators;

/// <summary>
/// عقد حاسبة نهاية الخدمة (End-of-Service / EOS) حسب قانون العمل الليبي.
///
/// الصيغة (MVP — Phase 4):
///   - سنوات الخدمة ≤ 5  → EOS = الراتب الشهري × سنوات الخدمة
///   - سنوات الخدمة > 5  → EOS = الراتب × 5 + (الراتب × 2 × (سنوات - 5))
///
/// استثناءات (resignation vs termination, السنة الجزئية) ستأتي في Phase 4.5.
/// </summary>
public interface IEosCalculator
{
    /// <summary>
    /// يحسب مستحقات نهاية الخدمة بناءً على الراتب الأخير وسنوات الخدمة.
    /// </summary>
    /// <param name="monthlySalary">الراتب الشهري الأخير.</param>
    /// <param name="yearsOfService">سنوات الخدمة (كسرية مقبولة).</param>
    /// <returns>مبلغ EOS (0 إذا الخدمة ≤ 0).</returns>
    decimal Calculate(decimal monthlySalary, decimal yearsOfService);

    /// <summary>
    /// يحسب سنوات الخدمة بين تاريخ التعيين وتاريخ النهاية (أو اليوم).
    /// </summary>
    decimal CalculateYearsOfService(DateTime hireDate, DateTime terminationDate);
}

/// <summary>
/// تنفيذ حاسبة EOS.
/// </summary>
public sealed class EosCalculator : IEosCalculator
{
    /// <summary>سنوات الخدمة التي يتغير بعدها معامل الـ EOS.</summary>
    private const decimal ThresholdYears = 5m;
    /// <summary>معامل السنوات في القوس الأول (1 شهر عن كل سنة).</summary>
    private const decimal FactorFirstBracket = 1m;
    /// <summary>معامل السنوات في القوس الثاني (2 شهر عن كل سنة بعد الـ 5).</summary>
    private const decimal FactorSecondBracket = 2m;

    public decimal Calculate(decimal monthlySalary, decimal yearsOfService)
    {
        if (monthlySalary <= 0 || yearsOfService <= 0) return 0m;

        decimal eos;
        if (yearsOfService <= ThresholdYears)
        {
            // القوس الأول: شهر/سنة × سنوات
            eos = monthlySalary * FactorFirstBracket * yearsOfService;
        }
        else
        {
            // القوس الأول كامل + القوس الثاني (2 شهر/سنة × الباقي)
            eos = (monthlySalary * FactorFirstBracket * ThresholdYears)
                + (monthlySalary * FactorSecondBracket * (yearsOfService - ThresholdYears));
        }

        return Math.Round(eos, 4, MidpointRounding.AwayFromZero);
    }

    public decimal CalculateYearsOfService(DateTime hireDate, DateTime terminationDate)
    {
        if (terminationDate <= hireDate) return 0m;
        // الفرق بالسنوات (كسرية) — Year + (الباقي كنسبة من السنة).
        var totalDays = (terminationDate.Date - hireDate.Date).TotalDays;
        var daysPerYear = 365.25m;
        return Math.Round((decimal)totalDays / daysPerYear, 4, MidpointRounding.AwayFromZero);
    }
}