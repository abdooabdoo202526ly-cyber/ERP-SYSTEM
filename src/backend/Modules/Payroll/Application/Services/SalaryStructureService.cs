using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Modules.Payroll.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Modules.Payroll.Application.Services;

// ============== Contract ==============

/// <summary>عقد خدمة SalaryStructure — CRUD + Deactivate (soft-delete) + multi-tenancy.</summary>
public interface ISalaryStructureService
{
    /// <summary>قائمة هياكل الرواتب للـ tenant (مع filter اختياري على الحالة).</summary>
    Task<PayrollResult<IReadOnlyList<SalaryStructureResponse>>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct);

    /// <summary>تفاصيل هيكل راتب واحد (مع الـ lines كاملة).</summary>
    Task<PayrollResult<SalaryStructureResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>إنشاء هيكل راتب جديد (مع Lines) — يفحص code uniqueness داخل الـ tenant.</summary>
    Task<PayrollResult<SalaryStructureResponse>> CreateAsync(Guid tenantId, Guid userId, CreateSalaryStructureRequest req, CancellationToken ct);

    /// <summary>تحديث هيكل راتب — full-replace للـ lines (delete + insert) للحفاظ على البساطة.</summary>
    Task<PayrollResult<SalaryStructureResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, CreateSalaryStructureRequest req, CancellationToken ct);

    /// <summary>إيقاف (soft-delete) هيكل راتب — الـ IsActive=false.</summary>
    Task<PayrollResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

// ============== Implementation ==============

/// <summary>
/// تنفيذ خدمة SalaryStructure. يتبع Clean Architecture:
/// - Domain (entities) ← تحتها
/// - Infrastructure (ISalaryStructureRepository) ← عبر interface فقط
/// - Result pattern موحَّد (PayrollResult&lt;T&gt;)
/// - Multi-tenancy: كل query مفهرس بـ TenantId
/// - Lines: full-replace عند Update (delete + insert) — أبسط من diff وتجنّب orphaned rows.
/// </summary>
public sealed class SalaryStructureService : ISalaryStructureService
{
    private readonly ISalaryStructureRepository _repo;
    private readonly ILogger<SalaryStructureService> _logger;

    public SalaryStructureService(ISalaryStructureRepository repo, ILogger<SalaryStructureService> logger)
    {
        _repo = repo; _logger = logger;
    }

    // ---------- ListAsync ----------

    public async Task<PayrollResult<IReadOnlyList<SalaryStructureResponse>>> ListAsync(
        Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var structures = await _repo.ListAsync(tenantId, includeInactive, ct);
        // pagination يدوي بعد الجلب (الـ repo الحالي يجلب الكل ثم نـ skip/take).
        var paged = structures.Skip(skip).Take(take).ToList();

        var result = new List<SalaryStructureResponse>(paged.Count);
        foreach (var s in paged)
        {
            var lines = await _repo.GetLinesAsync(s.Id, ct);
            result.Add(MapToResponse(s, lines));
        }
        return PayrollResult<IReadOnlyList<SalaryStructureResponse>>.Ok(result);
    }

    // ---------- GetByIdAsync ----------

    public async Task<PayrollResult<SalaryStructureResponse>> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var s = await _repo.GetByIdAsync(id, ct);
        if (s == null || s.TenantId != tenantId)
            return PayrollResult<SalaryStructureResponse>.Fail("هيكل الراتب غير موجود.", PayrollErrorCode.NotFound);

