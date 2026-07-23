# 👥 src/backend/Modules/HR/AGENTS.md

> HR Core Module — ✅ Phase 3.5 (Department + Employee + Attendance + Leave)

## شو فيه

```
HR/
├── Entities/
│   ├── Department.cs               # Department hierarchy (parent_id, code)
│   ├── Employee.cs                 # Employee (fullName, email, baseSalary, hireDate, departmentId)
│   ├── Attendance.cs               # CheckIn / CheckOut records
│   └── LeaveRequest.cs             # LeaveRequest (Create → Approve/Reject)
├── Application/
│   ├── Dtos.cs                     # Request/Response DTOs (~3.8KB)
│   ├── Validators.cs               # FluentValidation (~2.6KB)
│   └── Services/
│       └── Services.cs             # DepartmentService + EmployeeService + AttendanceService + LeaveService (~18KB)
└── Infrastructure/
    ├── IRepositories.cs            # IDepartment/Employee/Attendance/LeaveRequest/HRDocumentSequenceRepository
    └── Repositories.cs             # Dapper implementations (~15KB)
```

## Business Rules

### Department
- `code` فريد لكل tenant
- يدعم hierarchy عبر `parent_id` (nullable)
- soft-delete عبر `is_active = false`

### Employee
- `email` فريد لكل tenant
- `base_salary` موجب (Decimal(18,4))
- `hire_date` لا يمكن أن يكون في المستقبل
- عند soft-delete: لا تظهر في الـ queries العادية، تبقى في الجدول

### Attendance (CheckIn / CheckOut)
- لكل employee في يوم: CheckIn واحد ثم CheckOut
- `CheckOut` يجب أن يكون بعد `CheckIn` في نفس اليوم
- يحسب `worked_hours` تلقائياً عند CheckOut
- يحوي `notes` (nullable)

### LeaveRequest
- types: `Annual` / `Sick` / `Unpaid` / `Maternity`
- status: `Pending` → `Approved` / `Rejected`
- `start_date` < `end_date`
- لا يتجاوز `available_days` من نوع الإجازة
- عند Approve → يحسب عدد الأيام الفعلية (excludes weekends)

## Endpoints (6+)

| Method | Path | الوصف |
|--------|------|-------|
| GET | `/api/hr/departments` | قائمة الأقسام (tree) |
| POST | `/api/hr/departments` | إنشاء قسم |
| GET | `/api/hr/employees` | قائمة الموظفين |
| POST | `/api/hr/employees` | إنشاء موظف |
| PUT | `/api/hr/employees/{id}` | تحديث بيانات |
| GET | `/api/hr/attendance` | سجل الحضور (filter by employee, date) |
| POST | `/api/hr/attendance` | CheckIn / CheckOut |
| GET | `/api/hr/leaves` | قائمة طلبات الإجازة |
| POST | `/api/hr/leaves` | تقديم طلب |
| PUT | `/api/hr/leaves/{id}/approve` | موافقة |
| PUT | `/api/hr/leaves/{id}/reject` | رفض |

## Document Numbering

`IHRDocumentSequenceRepository` يولّد:
- `EMP-2026-0001`, `EMP-2026-0002`, ...
- `LR-2026-0001` (Leave Request)
- `ATT-2026-0001`

## لما تشتغل هنا

- **إضافة Payroll**: أنشئ `PayrollModule/` (Phase 4) يحوي Salary calculation, Payslip generation, Tax deductions
- **إضافة End-of-Service (EOS)**: حساب مستحقات نهاية الخدمة حسب القانون المحلي
- **Workflow للـ LeaveApproval**: استخدم `[Authorize(Roles="HRManager")]` على approve/reject
- **Performance reviews**: Phase 4+

## بعد التعديل

- شغّل `dotnet build` → 0 errors
- الـ migration الـ HR يجب أن يكون مطبّقاً في الـ DB قبل اختبار الـ endpoints
- اختبر بـ `curl` (POST ثم GET) للتأكد من الـ flow كامل

## تكامل مع الموديولات الأخرى

- **Identity**: كل employee مرتبط بـ `UserId` (في مرحلة لاحقة) — حالياً منفصل
- **Notifications**: عند Approve Leave → `LeaveApprovedEvent` → notification للـ employee (مستقبلي)
- **Finance**: BaseSalary في Payroll calculation (Phase 4)

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — نفس النمط
- [`../Identity/AGENTS.md`](../Identity/AGENTS.md) — User integration
- [`../../Host/AGENTS.md`](../../Host/AGENTS.md) — DI registration


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
