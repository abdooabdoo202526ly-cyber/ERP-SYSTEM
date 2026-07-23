using FluentValidation;

namespace ERPSystem.Modules.Inventory.Application;

public sealed class ReceiveStockRequestValidator : AbstractValidator<ReceiveStockRequest>
{
    public ReceiveStockRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية لازم تكون > 0 للاستلام.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MovementDate).NotEqual(default(DateTime));
    }
}

public sealed class IssueStockRequestValidator : AbstractValidator<IssueStockRequest>
{
    public IssueStockRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("الكمية لازم تكون > 0 للصرف.");
        RuleFor(x => x.MovementDate).NotEqual(default(DateTime));
    }
}

public sealed class TransferStockRequestValidator : AbstractValidator<TransferStockRequest>
{
    public TransferStockRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.SourceWarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.DestinationWarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(x => x.SourceWarehouseId != x.DestinationWarehouseId)
            .WithMessage("مخزن المصدر والمخزن الهدف يجب أن يكونا مختلفين.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public sealed class AdjustStockRequestValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEqual(Guid.Empty);
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).NotEqual(0).WithMessage("التسوية لازم تكون ≠ 0 (موجبة أو سالبة).");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ReferenceType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ReferenceId).NotEqual(Guid.Empty);
        RuleFor(x => x.ExpiresAt).Must(d => d > DateTime.UtcNow).WithMessage("تاريخ الانتهاء يجب أن يكون في المستقبل.");
    }
}
