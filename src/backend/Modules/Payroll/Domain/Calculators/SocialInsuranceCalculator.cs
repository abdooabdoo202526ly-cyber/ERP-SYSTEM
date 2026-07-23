namespace ERPSystem.Modules.Payroll.Domain.Calculators;

/// <summary>
/// عقد حاسبة التأمينات الاجتماعية الليبية.
/// حصة الموظف: 3.75% من الـ Gross.
/// حصة صاحب العمل: 7.5% من الـ Gross (لا تُخصم من راتب الموظف).
/// </summary>
public interface ISocialInsuranceCalculator
{
    /// <summary>حصة الموظف (تخصم من الراتب).</summary>
    decimal EmployeeContribution(decimal monthlyGross);

    /// <summary>حصة صاحب العمل (لا تظهر في payslip الموظف — للتقرير فقط).</summary>
    decimal EmployerContribution(decimal monthlyGross);

    /// <summary>إجمالي الحصة (للـ reporting).</summary>
    decimal TotalContribution(decimal monthlyGross);
}

/// <summary>
/// تنفيذ حاسبة التأمينات الاجتماعية.
/// المعادلات حسب قانون العمل الليبي وهيئة التأمينات الاجتماعية الليبية (2024).
/// </summary>
public sealed class SocialInsuranceCalculator : ISocialInsuranceCalculator
{
    /// <summary>نسبة حصة الموظف (3.75%).</summary>
    private const decimal EmployeeRate = 0.0375m;
    /// <summary>نسبة حصة صاحب العمل (7.5%).</summary>
    private const decimal EmployerRate = 0.075m;

    public decimal EmployeeContribution(decimal monthlyGross)
    {
        if (monthlyGross <= 0) return 0m;
        return Math.Round(monthlyGross * EmployeeRate, 4, MidpointRounding.AwayFromZero);
    }

    public decimal EmployerContribution(decimal monthlyGross)
    {
        if (monthlyGross <= 0) return 0m;
        return Math.Round(monthlyGross * EmployerRate, 4, MidpointRounding.AwayFromZero);
    }

    public decimal TotalContribution(decimal monthlyGross)
    {
        return EmployeeContribution(monthlyGross) + EmployerContribution(monthlyGross);
    }
}