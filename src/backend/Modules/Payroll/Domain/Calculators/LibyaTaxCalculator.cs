namespace ERPSystem.Modules.Payroll.Domain.Calculators;

/// <summary>
/// عقد حاسبة ضريبة الدخل الليبية.
/// الدخل يُمرَّخ سنوياً (12 شهراً) ويُقسَم على 12 لاحتساب الضريبة الشهرية.
/// </summary>
public interface ILibyaTaxCalculator
{
    /// <summary>
    /// يحسب ضريبة الدخل السنوية لراتب شهري معطى.
    /// </summary>
    /// <param name="monthlyGross">إجمالي الراتب الشهري (قبل خصم التأمينات).</param>
    /// <returns>مبلغ الضريبة الشهرية (0 إذا الراتب ≤ 0).</returns>
    decimal CalculateMonthlyTax(decimal monthlyGross);

    /// <summary>
    /// يحسب ضريبة الدخل السنوية لإجمالي سنوي.
    /// مفيد للـ EOS أو الـ reporting.
    /// </summary>
    decimal CalculateAnnualTax(decimal annualGross);
}

/// <summary>
/// تنفيذ حاسبة الضريبة الليبية حسب مصلحة الضرائب الليبية (GDT).
///
/// الأقواس (Brackets):
/// - 0    – 12,000 LYD/year → 5%
/// - 12,001 – 24,000 LYD/year → 10%
/// - 24,001+ LYD/year → 10% (flat — Libyan law effective 2024+)
///
/// الحساب progressive: كل قوس يُطبَّق فقط على الجزء الواقع داخله.
/// </summary>
public sealed class LibyaTaxCalculator : ILibyaTaxCalculator
{
    /// <summary>حد القوس الأول (سنوياً).</summary>
    private const decimal Bracket1Max = 12_000m;
    /// <summary>حد القوس الثاني (سنوياً).</summary>
    private const decimal Bracket2Max = 24_000m;
    /// <summary>معدل القوس الأول.</summary>
    private const decimal Rate1 = 0.05m;
    /// <summary>معدل القوس الثاني والثالث (نفس المعدّل).</summary>
    private const decimal Rate2 = 0.10m;
    /// <summary>عدد الأشهر في السنة.</summary>
    private const int MonthsPerYear = 12;

    public decimal CalculateAnnualTax(decimal annualGross)
    {
        if (annualGross <= 0) return 0m;

        decimal tax = 0m;

        // القوس الأول: 0 → 12,000 @ 5%
        var inBracket1 = Math.Min(annualGross, Bracket1Max);
        tax += inBracket1 * Rate1;

        // القوس الثاني: 12,000 → 24,000 @ 10%
        if (annualGross > Bracket1Max)
        {
            var inBracket2 = Math.Min(annualGross, Bracket2Max) - Bracket1Max;
            if (inBracket2 > 0) tax += inBracket2 * Rate2;
        }

        // القوس الثالث: > 24,000 @ 10% (flat)
        if (annualGross > Bracket2Max)
        {
            var inBracket3 = annualGross - Bracket2Max;
            tax += inBracket3 * Rate2;
        }

        // تقريب لأقرب فلس (4 منازل عشرية — مطابق Decimal(18,4) في الـ DB).
        return Math.Round(tax, 4, MidpointRounding.AwayFromZero);
    }

    public decimal CalculateMonthlyTax(decimal monthlyGross)
    {
        if (monthlyGross <= 0) return 0m;
        var annual = CalculateAnnualTax(monthlyGross * MonthsPerYear);
        return Math.Round(annual / MonthsPerYear, 4, MidpointRounding.AwayFromZero);
    }
}