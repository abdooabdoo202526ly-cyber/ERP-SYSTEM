using FluentValidation;

namespace ERPSystem.Modules.Projects.Application;

public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        // v1.0.22: CompanyId is now optional (single-company mode)
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9\\-_]+$").WithMessage("كود المشروع: حروف/أرقام/-/_ فقط.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).WithMessage("الميزانية لا تقل عن صفر.");
        RuleFor(x => x.StartDate).NotEqual(default(DateTime));
        RuleFor(x => x)
            .Must(x => x.EndDate == null || x.EndDate >= x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.");
    }
}

public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Budget).GreaterThanOrEqualTo(0).WithMessage("الميزانية لا تقل عن صفر.");
        RuleFor(x => x.StartDate).NotEqual(default(DateTime));
        RuleFor(x => x)
            .Must(x => x.EndDate == null || x.EndDate >= x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.");
    }
}

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.StartDate == null || x.EndDate == null || x.EndDate >= x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.");
    }
}

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ProgressPercent).InclusiveBetween(0, 100)
            .WithMessage("نسبة التقدم بين 0 و 100.");
        RuleFor(x => x)
            .Must(x => x.StartDate == null || x.EndDate == null || x.EndDate >= x.StartDate)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.");
    }
}

public sealed class CreateResourceRequestValidator : AbstractValidator<CreateResourceRequest>
{
    public CreateResourceRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.HourlyRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Type).Must(t => t != 0).WithMessage("نوع المورد مطلوب.");
    }
}

public sealed class CreateAssignmentRequestValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentRequestValidator()
    {
        RuleFor(x => x.ProjectId).NotEqual(Guid.Empty);
        RuleFor(x => x.TaskId).NotEqual(Guid.Empty);
        RuleFor(x => x.ResourceId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.From).NotEqual(default(DateTime));
        RuleFor(x => x.To).NotEqual(default(DateTime));
        RuleFor(x => x).Must(x => x.To > x.From)
            .WithMessage("تاريخ النهاية يجب أن يكون بعد البداية.");
    }
}
