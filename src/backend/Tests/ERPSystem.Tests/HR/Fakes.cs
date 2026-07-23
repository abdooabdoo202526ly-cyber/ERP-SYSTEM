using ERPSystem.Modules.HR.Entities;
using ERPSystem.Modules.HR.Infrastructure;

namespace ERPSystem.Tests.HR;

// ============== Shared HR Fakes ==============
// Used by EmployeeServiceTests, LeaveServiceTests, AttendanceServiceTests (future).

internal class FakeEmployeeRepository : IEmployeeRepository
{
    private readonly Dictionary<Guid, Employee> _items = new();
    public void Add(Employee e) => _items[e.Id] = e;

    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var e) ? e : null);
    public Task<Employee?> GetByNumberAsync(Guid tenantId, string number, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(e => e.TenantId == tenantId && e.EmployeeNumber == number));
    public Task<Employee?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct) =>
        Task.FromResult(_items.Values.FirstOrDefault(e => e.TenantId == tenantId && e.Email == email));
    public Task<IReadOnlyList<Employee>> ListAsync(Guid tenantId, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Employee>>(_items.Values
            .Where(e => e.TenantId == tenantId && (includeInactive || e.IsActive)
                && (departmentId == null || e.DepartmentId == departmentId))
            .Skip(skip)
            .Take(take)
            .ToList());
    public Task InsertAsync(Employee emp, CancellationToken ct) { _items[emp.Id] = emp; return Task.CompletedTask; }
    public Task UpdateAsync(Employee emp, CancellationToken ct) { _items[emp.Id] = emp; return Task.CompletedTask; }
}

internal class FakeHRSequence : IHRDocumentSequenceRepository
{
    private int _counter = 1;
    public Task<string> GetNextEmployeeNumberAsync(Guid tenantId, CancellationToken ct) =>
        Task.FromResult($"EMP-{DateTime.UtcNow.Year}-{_counter++:D4}");
}

internal class FakeLeaveRepository : ILeaveRequestRepository
{
    private readonly Dictionary<Guid, LeaveRequest> _items = new();
    public void Add(LeaveRequest l) => _items[l.Id] = l;

    public Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(id, out var l) ? l : null);

    public Task<IReadOnlyList<LeaveRequest>> ListAsync(Guid tenantId, Guid? employeeId, LeaveStatus? status, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LeaveRequest>>(_items.Values
            .Where(l => l.TenantId == tenantId
                && (employeeId == null || l.EmployeeId == employeeId)
                && (status == null || l.Status == status))
            .Skip(skip)
            .Take(take)
            .ToList());

    public Task<bool> HasOverlappingApprovedAsync(Guid employeeId, DateTime start, DateTime end, CancellationToken ct) =>
        Task.FromResult(_items.Values.Any(l =>
            l.EmployeeId == employeeId &&
            l.Status == LeaveStatus.Approved &&
            !(l.EndDate < start || l.StartDate > end)));

    public Task<bool> HasOverlappingApprovedExcludingAsync(Guid employeeId, DateTime start, DateTime end, Guid excludeId, CancellationToken ct) =>
        Task.FromResult(_items.Values.Any(l =>
            l.EmployeeId == employeeId &&
            l.Id != excludeId &&
            l.Status == LeaveStatus.Approved &&
            !(l.EndDate < start || l.StartDate > end)));

    public Task InsertAsync(LeaveRequest leave, CancellationToken ct) { _items[leave.Id] = leave; return Task.CompletedTask; }
    public Task UpdateAsync(LeaveRequest leave, CancellationToken ct) { _items[leave.Id] = leave; return Task.CompletedTask; }
}
