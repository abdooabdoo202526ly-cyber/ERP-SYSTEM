using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;
using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Domain.Calculators;

namespace ERPSystem.Modules.Payroll.Application.Services;

/// <summary>
/// عقد خدمة EOS — حساب مستحقات نهاية الخدمة.
/// قراءة فقط (لا تكتب في DB). تُستخدم للـ preview قبل الترحيل الفعلي.
/// </summary>
public interface IEosService
{
    /// <summary>يحسب EOS لموظف بناءً على تاريخ تركه للخدمة (أو اليوم).</summary>
    Task<PayrollResult<EosResponse>> CalculateEosAsync(Guid tenantId, Guid employeeId, DateTime? terminationDate, CancellationToken ct);
}

/// <summary>
/// تنفيذ خدمة EOS — يستخدم IEosCalculator للحساب ويجلب بيانات الموظف من HR.
/// </summary>
public sealed class EosService : IEosService
{
    private readonly IEmployeeRepository _employees;
    private readonly IEosCalculator _calculator;

    public EosService(IEmployeeRepository employees, IEosCalculator calculator)
    {
        _employees = employees; _calculator = calculator;
    }

    public async Task<PayrollResult<EosResponse>> CalculateEosAsync(Guid tenantId, Guid employeeId, DateTime? terminationDate, CancellationToken ct)
    {
        var emp = await _employees.GetByIdAsync(employeeId, ct);
        if (emp == null || emp.TenantId != tenantId)
            return PayrollResult<EosResponse>.Fail("الموظف غير موجود.", PayrollErrorCode.NotFound);

        var termDate = terminationDate ?? DateTime.UtcNow;
        if (termDate.Date < emp.HireDate.Date)
            return PayrollResult<EosResponse>.Fail(
                "تاريخ النهاية يجب أن يكون >= تاريخ التعيين.",
                PayrollErrorCode.ValidationError);

        var years = _calculator.CalculateYearsOfService(emp.HireDate, termDate);
        var eos = _calculator.Calculate(emp.BaseSalary, years);

        var formula = years <= 5
            ? $"{emp.BaseSalary:N4} × {years:N2} = {eos:N4} LYD"
            : $"{emp.BaseSalary:N4} × 5 + ({emp.BaseSalary:N4} × 2 × {years - 5m:N2}) = {eos:N4} LYD";

        return PayrollResult<EosResponse>.Ok(new EosResponse
        {
            EmployeeId = emp.Id,
            EmployeeNumber = emp.EmployeeNumber,
            EmployeeName = emp.FullName,
            HireDate = emp.HireDate,
            TerminationDate = termDate,
            YearsOfService = years,
            MonthlySalary = emp.BaseSalary,
            EosAmount = eos,
            Formula = formula
        });
    }
}