using FluentValidation;

namespace ERPSystem.Modules.Payroll.Application;

/// <summary>تحقّق من طلب إنشاء دورة رواتب — period_start &lt; period_end، future guard اختياري.</summary>
public sealed class CreatePayrollRunRequestValidator : AbstractValidator<CreatePayrollRunRequest>
{
    public CreatePayrollRunRequestValidator()
    {
        RuleFor(x => x.PeriodStart).NotEmpty().WithMessage("تاريخ البداية مطلوب.");
        RuleFor(x => x.PeriodEnd).NotEmpty().WithMessage("تاريخ النهاية مطلوب.")
            .GreaterThanOrEqualTo(x => x.PeriodStart.Date)
            .WithMessage("تاريخ النهاية يجب أن يكون >= تاريخ البداية.");
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

/// <summary>v1.0.26: تحقّق من طلب إنشاء/تحديث هيكل راتب — code فريد، اسم مطلوب، LYD افتراضي للعملة.</summary>
public sealed class CreateSalaryStructureRequestValidator : AbstractValidator<CreateSalaryStructureRequest>
{
    public CreateSalaryStructureRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم هيكل الراتب مطلوب.")
            .MaximumLength(200).WithMessage("الاسم طويل جداً (حد أقصى 200 حرف).");
        RuleFor(x => x.Code).NotEmpty().WithMessage("كود هيكل الراتب مطلوب.")
            .MaximumLength(50).WithMessage("الكود طويل جداً (حد أقصى 50 حرف).")
            .Matches("^[A-Za-z0-9_-]+$").WithMessage("الكود: حروف/أرقام/-/_ فقط.");
        RuleFor(x => x.Currency).MaximumLength(10).WithMessage("العملة طويلة جداً.");
        RuleFor(x => x.Lines).NotNull().WithMessage("قائمة المكوّنات مطلوبة.");
        RuleForEach(x => x.Lines).SetValidator(new CreateSalaryStructureLineRequestValidator());
    }
}

/// <summary>v1.0.26: تحقّق من سطر مكوّن (earning / deduction) لهيكل الراتب.</summary>
public sealed class CreateSalaryStructureLineRequestValidator : AbstractValidator<CreateSalaryStructureLineRequest>
{
    public CreateSalaryStructureLineRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم المكوّن مطلوب.")
            .MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0).WithMessage("قيمة المكوّن يجب أن تكون >= 0.");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}