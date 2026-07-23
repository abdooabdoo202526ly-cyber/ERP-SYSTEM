# 🔐 src/backend/Modules/Identity/AGENTS.md

> Identity Module — Phase 0 (✅ مكتمل).
>
> محدّث: 2026-06-24 — إضافة الربط مع Phase 3/4 modules

## شو فيه

```
Identity/
├── Entities/
│   ├── User.cs            # User entity + navigations
│   ├── Role.cs            # Role + UserRole join
│   ├── Tenant.cs          # Tenant (multi-tenancy root)
│   └── RefreshToken.cs    # JWT refresh token (rotation + reuse detection)
├── Application/
│   └── Auth/
│       ├── AuthDtos.cs         # RegisterRequest, LoginRequest, AuthResponse, UserInfo
│       ├── IAuthService.cs     # Contract
│       ├── AuthService.cs      # Implementation (Register, Login, Refresh, Revoke)
│       ├── IJwtTokenService.cs # Contract
│       ├── JwtTokenService.cs  # JWT generation + validation
│       ├── JwtSettings.cs      # Config binding
│       └── Validators.cs       # FluentValidation rules
└── Infrastructure/
    ├── IRepositories.cs        # All repository contracts
    ├── UserRepository.cs       # Dapper queries
    ├── RoleRepository.cs       # + EnsureDefaultRolesAsync
    ├── TenantRepository.cs
    └── RefreshTokenRepository.cs
```

## Domain Model

### Tenant
- معرّف منفصل لكل مستأجر (شركة / مؤسسة)
- `subdomain` فريد للتمييز — **يُحسب تلقائياً من TenantName عبر `Slugify()` عند إنشاء tenant جديد** (لا يُرسل من الـ client)
- `IsActive` للـ soft-disable
- `SubscriptionExpiresAt` للـ SaaS billing لاحقاً

### User
- `TenantId` — كل user مرتبط بمستأجر واحد
- `Email` فريد **داخل المستأجر** (يمكن تكراره عبر tenants)
- `PasswordHash` — BCrypt، workFactor 12
- `IsActive`, `TwoFactorEnabled` (للمرحلة القادمة)

### Role
- 4 أدوار افتراضية تُنشأ تلقائياً لكل tenant جديد:
  - **Admin** — كامل الصلاحيات
  - **Accountant** — Finance فقط
  - **ProjectManager** — Projects فقط
  - **Viewer** — قراءة فقط

### RefreshToken
- `TokenHash` (SHA-256 base64) — لا نخزن النص الصريح
- `ExpiresAt`، `RevokedAt`، `ReplacedByTokenHash`
- `IsActive = RevokedAt == null && Now < ExpiresAt`
- **Token Rotation**: كل refresh يُلغي القديم ويُولّد جديد
- **Reuse Detection**: استخدام refresh ملغى = `RevokeAllForUserAsync` (defense in depth)

## Auth Flows

### 1. Register

```
POST /api/auth/register
Body: {
  tenantId?: Guid,        // لربط بـ tenant موجود
  tenantName?: string,    // لإنشاء tenant جديد (يُحسب Subdomain من هذا الحقل)
  email: string,
  password: string,       // ≥8 chars, [A-Z], [a-z], [0-9]
  fullName: string,
  baseCurrency?: string   // default "LYD"
}
```

- **Validation:** يجب أن يكون `TenantId != Guid.Empty` أو `TenantName` غير فارغ
- إذا `tenantId` موجود: ربط بـ tenant موجود
- إذا `tenantName` موجود: إنشاء tenant جديد (Subdomain = Slugify(TenantName)) + Admin role للمستخدم الجديد
- `EnsureDefaultRolesAsync(tenantId)` يضمن وجود الأدوار الأربعة
- `BaseCurrency` يُمرر لـ `ITenantBootstrap.OnTenantCreatedAsync` (لإنشاء الـ holding company بنفس العملة)

### 2. Login

```
POST /api/auth/login
Body: { email: string, password: string, tenantId?: Guid }
```

- إذا `tenantId` موجود: بحث داخله
- وإلا: بحث شامل (لـ super-admin فقط)
- BCrypt.Verify + LastLogin update

### AuthResponse (مشترك بين register و login)

```csharp
{
  AccessToken: string,
  RefreshToken: string,
  AccessTokenExpiresAt: DateTime,
  RefreshTokenExpiresAt: DateTime,
  User: UserInfo,
  HoldingCompanyId: Guid  // للـ multi-company bootstrap
}
```

### 3. Refresh

```
POST /api/auth/refresh
Body: { accessToken, refreshToken }
```

- يفك Access Token (يقبل منتهي الصلاحية)
- يتحقق من RefreshToken في DB
- Rotation: يلغي القديم + يولد جديد

### 4. Logout

```
POST /api/auth/logout (Bearer required)
Body: { refreshToken }
```

- يلغي الـ Refresh Token المحدد

### 5. Me

```
GET /api/auth/me (Bearer required)
```

- يرجع UserInfo من الـ claims

## لما تشتغل هنا

- إضافة permission جديد: عدّل `UserRole` و أضف permission claims
- إضافة 2FA: فعّل `TwoFactorEnabled` logic و أضف endpoint جديد
- إضافة audit log: أنشئ `IdentityAudit` entity + migration

## بعد التعديل

- إذا أضفت entity جديد: اكتب migration جديدة (لا تعدّل القديمة)
- إذا غيّرت auth flow: حدّث قسم "Auth Flows" أعلاه
- أضف unit tests في `Tests/Auth/`

## مرتبطة بـ

- [`../../AGENTS.md`](../../AGENTS.md)
- [`../AGENTS.md`](../AGENTS.md)
- [`../../Shared/AGENTS.md`](../../Shared/AGENTS.md) — TenantContext, Migrations
- [`../../Host/AGENTS.md`](../../Host/AGENTS.md) — AuthController
- [`../Finance/AGENTS.md`](../Finance/AGENTS.md) — Tenant bootstrap (HoldingCompany + CoA)
- [`../Procurement/AGENTS.md`](../Procurement/AGENTS.md) — Phase 3
- [`../HR/AGENTS.md`](../HR/AGENTS.md) — Phase 3.5
- [`../Payroll/AGENTS.md`](../Payroll/AGENTS.md) — Phase 4


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../../../../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
