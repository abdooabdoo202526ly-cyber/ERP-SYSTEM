using ERPSystem.Modules.Payroll.Application;
using ERPSystem.Modules.Payroll.Application.Services;
using ERPSystem.Modules.Payroll.Domain.Entities;
using ERPSystem.Modules.Payroll.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPSystem.Tests.Payroll;

/// <summary>
/// اختبارات خدمة SalaryStructure — تغطية CRUD + multi-tenancy + validation.
/// النمط: Fake Repository يحاكي Dapper calls (in-memory Dictionary).
/// </summary>
public class SalaryStructureServiceTests
{
    private static (SalaryStructureService svc, FakeSalaryStructureRepository repo) Build()
    {
        var repo = new FakeSalaryStructureRepository();
        var svc = new SalaryStructureService(repo, NullLogger<SalaryStructureService>.Instance);
        return (svc, repo);
    }

    // ---------- CreateAsync ----------

    [Fact]
    public async Task Create_DefaultsToActive_LYD_WithEmptyLines()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        {
            Name = "هيكل بدوام كامل",
            Code = "FT-LYD",
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Name.Should().Be("هيكل بدوام كامل");
        r.Value!.Code.Should().Be("FT-LYD");
        r.Value!.Currency.Should().Be("LYD", "default currency = LYD");
        r.Value!.IsActive.Should().BeTrue();
        r.Value!.Lines.Should().BeEmpty();
        r.Value!.TotalEarnings.Should().Be(0);
        r.Value!.TotalDeductions.Should().Be(0);
        repo.Structures.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WithLines_PersistsAllLines()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        {
            Name = "هيكل اختباري",
            Code = "TEST-1",
            Currency = "USD",
            Lines = new List<CreateSalaryStructureLineRequest>
            {
                new() { Type = SalaryComponentType.Earning, Name = "بدل سكن", Amount = 500m, SortOrder = 0 },
                new() { Type = SalaryComponentType.Earning, Name = "بدل نقل", Amount = 200m, SortOrder = 1 },
                new() { Type = SalaryComponentType.Deduction, Name = "تأمين", Amount = 100m, SortOrder = 2 },
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Lines.Should().HaveCount(3);
        r.Value!.TotalEarnings.Should().Be(700m, "500 + 200");
        r.Value!.TotalDeductions.Should().Be(100m);
        r.Value!.Currency.Should().Be("USD");
        repo.Lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task Create_DuplicateCode_Fails()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "DUP" }, CancellationToken.None);

        var r = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "B", Code = "dup" }, CancellationToken.None);  // case-insensitive uniqueness

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.AlreadyExists);
    }

    [Fact]
    public async Task Create_DuplicateCode_AcrossTenants_Allowed()
    {
        var (svc, _) = Build();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var r1 = await svc.CreateAsync(tenant1, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "SAME" }, CancellationToken.None);
        var r2 = await svc.CreateAsync(tenant2, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "B", Code = "SAME" }, CancellationToken.None);

        r1.Succeeded.Should().BeTrue();
        r2.Succeeded.Should().BeTrue("الكود فريد داخل الـ tenant فقط");
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetById_WrongTenant_Fails()
    {
        var (svc, _) = Build();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        var c = await svc.CreateAsync(tenant1, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);

        var r = await svc.GetByIdAsync(tenant2, c.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistent_Fails()
    {
        var (svc, _) = Build();
        var r = await svc.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsLines_SortedBySortOrder()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        var c = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        {
            Name = "X", Code = "X",
            Lines = new List<CreateSalaryStructureLineRequest>
            {
                new() { Type = SalaryComponentType.Deduction, Name = "Z-Last", Amount = 50m, SortOrder = 5 },
                new() { Type = SalaryComponentType.Earning, Name = "A-First", Amount = 100m, SortOrder = 1 },
                new() { Type = SalaryComponentType.Earning, Name = "B-Middle", Amount = 200m, SortOrder = 3 },
            }
        }, CancellationToken.None);

        var r = await svc.GetByIdAsync(tenantId, c.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();
        r.Value!.Lines.Should().HaveCount(3);
        r.Value!.Lines[0].Name.Should().Be("A-First");
        r.Value!.Lines[1].Name.Should().Be("B-Middle");
        r.Value!.Lines[2].Name.Should().Be("Z-Last");
    }

    // ---------- ListAsync ----------

    [Fact]
    public async Task List_ReturnsOnlyActiveByDefault()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);
        var b = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "B", Code = "B" }, CancellationToken.None);
        await svc.DeactivateAsync(tenantId, Guid.NewGuid(), b.Value!.Id, CancellationToken.None);

        var activeOnly = await svc.ListAsync(tenantId, includeInactive: false, 0, 50, CancellationToken.None);
        activeOnly.Value!.Should().HaveCount(1, "B معطّل");
        activeOnly.Value![0].Code.Should().Be("A");

        var all = await svc.ListAsync(tenantId, includeInactive: true, 0, 50, CancellationToken.None);
        all.Value!.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_IsolatesByTenant()
    {
        var (svc, _) = Build();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await svc.CreateAsync(tenant1, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);
        await svc.CreateAsync(tenant2, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "B", Code = "B" }, CancellationToken.None);

        var r1 = await svc.ListAsync(tenant1, true, 0, 50, CancellationToken.None);
        var r2 = await svc.ListAsync(tenant2, true, 0, 50, CancellationToken.None);

        r1.Value!.Should().HaveCount(1);
        r1.Value![0].Code.Should().Be("A");
        r2.Value!.Should().HaveCount(1);
        r2.Value![0].Code.Should().Be("B");
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task Update_ReplacesAllLines_FullReplace()
    {
        var (svc, repo) = Build();
        var tenantId = Guid.NewGuid();

        var c = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        {
            Name = "Old", Code = "OLD",
            Lines = new List<CreateSalaryStructureLineRequest>
            {
                new() { Type = SalaryComponentType.Earning, Name = "Old-E1", Amount = 100m, SortOrder = 0 },
                new() { Type = SalaryComponentType.Earning, Name = "Old-E2", Amount = 200m, SortOrder = 1 },
            }
        }, CancellationToken.None);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), c.Value!.Id, new CreateSalaryStructureRequest
        {
            Name = "New", Code = "OLD",
            Lines = new List<CreateSalaryStructureLineRequest>
            {
                new() { Type = SalaryComponentType.Earning, Name = "New-E1", Amount = 500m, SortOrder = 0 },
            }
        }, CancellationToken.None);

        r.Succeeded.Should().BeTrue();
        r.Value!.Name.Should().Be("New");
        r.Value!.Lines.Should().HaveCount(1, "full replace — القديم تم حذفه");
        r.Value!.Lines[0].Name.Should().Be("New-E1");
        r.Value!.TotalEarnings.Should().Be(500m);
        repo.Lines.Should().HaveCount(1, "الـ lines القديمة حُذفت من الـ repo");
    }

    [Fact]
    public async Task Update_NonExistent_Fails()
    {
        var (svc, _) = Build();
        var r = await svc.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new CreateSalaryStructureRequest { Name = "X", Code = "X" }, CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }

    [Fact]
    public async Task Update_ChangingCodeToExistingOne_Fails()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);
        var b = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "B", Code = "B" }, CancellationToken.None);

        var r = await svc.UpdateAsync(tenantId, Guid.NewGuid(), b.Value!.Id, new CreateSalaryStructureRequest
        { Name = "B", Code = "A" }, CancellationToken.None);

        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.AlreadyExists);
    }

    // ---------- DeactivateAsync ----------

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        var c = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);

        var r = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        r.Succeeded.Should().BeTrue();

        var fetched = await svc.GetByIdAsync(tenantId, c.Value!.Id, CancellationToken.None);
        fetched.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_IsIdempotent()
    {
        var (svc, _) = Build();
        var tenantId = Guid.NewGuid();

        var c = await svc.CreateAsync(tenantId, Guid.NewGuid(), new CreateSalaryStructureRequest
        { Name = "A", Code = "A" }, CancellationToken.None);

        await svc.DeactivateAsync(tenantId, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);
        var r2 = await svc.DeactivateAsync(tenantId, Guid.NewGuid(), c.Value!.Id, CancellationToken.None);

        r2.Succeeded.Should().BeTrue("إعادة الإيقاف تُعتبر no-op ناجح");
    }

    [Fact]
    public async Task Deactivate_NonExistent_Fails()
    {
        var (svc, _) = Build();
        var r = await svc.DeactivateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        r.Succeeded.Should().BeFalse();
        r.ErrorCode.Should().Be(PayrollErrorCode.NotFound);
    }
}

