using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Entities;
using FluentValidation;

namespace ERPSystem.Modules.AccountsReceivable.Application;

/// <summary>
/// FluentValidation rules لكل Request DTOs في AccountsReceivable.
/// ملاحظة: اسم كل class ينتهي بـ RequestValidator لتوافقه مع
/// AddValidatorsFromAssemblyContaining في Program.cs.
/// </summary>
public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9\\-_]+$").WithMessage("Code: حروف/أرقام/-/_ فقط.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
        RuleFor(x => x.PaymentTermsDays).GreaterThanOrEqualTo(0).LessThanOrEqualTo(365);
    }
}

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).MaximumLength(200);
        RuleFor(x => x.TaxId).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.CreditLimit).GreaterThanOrEqualTo(0).When(x => x.CreditLimit.HasValue);
        RuleFor(x => x.PaymentTermsDays).GreaterThanOrEqualTo(0).LessThanOrEqualTo(365);
    }
}

public sealed class CreateSalesInvoiceLineRequestValidator : AbstractValidator<CreateSalesInvoiceLineRequest>
{
    public CreateSalesInvoiceLineRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxRate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1);
    }
}

public sealed class CreateSalesInvoiceRequestValidator : AbstractValidator<CreateSalesInvoiceRequest>
{
    public CreateSalesInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty);
        RuleFor(x => x.InvoiceDate).NotEmpty();
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("يجب أن تحتوي الفاتورة على بند واحد على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new CreateSalesInvoiceLineRequestValidator());
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.InvoiceDate.Date)
            .When(x => x.DueDate.HasValue)
            .WithMessage("تاريخ الاستحقاق يجب أن يكون >= تاريخ الفاتورة.");
    }
}

public sealed class UpdateSalesInvoiceRequestValidator : AbstractValidator<UpdateSalesInvoiceRequest>
{
    public UpdateSalesInvoiceRequestValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("يجب أن تحتوي الفاتورة على بند واحد على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new CreateSalesInvoiceLineRequestValidator());
    }
}

public sealed class CreateReceiptAllocationRequestValidator : AbstractValidator<CreateReceiptAllocationRequest>
{
    public CreateReceiptAllocationRequestValidator()
    {
        RuleFor(x => x.SalesInvoiceId).NotEqual(Guid.Empty);
        RuleFor(x => x.AmountApplied).GreaterThan(0);
    }
}

public sealed class CreateReceiptRequestValidator : AbstractValidator<CreateReceiptRequest>
{
    public CreateReceiptRequestValidator()
    {
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty);
        RuleFor(x => x.ReceiptDate).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ السند يجب أن يكون أكبر من صفر.");
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.PaymentMethod).Must(m => string.IsNullOrEmpty(m) || PaymentMethod.All.Contains(m))
            .WithMessage($"PaymentMethod يجب أن يكون واحداً من: {string.Join(", ", PaymentMethod.All)}");
        RuleFor(x => x.Allocations).NotEmpty().WithMessage("يجب تخصيص سند القبض على فاتورة واحدة على الأقل.");
        RuleForEach(x => x.Allocations).SetValidator(new CreateReceiptAllocationRequestValidator());
    }
}
