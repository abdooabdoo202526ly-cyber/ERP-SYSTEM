using ERPSystem.Modules.Companies.Application.Services;
using ERPSystem.Modules.Companies.Entities;
using ERPSystem.Modules.Projects.Application;
using ERPSystem.Modules.Projects.Entities;
using ERPSystem.Modules.Projects.Infrastructure;

namespace ERPSystem.Modules.Projects.Application.Services;

public sealed class ProjectResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ProjectErrorCode? ErrorCode { get; init; }
    public static ProjectResult<T> Ok(T v) => new() { Succeeded = true, Value = v };
    public static ProjectResult<T> Fail(string e, ProjectErrorCode c) => new() { Succeeded = false, Error = e, ErrorCode = c };
}

public enum ProjectErrorCode
{
    NotFound, AlreadyExists, ValidationError, InvalidStatusTransition, Internal
}

public interface IProjectService
{
    Task<ProjectResult<ProjectResponse>> CreateAsync(Guid tenantId, Guid userId, CreateProjectRequest req, CancellationToken ct);
    Task<ProjectResult<ProjectResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateProjectRequest req, CancellationToken ct);
    Task<ProjectResult<ProjectResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<ProjectResult<IReadOnlyList<ProjectResponse>>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<ProjectResult<ProjectResponse>> ChangeStatusAsync(Guid tenantId, Guid userId, Guid id, ProjectStatus newStatus, CancellationToken ct);
    Task<ProjectResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct);
}

