using FluentValidation;

namespace ERPSystem.Modules.Finance.Application;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("كود الحساب مطلوب.")
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9\\-_]+$").WithMessage("كود الحساب يجب أن يحتوي على حروف وأرقام و - و _ فقط.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("اسم الحساب مطلوب.")
            .MaximumLength(200);

        RuleFor(x => x.Type).Must(t => t != 0).WithMessage("نوع الحساب مطلوب.");
    }
}

public sealed class PostJournalEntryRequestValidator : AbstractValidator<PostJournalEntryRequest>
{
    public PostJournalEntryRequestValidator()
    {
        RuleFor(x => x.EntryDate)
            .NotEqual(default(DateTime)).WithMessage("تاريخ القيد مطلوب.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("وصف القيد مطلوب.")
            .MaximumLength(500);

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("القيد يجب أن يحتوي على سطرين على الأقل.")
            .Must(lines => lines.Count >= 2).WithMessage("القيد يجب أن يحتوي على سطرين على الأقل (مدين + دائن).");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId)
                .NotEqual(Guid.Empty).WithMessage("AccountId مطلوب لكل سطر.");

            // debit XOR credit: واحد فقط > 0
            line.RuleFor(l => l)
                .Must(l => (l.Debit > 0 && l.Credit == 0) || (l.Credit > 0 && l.Debit == 0))
                .WithMessage("كل سطر يجب أن يكون إما مدين أو دائن (ليس الاثنين، ولا صفر).");

            line.RuleFor(l => l.Debit).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.Credit).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreatePostingRuleRequestValidator : AbstractValidator<CreatePostingRuleRequest>
{
    public CreatePostingRuleRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EventType).Must(e => e != 0).WithMessage("نوع الحدث مطلوب.");
        RuleFor(x => x.Template).NotNull();
        RuleFor(x => x.Template.Lines)
            .NotEmpty().WithMessage("قالب القيد يجب أن يحتوي على سطرين على الأقل.");
    }
}
