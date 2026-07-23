using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Entities;
using ERPSystem.Modules.AccountsReceivable.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.AccountsReceivable.Application.Services;

// ============== Result pattern موحد لكل خدمات AR ==============
public sealed class ArResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ArErrorCode? ErrorCode { get; init; }
    public static ArResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static ArResult<T> Fail(string e, ArErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

public enum ArErrorCode
{
    NotFound, AlreadyExists, ValidationError, InvalidStatusTransition, BusinessRuleViolation, Internal
}

// ============== Customer Service ==============

public interface ICustomerService
{
    Task<ArResult<CustomerResponse>> CreateAsync(Guid tenantId, Guid userId, CreateCustomerRequest req, CancellationToken ct);
    Task<ArResult<CustomerResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateCustomerRequest req, CancellationToken ct);
    Task<ArResult<CustomerResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ArResult<IReadOnlyList<CustomerResponse>>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<ArResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repo;
    private readonly ILogger<CustomerService> _logger;
    public CustomerService(ICustomerRepository repo, ILogger<CustomerService> logger) { _repo = repo; _logger = logger; }

    public async Task<ArResult<CustomerResponse>> CreateAsync(Guid tenantId, Guid userId, CreateCustomerRequest req, CancellationToken ct)
    {
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return ArResult<CustomerResponse>.Fail("كود العميل مستخدم.", ArErrorCode.AlreadyExists);

        var now = DateTime.UtcNow;
        var c = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = req.CompanyId, // v1.0.11: nullable - was Guid.Empty causing FK violation
            Code = req.Code.Trim(),
            Name = req.Name.Trim(),
            NameEn = req.NameEn?.Trim(),
            TaxId = req.TaxId?.Trim(),
            Email = req.Email?.Trim(),
            Phone = req.Phone?.Trim(),
            Address = req.Address?.Trim(),
            CreditLimit = req.CreditLimit,
            PaymentTermsDays = req.PaymentTermsDays,
            IsActive = true,
            CreatedAt = now, CreatedBy = userId,
            UpdatedAt = now, UpdatedBy = userId,
        };
        await _repo.InsertAsync(c, ct);
        _logger.LogInformation("تم إنشاء العميل {Code} ({Name}) للمستأجر {TenantId}", c.Code, c.Name, tenantId);
        return ArResult<CustomerResponse>.Ok(MapToResponse(c));
    }

    public async Task<ArResult<CustomerResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateCustomerRequest req, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId)
            return ArResult<CustomerResponse>.Fail("غير موجود.", ArErrorCode.NotFound);

        c.Name = req.Name.Trim();
        c.NameEn = req.NameEn?.Trim();
        c.TaxId = req.TaxId?.Trim();
        c.Email = req.Email?.Trim();
        c.Phone = req.Phone?.Trim();
        c.Address = req.Address?.Trim();
        c.CreditLimit = req.CreditLimit;
        c.PaymentTermsDays = req.PaymentTermsDays;
        c.IsActive = req.IsActive;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = userId;
        await _repo.UpdateAsync(c, ct);
        return ArResult<CustomerResponse>.Ok(MapToResponse(c));
    }

    public async Task<ArResult<CustomerResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId)
            return ArResult<CustomerResponse>.Fail("غير موجود.", ArErrorCode.NotFound);
        return ArResult<CustomerResponse>.Ok(MapToResponse(c));
    }

    public async Task<ArResult<IReadOnlyList<CustomerResponse>>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _repo.ListAsync(tenantId, includeInactive, skip, take, ct);
        return ArResult<IReadOnlyList<CustomerResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<ArResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var c = await _repo.GetByIdAsync(id, ct);
        if (c == null || c.TenantId != tenantId)
            return ArResult<bool>.Fail("غير موجود.", ArErrorCode.NotFound);
        c.IsActive = false;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = userId;
        await _repo.UpdateAsync(c, ct);
        return ArResult<bool>.Ok(true);
    }

    private static CustomerResponse MapToResponse(Customer c) => new()
    {
        Id = c.Id, TenantId = c.TenantId, CompanyId = c.CompanyId,
        Code = c.Code, Name = c.Name, NameEn = c.NameEn, TaxId = c.TaxId,
        Email = c.Email, Phone = c.Phone, Address = c.Address,
        CreditLimit = c.CreditLimit, PaymentTermsDays = c.PaymentTermsDays, IsActive = c.IsActive,
    };
}