public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectBudgetRepository _budgets;
    private readonly ICostCenterService _costCenters;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IProjectRepository projects, IProjectBudgetRepository budgets, ICostCenterService costCenters, ICompanyService companies, ILogger<ProjectService> logger)
    {
        _projects = projects; _budgets = budgets; _costCenters = costCenters; _companies = companies; _logger = logger;
    }

    private readonly ICompanyService _companies;

    public async Task<ProjectResult<ProjectResponse>> CreateAsync(Guid tenantId, Guid userId, CreateProjectRequest req, CancellationToken ct)
    {
        if (await _projects.GetByCodeAsync(tenantId, req.Code, ct) != null)
            return ProjectResult<ProjectResponse>.Fail("كود المشروع مستخدم.", ProjectErrorCode.AlreadyExists);

        // v1.0.22: if CompanyId is null, use the tenant's first company
        // (auto-creates a default holding company if none exists).
        Guid companyId = req.CompanyId ?? Guid.Empty;
        if (companyId == Guid.Empty)
        {
            var companiesList = await _companies.ListAsync(tenantId, includeInactive: false, ct);
            if (companiesList.Succeeded && companiesList.Value != null && companiesList.Value.Count > 0)
            {
                companyId = companiesList.Value[0].Id;
            }
            else
            {
                // No companies yet — create a default holding
                var created = await _companies.EnsureDefaultHoldingAsync(tenantId, "Default Holding", "LYD", ct);
                if (created != Guid.Empty) companyId = created;
            }
        }
        if (companyId == Guid.Empty)
            return ProjectResult<ProjectResponse>.Fail("تعذّر تحديد الشركة. الرجاء إنشاء شركة أولاً.", ProjectErrorCode.ValidationError);

        // 1) Auto-create CostCenter (type=Project, code=PRJ code)
        var ccReq = new CreateCostCenterRequest
        {
            CompanyId = companyId,
            Code = $"CC-{req.Code}",
            Name = req.Name,
            Type = CostCenterType.Project,
            BudgetAmount = req.Budget,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
        };
        var ccResult = await _costCenters.CreateAsync(tenantId, ccReq, ct);
        if (!ccResult.Succeeded)
            return ProjectResult<ProjectResponse>.Fail($"فشل إنشاء CostCenter: {ccResult.Error}", ProjectErrorCode.Internal);

        // 2) Create Project
        var now = DateTime.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            CostCenterId = ccResult.Value!.Id,
            Code = req.Code.Trim(),
            Name = req.Name.Trim(),
            Description = req.Description,
            CustomerId = req.CustomerId,
            Status = ProjectStatus.Planning,
            Budget = req.Budget,
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId,
            IsActive = true
        };
        await _projects.InsertAsync(project, ct);

        // 3) Create ProjectBudget
        await _budgets.InsertAsync(new ProjectBudget
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProjectId = project.Id,
            CostCenterId = project.CostCenterId,
            AccountId = null,   // يمكن ربطه بحساب 4111 لاحقاً
            BudgetAmount = req.Budget,
            SpentAmount = 0,
            CommittedAmount = 0,
            LastRecalculatedAt = now,
        }, ct);

        _logger.LogInformation("تم إنشاء مشروع {Code} + CostCenter + Budget للمستأجر {TenantId}", req.Code, tenantId);
        return ProjectResult<ProjectResponse>.Ok(MapToResponse(project));
    }

    public async Task<ProjectResult<ProjectResponse>> UpdateAsync(Guid tenantId, Guid userId, Guid id, UpdateProjectRequest req, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(id, ct);
        if (project == null || project.TenantId != tenantId) return ProjectResult<ProjectResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);

        project.Name = req.Name.Trim();
        project.Description = req.Description;
        project.CustomerId = req.CustomerId;
        project.Budget = req.Budget;
        project.StartDate = req.StartDate;
        project.EndDate = req.EndDate;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = userId;
        await _projects.UpdateAsync(project, ct);

        // sync budget
        var budget = await _budgets.GetByProjectAsync(id, ct);
        if (budget != null)
        {
            budget.BudgetAmount = req.Budget;
            await _budgets.UpdateAsync(budget, ct);
        }
        return ProjectResult<ProjectResponse>.Ok(MapToResponse(project));
    }

    public async Task<ProjectResult<ProjectResponse>> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        var p = await _projects.GetByIdAsync(id, ct);
        if (p == null || p.TenantId != tenantId) return ProjectResult<ProjectResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        return ProjectResult<ProjectResponse>.Ok(MapToResponse(p));
    }

    public async Task<ProjectResult<IReadOnlyList<ProjectResponse>>> ListAsync(Guid tenantId, Guid? companyId, ProjectStatus? status, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        if (take is < 1 or > 200) take = 50;
        var list = await _projects.ListAsync(tenantId, companyId, status, includeInactive, skip, take, ct);
        return ProjectResult<IReadOnlyList<ProjectResponse>>.Ok(list.Select(MapToResponse).ToList());
    }

    public async Task<ProjectResult<ProjectResponse>> ChangeStatusAsync(Guid tenantId, Guid userId, Guid id, ProjectStatus newStatus, CancellationToken ct)
    {
        var p = await _projects.GetByIdAsync(id, ct);
        if (p == null || p.TenantId != tenantId) return ProjectResult<ProjectResponse>.Fail("غير موجود.", ProjectErrorCode.NotFound);

        // validation: forward-only workflow
        var validTransitions = new Dictionary<ProjectStatus, ProjectStatus[]>
        {
            { ProjectStatus.Planning, new[] { ProjectStatus.Active, ProjectStatus.Cancelled } },
            { ProjectStatus.Active, new[] { ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled } },
            { ProjectStatus.OnHold, new[] { ProjectStatus.Active, ProjectStatus.Cancelled } },
            { ProjectStatus.Completed, Array.Empty<ProjectStatus>() },
            { ProjectStatus.Cancelled, Array.Empty<ProjectStatus>() },
        };
        if (!validTransitions[p.Status].Contains(newStatus))
            return ProjectResult<ProjectResponse>.Fail($"لا يمكن الانتقال من {p.Status} إلى {newStatus}.", ProjectErrorCode.InvalidStatusTransition);

        p.Status = newStatus;
        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = userId;
        await _projects.UpdateAsync(p, ct);
        return ProjectResult<ProjectResponse>.Ok(MapToResponse(p));
    }

    public async Task<ProjectResult<bool>> DeactivateAsync(Guid tenantId, Guid userId, Guid id, CancellationToken ct)
    {
        var p = await _projects.GetByIdAsync(id, ct);
        if (p == null || p.TenantId != tenantId) return ProjectResult<bool>.Fail("غير موجود.", ProjectErrorCode.NotFound);
        p.IsActive = false;
        p.UpdatedAt = DateTime.UtcNow;
        p.UpdatedBy = userId;
        await _projects.UpdateAsync(p, ct);
        return ProjectResult<bool>.Ok(true);
    }

    private static ProjectResponse MapToResponse(Project p) => new()
    {
        Id = p.Id, TenantId = p.TenantId, CompanyId = p.CompanyId, CostCenterId = p.CostCenterId,
        Code = p.Code, Name = p.Name, Description = p.Description, CustomerId = p.CustomerId,
        Status = p.Status, Budget = p.Budget, StartDate = p.StartDate, EndDate = p.EndDate,
        CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, IsActive = p.IsActive,
    };
}