// ============== Fake Repository ==============

internal class FakeSalaryStructureRepository : ISalaryStructureRepository
{
    public Dictionary<Guid, SalaryStructure> Structures { get; } = new();
    public Dictionary<Guid, List<SalaryStructureLine>> LinesByStructure { get; } = new();
    // Lines المحذوفة (للتأكد من سلوك الـ full-replace)
    public List<SalaryStructureLine> DeletedLines { get; } = new();
    public List<SalaryStructureLine> Lines =>
        LinesByStructure.SelectMany(kv => kv.Value).ToList();

    public Task<SalaryStructure?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Structures.TryGetValue(id, out var s) ? s : null);

    public Task<SalaryStructure?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct) =>
        Task.FromResult(Structures.Values.FirstOrDefault(s =>
            s.TenantId == tenantId && string.Equals(s.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<SalaryStructure>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SalaryStructure>>(Structures.Values
            .Where(s => s.TenantId == tenantId && (includeInactive || s.IsActive))
            .OrderBy(s => s.Code)
            .ToList());

    public Task<IReadOnlyList<SalaryStructureLine>> GetLinesAsync(Guid salaryStructureId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SalaryStructureLine>>(
            LinesByStructure.TryGetValue(salaryStructureId, out var l)
                ? l.OrderBy(x => x.SortOrder).ToList()
                : new List<SalaryStructureLine>());

    public Task InsertAsync(SalaryStructure structure, IEnumerable<SalaryStructureLine> lines, CancellationToken ct)
    {
        Structures[structure.Id] = structure;
        LinesByStructure[structure.Id] = lines.ToList();
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SalaryStructure structure, CancellationToken ct)
    {
        if (!Structures.ContainsKey(structure.Id))
            throw new InvalidOperationException("Structure not found");
        Structures[structure.Id] = structure;
        return Task.CompletedTask;
    }

    public Task InsertLineAsync(SalaryStructureLine line, CancellationToken ct)
    {
        if (!LinesByStructure.TryGetValue(line.SalaryStructureId, out var list))
            LinesByStructure[line.SalaryStructureId] = list = new List<SalaryStructureLine>();
        list.Add(line);
        return Task.CompletedTask;
    }

    public Task DeleteLineAsync(Guid lineId, CancellationToken ct)
    {
        foreach (var kv in LinesByStructure)
        {
            var removed = kv.Value.RemoveAll(l => l.Id == lineId);
            if (removed > 0)
            {
                // tracking للاختبارات
                var line = kv.Value.FirstOrDefault(l => l.Id == lineId) ?? new SalaryStructureLine { Id = lineId };
                DeletedLines.Add(line);
            }
        }
        return Task.CompletedTask;
    }
}