        var lines = await _repo.GetLinesAsync(s.Id, ct);
        return PayrollResult<SalaryStructureResponse>.Ok(MapToResponse(s, lines));
    }

    // ---------- CreateAsync ----------

    public async Task<PayrollResult<SalaryStructureResponse>> CreateAsync(
        Guid tenantId, Guid userId, CreateSalaryStructureRequest req, CancellationToken ct)
    {
        // uniqueness check (case-insensitive).
        if (await _repo.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return PayrollResult<SalaryStructureResponse>.Fail(
                "كود هيكل الراتب مستخدم.", PayrollErrorCode.AlreadyExists);

        var now = DateTime.UtcNow;
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = req.Name.Trim(),
            Code = req.Code.Trim(),
            Currency = string.IsNullOrWhiteSpace(req.Currency) ? "LYD" : req.Currency.Trim().ToUpperInvariant(),
            IsActive = req.IsActive ?? true,
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId,
        };

        // materialise الـ Lines بأمان (Id + TenantId) قبل الإدراج.
        var lines = (req.Lines ?? new List<CreateSalaryStructureLineRequest>())
            .Select(l => new SalaryStructureLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SalaryStructureId = structure.Id,
                Type = l.Type,
                Name = (l.Name ?? string.Empty).Trim(),
                Formula = string.IsNullOrWhiteSpace(l.Formula) ? null : l.Formula.Trim(),
                Amount = l.Amount,
                SortOrder = l.SortOrder,
            })
            .ToList();

        await _repo.InsertAsync(structure, lines, ct);
        _logger.LogInformation("تم إنشاء هيكل راتب {Code} ({Id}) للـ tenant {TenantId} بسطرين: {LineCount}",
            structure.Code, structure.Id, tenantId, lines.Count);

        return PayrollResult<SalaryStructureResponse>.Ok(MapToResponse(structure, lines));
    }

    // ---------- UpdateAsync ----------

    public async Task<PayrollResult<SalaryStructureResponse>> UpdateAsync(
        Guid tenantId, Guid userId, Guid id, CreateSalaryStructureRequest req, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing == null || existing.TenantId != tenantId)
            return PayrollResult<SalaryStructureResponse>.Fail("هيكل الراتب غير موجود.", PayrollErrorCode.NotFound);

        // إذا تغيّر الـ code، تأكّد من عدم التعارض.
        if (!string.Equals(existing.Code, req.Code, StringComparison.OrdinalIgnoreCase))
        {
            var conflict = await _repo.GetByCodeAsync(tenantId, req.Code, ct);
            if (conflict != null && conflict.Id != existing.Id)
                return PayrollResult<SalaryStructureResponse>.Fail(
                    "كود هيكل الراتب مستخدم.", PayrollErrorCode.AlreadyExists);
        }

        existing.Name = req.Name.Trim();
        existing.Code = req.Code.Trim();
        existing.Currency = string.IsNullOrWhiteSpace(req.Currency) ? "LYD" : req.Currency.Trim().ToUpperInvariant();
        existing.IsActive = req.IsActive ?? existing.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        // full-replace للـ Lines: حذف القديم + إدراج الجديد.
        var oldLines = await _repo.GetLinesAsync(existing.Id, ct);
        foreach (var old in oldLines)
        {
            await _repo.DeleteLineAsync(old.Id, ct);
        }
        var newLines = (req.Lines ?? new List<CreateSalaryStructureLineRequest>())
            .Select(l => new SalaryStructureLine
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SalaryStructureId = existing.Id,
                Type = l.Type,
                Name = (l.Name ?? string.Empty).Trim(),
                Formula = string.IsNullOrWhiteSpace(l.Formula) ? null : l.Formula.Trim(),
                Amount = l.Amount,
                SortOrder = l.SortOrder,
            })
            .ToList();
        foreach (var nl in newLines)
        {
            await _repo.InsertLineAsync(nl, ct);
        }

        await _repo.UpdateAsync(existing, ct);
        _logger.LogInformation("تم تحديث هيكل راتب {Id} ({Code}) بسطرين: {LineCount}",
            existing.Id, existing.Code, newLines.Count);

        return PayrollResult<SalaryStructureResponse>.Ok(MapToResponse(existing, newLines));
    }

    // ---------- DeactivateAsync ----------

    public async Task<PayrollResult<bool>> DeactivateAsync(
        Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var s = await _repo.GetByIdAsync(id, ct);
        if (s == null || s.TenantId != tenantId)
            return PayrollResult<bool>.Fail("هيكل الراتب غير موجود.", PayrollErrorCode.NotFound);

        if (!s.IsActive)
            return PayrollResult<bool>.Ok(true); // idempotent — لا خطأ على إعادة الإيقاف.

        s.IsActive = false;
        s.UpdatedAt = DateTime.UtcNow;
        s.UpdatedBy = userId;
        await _repo.UpdateAsync(s, ct);

        _logger.LogInformation("تم إيقاف هيكل راتب {Id} ({Code})", s.Id, s.Code);
        return PayrollResult<bool>.Ok(true);
    }

    // ============== Helpers ==============

    private static SalaryStructureResponse MapToResponse(SalaryStructure s, IReadOnlyList<SalaryStructureLine> lines)
    {
        var lineResponses = lines
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new SalaryStructureLineResponse
            {
                Id = l.Id,
                Type = l.Type,
                Name = l.Name,
                Formula = l.Formula,
                Amount = l.Amount,
                SortOrder = l.SortOrder,
            })
            .ToList();

        return new SalaryStructureResponse
        {
            Id = s.Id,
            TenantId = s.TenantId,
            Name = s.Name,
            Code = s.Code,
            Currency = s.Currency,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            Lines = lineResponses,
            TotalEarnings = lineResponses.Where(l => l.Type == SalaryComponentType.Earning).Sum(l => l.Amount),
            TotalDeductions = lineResponses.Where(l => l.Type == SalaryComponentType.Deduction).Sum(l => l.Amount),
        };
    }
}
