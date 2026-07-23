using ERPSystem.Modules.HR.Application;
using FluentValidation;

namespace ERPSystem.Modules.HR.Application;

public sealed class CreateDepartmentRequestValidator : AbstractValidator<CreateDepartmentRequest>
{
    public CreateDepartmentRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9\\-_]+$").WithMessage("Code: حروف/أرقام/-/_ فقط.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateDepartmentRequestValidator : AbstractValidator<UpdateDepartmentRequest>
{
    public UpdateDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.NationalId).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(100);
        RuleFor(x => x.HireDate).NotEmpty();
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.NationalId).MaximumLength(50);
        RuleFor(x => x.JobTitle).MaximumLength(100);
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0);
    }
}

public sealed class CheckInOutRequestValidator : AbstractValidator<CheckInOutRequest>
{
    public CheckInOutRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CreateLeaveRequestValidator : AbstractValidator<CreateLeaveRequestDto>
{
    public CreateLeaveRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEqual(Guid.Empty);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThanOrEqualTo(x => x.StartDate.Date)
            .WithMessage("تاريخ النهاية يجب أن يكون >= تاريخ البداية.");
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

// T4-HR-Details: validator لتعديل طلب إجازة قابل للتعديل فقط إذا الحالة Pending.
// ملاحظة: لا نتحقق من الـ Status هنا — الباك اند يفرضه في الـ service layer.
// الحقول نفسها: LeaveType/StartDate/EndDate/Reason/Notes.
public sealed class UpdateLeaveRequestValidator : AbstractValidator<UpdateLeaveRequestDto>
{
    public UpdateLeaveRequestValidator()
    {
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty().GreaterThanOrEqualTo(x => x.StartDate.Date)
            .WithMessage("تاريخ النهاية يجب أن يكون >= تاريخ البداية.");
        RuleFor(x => x.Reason).MaximumLength(1000);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}
