using Dapper;
using ERPSystem.Modules.HR.Entities;
using ERPSystem.Shared.Infrastructure;

namespace ERPSystem.Modules.HR.Infrastructure;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbConnectionFactory _db;
    public DepartmentRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, code, name, parent_id AS ParentId,
        manager_id AS ManagerId, is_active AS IsActive, created_at AS CreatedAt, updated_at AS UpdatedAt";

    public async Task<Department?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Department>(new CommandDefinition(
            $"SELECT {Sel} FROM departments WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<Department?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Department>(new CommandDefinition(
            $"SELECT {Sel} FROM departments WHERE tenant_id = @TenantId AND LOWER(code) = LOWER(@Code) LIMIT 1",
            new { TenantId = tenantId, Code = code }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Department>> ListAsync(Guid tenantId, bool includeInactive, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM departments WHERE tenant_id = @TenantId";
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY code";
        var rows = await conn.QueryAsync<Department>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Department d, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO departments (id, tenant_id, code, name, parent_id, manager_id, is_active, created_at, updated_at)
            VALUES (@Id, @TenantId, @Code, @Name, @ParentId, @ManagerId, @IsActive, @CreatedAt, @UpdatedAt)",
            d, cancellationToken: ct));
    }

    public async Task UpdateAsync(Department d, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE departments SET name = @Name, parent_id = @ParentId, manager_id = @ManagerId,
                                   is_active = @IsActive, updated_at = @UpdatedAt
            WHERE id = @Id", d, cancellationToken: ct));
    }
}

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly IDbConnectionFactory _db;
    public EmployeeRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, employee_number AS EmployeeNumber, full_name AS FullName,
        email, phone, national_id AS NationalId, department_id AS DepartmentId, job_title AS JobTitle,
        hire_date AS HireDate, termination_date AS TerminationDate, base_salary AS BaseSalary,
        is_active AS IsActive, created_at AS CreatedAt, created_by AS CreatedBy,
        updated_at AS UpdatedAt, updated_by AS UpdatedBy";

    public async Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Employee>(new CommandDefinition(
            $"SELECT {Sel} FROM employees WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<Employee?> GetByNumberAsync(Guid tenantId, string number, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Employee>(new CommandDefinition(
            $"SELECT {Sel} FROM employees WHERE tenant_id = @TenantId AND employee_number = @Number LIMIT 1",
            new { TenantId = tenantId, Number = number }, cancellationToken: ct));
    }

    public async Task<Employee?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Employee>(new CommandDefinition(
            $"SELECT {Sel} FROM employees WHERE tenant_id = @TenantId AND LOWER(email) = LOWER(@Email) LIMIT 1",
            new { TenantId = tenantId, Email = email }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Employee>> ListAsync(Guid tenantId, Guid? departmentId, bool includeInactive, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM employees WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (departmentId.HasValue) { sql += " AND department_id = @DepartmentId"; p.Add("DepartmentId", departmentId.Value); }
        if (!includeInactive) sql += " AND is_active = true";
        sql += " ORDER BY employee_number OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<Employee>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Employee e, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO employees (id, tenant_id, employee_number, full_name, email, phone, national_id,
                                   department_id, job_title, hire_date, termination_date, base_salary,
                                   is_active, created_at, created_by, updated_at, updated_by)
            VALUES (@Id, @TenantId, @EmployeeNumber, @FullName, @Email, @Phone, @NationalId,
                    @DepartmentId, @JobTitle, @HireDate, @TerminationDate, @BaseSalary,
                    @IsActive, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)",
            e, cancellationToken: ct));
    }

    public async Task UpdateAsync(Employee e, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE employees SET full_name = @FullName, email = @Email, phone = @Phone, national_id = @NationalId,
                                  department_id = @DepartmentId, job_title = @JobTitle,
                                  termination_date = @TerminationDate, base_salary = @BaseSalary,
                                  is_active = @IsActive, updated_at = @UpdatedAt, updated_by = @UpdatedBy
            WHERE id = @Id", e, cancellationToken: ct));
    }
}

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly IDbConnectionFactory _db;
    public AttendanceRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, employee_id AS EmployeeId, type, timestamp, notes, ip_address AS IpAddress, created_at AS CreatedAt";

    public async Task<Attendance?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Attendance>(new CommandDefinition(
            $"SELECT {Sel} FROM attendance WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<Attendance?> GetLastForEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<Attendance>(new CommandDefinition(
            $"SELECT {Sel} FROM attendance WHERE tenant_id = @TenantId AND employee_id = @EmployeeId ORDER BY timestamp DESC LIMIT 1",
            new { TenantId = tenantId, EmployeeId = employeeId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Attendance>> ListAsync(Guid tenantId, Guid? employeeId, DateTime? from, DateTime? to, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM attendance WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (employeeId.HasValue) { sql += " AND employee_id = @EmployeeId"; p.Add("EmployeeId", employeeId.Value); }
        if (from.HasValue) { sql += " AND timestamp >= @From"; p.Add("From", from.Value); }
        if (to.HasValue) { sql += " AND timestamp <= @To"; p.Add("To", to.Value); }
        sql += " ORDER BY timestamp DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<Attendance>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task InsertAsync(Attendance a, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO attendance (id, tenant_id, employee_id, type, timestamp, notes, ip_address, created_at)
            VALUES (@Id, @TenantId, @EmployeeId, @Type, @Timestamp, @Notes, @IpAddress, @CreatedAt)", new
        {
            a.Id, a.TenantId, a.EmployeeId, Type = a.Type.ToString(), a.Timestamp, a.Notes, a.IpAddress, a.CreatedAt
        }, cancellationToken: ct));
    }

    // T4-HR-Details: تعديل سجل حضور — تحديث Notes فقط (الحقائق لا تتغير).
    public async Task UpdateAsync(Attendance a, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE attendance SET notes = @Notes WHERE id = @Id",
            new { a.Notes, a.Id }, cancellationToken: ct));
    }

    // T4-HR-Details: حذف سجل حضور (hard delete — نُسجّل في الـ audit log عند الحاجة).
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM attendance WHERE id = @Id",
            new { Id = id }, cancellationToken: ct));
    }
}

