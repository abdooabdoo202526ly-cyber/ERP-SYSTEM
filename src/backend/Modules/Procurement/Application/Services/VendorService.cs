using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using ERPSystem.Modules.Procurement.Infrastructure;

namespace ERPSystem.Modules.Procurement.Application.Services;

/// <summary>نمط النتيجة الموحّد لكل خدمات Procurement — Success/Error + ErrorCode.</summary>
public sealed class ProcurementResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ProcurementErrorCode? ErrorCode { get; init; }
    public static ProcurementResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static ProcurementResult<T> Fail(string e, ProcurementErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

public enum ProcurementErrorCode
{
    NotFound, AlreadyExists, ValidationError, InvalidStatusTransition, BusinessRuleViolation, Internal
}

public interface IVendorService
{
    Task<ProcurementResult<VendorResponse>> CreateAsync(Guid tenantId, Guid userId, CreateVendorRequest req, CancellationToken ct);
    Task<ProcurementResult<VendorResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateVendorRequest req, CancellationToken ct);
    Task<ProcurementResult<VendorResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProcurementResult<IReadOnlyList<VendorResponse>>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<ProcurementResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class VendorService : IVendorService
{
    private readonly IVendorRepository _repo;
    public VendorService(IVendorRepository repo) => _repo = repo;

    public async Task<ProcurementResult<VendorResponse>> CreateAsync(Guid tenantId, Guid userId, CreateVendorRequest req, CancellationToken ct)
    {
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return ProcurementResult<VendorResponse>.Fail("كود المورّد مستخدم.", ProcurementErrorCode.AlreadyExists);

        var now = DateTime.UtcNow;
        var v = new Vendor
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Code = req.Code.Trim(), Name = req.Name.Trim(),
            Email = req.Email, Phone = req.Phone, Address = req.Address, TaxNumber = req.TaxNumber,
            Website = req.Website,  // DEC-081c: fix website persistence
            Currency = req.Currency.ToUpperInvariant(), PaymentTerms = req.PaymentTerms,
            IsActive = true,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId
        };
        await _repo.InsertAsync(v, ct);
        return ProcurementResult<VendorResponse>.Ok(MapToResponse(v));
    }

    public async Task<ProcurementResult<VendorResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateVendorRequest req, CancellationToken ct)
    {
        var v = await _repo.GetByIdAsync(id, ct);
        if (v == null || v.TenantId != tenantId)
            return ProcurementResult<VendorResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);

        v.Name = req.Name.Trim();
        v.Email = req.Email; v.Phone = req.Phone; v.Address = req.Address; v.TaxNumber = req.TaxNumber;
        v.Website = req.Website;  // DEC-081c: fix website persistence
        v.Currency = req.Currency.ToUpperInvariant();
        v.PaymentTerms = req.PaymentTerms;
        v.IsActive = req.IsActive;
        v.UpdatedAt = DateTime.UtcNow;
        v.UpdatedBy = userId;
        await _repo.UpdateAsync(v, ct);
        return ProcurementResult<VendorResponse>.Ok(MapToResponse(v));
    }

    public async Task<ProcurementResult<VendorResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var v = await _repo.GetByIdAsync(id, ct);
        if (v == null || v.TenantId != tenantId)
            return ProcurementResult<VendorResponse>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        return ProcurementResult<VendorResponse>.Ok(MapToResponse(v));
    }

    public async Task<ProcurementResult<IReadOnlyList<VendorResponse>>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _repo.ListAsync(tenantId, includeInactive, skip, take, ct);
        return ProcurementResult<IReadOnlyList<VendorResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<ProcurementResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var v = await _repo.GetByIdAsync(id, ct);
        if (v == null || v.TenantId != tenantId)
            return ProcurementResult<bool>.Fail("غير موجود.", ProcurementErrorCode.NotFound);
        v.IsActive = false;
        v.UpdatedAt = DateTime.UtcNow;
        v.UpdatedBy = userId;
        await _repo.UpdateAsync(v, ct);
        return ProcurementResult<bool>.Ok(true);
    }

    private static VendorResponse MapToResponse(Vendor v) => new()
    {
        Id = v.Id, TenantId = v.TenantId, Code = v.Code, Name = v.Name,
        Email = v.Email, Phone = v.Phone, Address = v.Address, TaxNumber = v.TaxNumber,
        Website = v.Website,  // DEC-081c
        Currency = v.Currency, PaymentTerms = v.PaymentTerms, IsActive = v.IsActive
    };
}
