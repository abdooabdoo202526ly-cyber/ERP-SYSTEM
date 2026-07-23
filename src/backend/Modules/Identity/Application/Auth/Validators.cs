using FluentValidation;

namespace ERPSystem.Modules.Identity.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("البريد الإلكتروني مطلوب.")
            .EmailAddress().WithMessage("صيغة البريد الإلكتروني غير صحيحة.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("كلمة المرور مطلوبة.")
            .MinimumLength(8).WithMessage("كلمة المرور يجب أن تكون 8 أحرف على الأقل.")
            .Matches("[A-Z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف كبير.")
            .Matches("[a-z]").WithMessage("كلمة المرور يجب أن تحتوي على حرف صغير.")
            .Matches("[0-9]").WithMessage("كلمة المرور يجب أن تحتوي على رقم.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("الاسم الكامل مطلوب.")
            .MaximumLength(200);

        // إما TenantId موجود أو TenantName (لإنشاء tenant جديد)
        RuleFor(x => x)
            .Must(x => x.TenantId != Guid.Empty || !string.IsNullOrWhiteSpace(x.TenantName))
            .WithMessage("يجب تحديد TenantId أو TenantName للمستأجر.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