public sealed class LeaveRequestRepository : ILeaveRequestRepository
{
    private readonly IDbConnectionFactory _db;
    public LeaveRequestRepository(IDbConnectionFactory db) => _db = db;

    private const string Sel = @"id, tenant_id AS TenantId, employee_id AS EmployeeId, leave_type AS LeaveType,
        start_date AS StartDate, end_date AS EndDate, total_days AS TotalDays, status, reason,
        approver_id AS ApproverId, approved_at AS ApprovedAt, notes,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt";

    public async Task<LeaveRequest?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<LeaveRequest>(new CommandDefinition(
            $"SELECT {Sel} FROM leave_requests WHERE id = @Id LIMIT 1", new { Id = id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LeaveRequest>> ListAsync(Guid tenantId, Guid? employeeId, LeaveStatus? status, int skip, int take, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var sql = $"SELECT {Sel} FROM leave_requests WHERE tenant_id = @TenantId";
        var p = new DynamicParameters();
        p.Add("TenantId", tenantId);
        if (employeeId.HasValue) { sql += " AND employee_id = @EmployeeId"; p.Add("EmployeeId", employeeId.Value); }
        if (status.HasValue) { sql += " AND status = @Status"; p.Add("Status", status.Value.ToString()); }
        sql += " ORDER BY created_at DESC OFFSET @Skip LIMIT @Take";
        p.Add("Skip", skip); p.Add("Take", take);
        var rows = await conn.QueryAsync<LeaveRequest>(new CommandDefinition(sql, p, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<bool> HasOverlappingApprovedAsync(Guid employeeId, DateTime start, DateTime end, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM leave_requests
            WHERE employee_id = @EmployeeId
              AND status = 'Approved'
              AND NOT (end_date < @Start OR start_date > @End)",
            new { EmployeeId = employeeId, Start = start, End = end }, cancellationToken: ct));
        return n > 0;
    }

    // T4-HR-Details: نفس HasOverlappingApprovedAsync لكن مع استثناء ID معيّن
    // (الطلب الذي يتم تعديله نفسه).
    public async Task<bool> HasOverlappingApprovedExcludingAsync(Guid employeeId, DateTime start, DateTime end, Guid excludeId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        var n = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM leave_requests
            WHERE employee_id = @EmployeeId
              AND status = 'Approved'
              AND id <> @ExcludeId
              AND NOT (end_date < @Start OR start_date > @End)",
            new { EmployeeId = employeeId, Start = start, End = end, ExcludeId = excludeId }, cancellationToken: ct));
        return n > 0;
    }

    public async Task InsertAsync(LeaveRequest l, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO leave_requests (id, tenant_id, employee_id, leave_type, start_date, end_date, total_days,
                                        status, reason, approver_id, approved_at, notes,
                                        created_at, created_by, updated_at)
            VALUES (@Id, @TenantId, @EmployeeId, @LeaveType, @StartDate, @EndDate, @TotalDays,
                    @Status, @Reason, @ApproverId, @ApprovedAt, @Notes,
                    @CreatedAt, @CreatedBy, @UpdatedAt)", new
        {
            l.Id, l.TenantId, l.EmployeeId, LeaveType = l.LeaveType.ToString(),
            l.StartDate, l.EndDate, l.TotalDays, Status = l.Status.ToString(),
            l.Reason, l.ApproverId, l.ApprovedAt, l.Notes,
            l.CreatedAt, l.CreatedBy, l.UpdatedAt
        }, cancellationToken: ct));
    }

    public async Task UpdateAsync(LeaveRequest l, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            UPDATE leave_requests SET status = @Status, approver_id = @ApproverId, approved_at = @ApprovedAt,
                                      notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id", new
        {
            l.Id, Status = l.Status.ToString(), l.ApproverId, l.ApprovedAt, l.Notes, l.UpdatedAt
        }, cancellationToken: ct));
    }
}

public sealed class HRDocumentSequenceRepository : IHRDocumentSequenceRepository
{
    private readonly IDbConnectionFactory _db;
    public HRDocumentSequenceRepository(IDbConnectionFactory db) => _db = db;

    public async Task<string> GetNextEmployeeNumberAsync(Guid tenantId, CancellationToken ct)
    {
        using var conn = await _db.CreateOltpConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(@"
            CREATE TABLE IF NOT EXISTS hr_document_sequences (
                tenant_id UUID NOT NULL,
                prefix VARCHAR(20) NOT NULL,
                last_number INT NOT NULL DEFAULT 0,
                PRIMARY KEY (tenant_id, prefix)
            )", cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO hr_document_sequences (tenant_id, prefix, last_number)
            VALUES (@TenantId, 'EMP', 1)
            ON CONFLICT (tenant_id, prefix) DO UPDATE SET last_number = hr_document_sequences.last_number + 1",
            new { TenantId = tenantId }, cancellationToken: ct));

        var last = await conn.QueryFirstOrDefaultAsync<int>(new CommandDefinition(
            "SELECT last_number FROM hr_document_sequences WHERE tenant_id = @TenantId AND prefix = 'EMP'",
            new { TenantId = tenantId }, cancellationToken: ct));

        var year = DateTime.UtcNow.Year;
        return $"EMP-{year}-{last:D4}";
    }
}
