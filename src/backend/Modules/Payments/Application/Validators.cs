using ERPSystem.Modules.Payments.Application;
using ERPSystem.Modules.Payments.Entities;
using FluentValidation;

namespace ERPSystem.Modules.Payments.Application;

/// <summary>
/// FluentValidation rules لـ CreatePaymentRequest.
/// اسم الـ class ينتهي بـ RequestValidator لاكتشافه عبر
/// AddValidatorsFromAssemblyContaining في Program.cs.
/// </summary>
public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.PartyType)
            .NotEmpty().WithMessage("نوع الطرف مطلوب.")
            .Must(t => PaymentPartyTypes.All.Contains(t))
            .WithMessage($"نوع الطرف يجب أن يكون واحداً من: {string.Join(", ", PaymentPartyTypes.All)}");

        RuleFor(x => x.PartyId)
            .NotEqual(Guid.Empty).WithMessage("معرّف الطرف مطلوب.");

        RuleFor(x => x.PaymentDate)
            .NotEqual(default(DateTime)).WithMessage("تاريخ الدفع مطلوب.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("المبلغ يجب أن يكون أكبر من صفر.");

        RuleFor(x => x.CurrencyCode)
            .NotEmpty().Length(3).WithMessage("رمز العملة يجب أن يكون 3 أحرف (مثل LYD).");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().Must(m => PaymentMethods.All.Contains(m))
            .WithMessage($"طريقة الدفع يجب أن تكون: {string.Join(", ", PaymentMethods.All)}");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).When(x => !string.IsNullOrEmpty(x.Notes));

        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.RefType)
                .NotEmpty().Must(t => PaymentRefTypes.All.Contains(t))
                .WithMessage($"نوع المرجع يجب أن يكون: {string.Join(", ", PaymentRefTypes.All)}");
            a.RuleFor(x => x.RefId).NotEqual(Guid.Empty);
            a.RuleFor(x => x.AmountApplied).GreaterThan(0);
        });
    }
}

public sealed class AllocatePaymentRequestValidator : AbstractValidator<AllocatePaymentRequest>
{
    public AllocatePaymentRequestValidator()
    {
        RuleFor(x => x.Allocations)
            .NotEmpty().WithMessage("يجب تمرير تخصيص واحد على الأقل.");
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.RefType)
                .NotEmpty().Must(t => PaymentRefTypes.All.Contains(t))
                .WithMessage($"نوع المرجع يجب أن يكون: {string.Join(", ", PaymentRefTypes.All)}");
            a.RuleFor(x => x.RefId).NotEqual(Guid.Empty);
            a.RuleFor(x => x.AmountApplied).GreaterThan(0);
        });
    }
}
