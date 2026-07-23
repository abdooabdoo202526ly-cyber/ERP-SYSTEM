# 🚪 src/backend/Host/AGENTS.md

> نقطة الدخول للـ Backend (Program.cs + Controllers + Swagger).

## شو فيه

- `Program.cs` — composition root: DI registrations، middleware pipeline
- `Controllers/` — كل الـ HTTP endpoints
- `Swagger/` — Swagger customizations
- `appsettings.json` + `appsettings.Development.json` — التهيئة
- `ERP-SYSTEM.csproj` — الـ NuGet dependencies + Compile Includes للموديولات

## Conventions

- **Controllers فقط في Host** — منطق الأعمال في `Application/` layers
- **DI registrations في Program.cs** مرتبة: Logging → Config → Infrastructure → Application → Auth → API
- **Endpoint route**: دائماً `api/<module>/<resource>` — مثال: `api/auth/login`
- **Response shape**: إما typed DTO أو `ProblemDetails` للأخطاء
- **Authorize**: استخدم `[Authorize]` على الـ controller أو action، استخدم `[AllowAnonymous]` فقط على `register/login/refresh`
- **Validators**: عند إنشاء `IValidator<TRequest>` جديد، **تأكد أن** اسم الـ class هو `XRequestValidator` (مثلاً `UpdateProjectRequestValidator`) — لأن `AddValidatorsFromAssemblyContaining<CreateProjectRequestValidator>()` يكتشف فقط الـ validators الموجودة في الـ Host assembly. **أي Request DTO يُمرر للـ controller يجب أن يكون له validator مقابل** (وإلا DI يفشل بـ 500).

## لما تشتغل هنا

- عند إضافة module جديد:
  1. أنشئ folder في `Modules/<Name>/`
  2. عرّف الـ interfaces و DI registrations في Program.cs
  3. أنشئ Controller في `Host/Controllers/<Name>Controller.cs`
  4. أضف الـ migration في `Shared/Migrations/`
- عند تغيير DI: حدّث قسم DI registrations في هذا الـ AGENTS.md

## بعد التعديل

- تأكد أن `dotnet build` نظيف
- Swagger UI يعرض الـ endpoints الجديدة
- Health endpoints (`/health`, `/health/live`, `/health/ready`) ترجع 200

## Health Endpoints (الفعلية)

| Endpoint | الـ Route | الـ Method | الغرض |
|----------|---------|----------|-------|
| `MapGet("/health")` | `Program.cs` | GET | Liveness (بدون controller) |
| `HealthController.Live` | `/health/live` | GET | Liveness (في HealthController) |
| `HealthController.Ready` | `/health/ready` | GET | Readiness (يفحص Postgres؛ Redis اختياري) |

## Controllers المسجّلة (Phase 3+)

- `AuthController` — Identity
- `AccountsController`, `JournalEntriesController`, `LedgerController`, `PostingRulesController` — Finance
- `CompaniesController`, `CostCentersController` — Companies
- `ProjectsController`, `TasksController`, `ResourcesController` — Projects
- `ItemsController`, `ItemCategoriesController`, `WarehousesController`, `UnitOfMeasuresController`, `StockMovementsController`, `StockLevelsController`, `StockReservationsController` — Inventory
- `NotificationsController` — Notifications
- `EventsController` — EventBus
- `ReportsController` — Reports
- **`ProcurementController`** 🆕 Phase 3 — 11 endpoints (vendors, POs, GRs, bills)
- **`HrController`** 🆕 Phase 3.5 — 6+ endpoints (departments, employees, attendance, leaves)

**Redis** مسجّل في DI فقط لو `ConnectionStrings:Redis` غير فارغ. مع `AbortOnConnectFail = false`، النظام يستمر في العمل حتى لو Redis معطّل (لـ dev). في الإنتاج، يجب تشغيل Redis واعتماده كـ dependency.

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md)
- [`../Shared/AGENTS.md`](../Shared/AGENTS.md)


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
