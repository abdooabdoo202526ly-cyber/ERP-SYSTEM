# 🤖 AGENTS.md — ERP-SYSTEM (Root)

> **التوثيق الذاتي لـ AI Agents والـ humans معاً.** قبل أي تعديل، اقرأ من الجذر → للمجلد المطلوب.
> محدّث: Phase 4 — Payroll + EOS (يونيو 2026) — كل التغييرات من Phase 3/3.5/4 موثّقة

---

## 📌 نظرة عامة

نظام ERP متعدد المستأجرين (Multi-tenant Modular Monolith) للمرحلة الأولى (MVP). يتكون من **7 وحدات أعمال** (Identity + Companies + Finance + Projects + Inventory + Reports + Notifications) فوق أساس Multi-tenancy + Event Store + Outbox.

| الخاصية | القيمة |
|---------|--------|
| المنهجية | Agile / Scrum + Iterative MVP |
| المدة المتوقعة | 8-10 أسابيع |
| المالك | anas600 (https://github.com/anas600) |
| الترخيص | Private — جميع الحقوق محفوظة |
| الحالة | **Phase 4 مكتمل (PR #1 → #15)**، Phase 5 قادم |

---

## 🛠️ Tech Stack المعتمد

| الطبقة | التقنية | الإصدار | ملاحظات |
|--------|---------|---------|---------|
| Runtime | .NET | 9.0 | `net9.0` target (يعمل على SDK 9.x و 10.x) |
| Language (Backend) | C# | 12+ | Nullable Reference Types مفعّلة |
| Database (OLTP) | **PostgreSQL** | **15** | ✅ مُختبَر محلياً (15.18). 16+ مقبول |
| Database (Events) | PostgreSQL | 15 | نفس الـ instance، schema منفصل `mt_events` |
| Migrations | FluentMigrator | 5.0 | **10 migrations**: identity → finance → projects → inventory → outbox → procurement → hr → payroll |
| ORM | Dapper | 2.1+ | لا EF Core (القرار في PLAN.md) |
| Event Store | MartenDB | 7.34+ | حزمة مُثبّتة (Phase 3+)؛ حالياً Outbox pattern في Postgres |
| Cache/Queue | Redis | 7 | **اختياري** في dev؛ الكود يتفحص `ConnectionStrings:Redis` |
| Auth | JWT Bearer + BCrypt | — | Access 60min، Refresh 14 يوم، Token rotation، Reuse detection |
| Frontend | Next.js | 14.2 | App Router، RTL |
| Frontend Language | TypeScript | 5.5+ | Strict mode |
| UI Components | **Tailwind CSS** | 3.4 | ⚠️ shadcn/ui مذكور تاريخياً لكن **غير مُطبَّق** (لا يوجد `components/ui/`) |
| API Docs | Swashbuckle | 6.6+ | Swagger UI على `/swagger` |
| Testing | xUnit + FluentAssertions | — | `src/backend/Tests/` — عدد الاختبارات حسب الـ modules |
| Container | Docker Compose | 3.9 | `infra/docker/docker-compose.dev.yml` |
| CI | GitHub Actions | — | `.github/workflows/ci.yml` |

---

## 📁 Index للـ AGENTS.md الفرعية

قبل ما تعدّل أي ملف، اقرأ AGENTS.md للمجلد اللي بيشمله تعديلك:

| المسار | الوصف |
|--------|-------|
| [`docs/AGENTS.md`](docs/AGENTS.md) | خطة المشروع (PLAN.md) والتوثيق |
| [`src/AGENTS.md`](src/AGENTS.md) | كل الـ source code (backend + frontend) |
| [`src/backend/AGENTS.md`](src/backend/AGENTS.md) | الـ Backend (ASP.NET Core) |
| [`src/backend/Host/AGENTS.md`](src/backend/Host/AGENTS.md) | نقطة الدخول + Controllers + Swagger |
| [`src/backend/Modules/Identity/AGENTS.md`](src/backend/Modules/Identity/AGENTS.md) | Identity Module (Users, Roles, Tenants) |
| [`src/backend/Modules/Finance/AGENTS.md`](src/backend/Modules/Finance/AGENTS.md) | Finance Module (Phase 1) |
| [`src/backend/Modules/Projects/AGENTS.md`](src/backend/Modules/Projects/AGENTS.md) | Projects Module (Phase 2.1) |
| [`src/backend/Modules/Inventory/AGENTS.md`](src/backend/Modules/Inventory/AGENTS.md) | Inventory Module (Phase 2.2-2.3) |
| [`src/backend/Modules/Reports/AGENTS.md`](src/backend/Modules/Reports/AGENTS.md) | Reports Module (Phase 2.5) |
| [`src/backend/Shared/AGENTS.md`](src/backend/Shared/AGENTS.md) | كود مشترك (Tenant, Migrations, Events) |
| [`src/backend/Tests/AGENTS.md`](src/backend/Tests/AGENTS.md) | xUnit test projects |
| [`src/frontend/AGENTS.md`](src/frontend/AGENTS.md) | Next.js frontend |
| [`src/backend/Modules/Procurement/AGENTS.md`](src/backend/Modules/Procurement/AGENTS.md) | Procurement Module (Phase 3) |
| [`src/backend/Modules/HR/AGENTS.md`](src/backend/Modules/HR/AGENTS.md) | HR Core Module (Phase 3.5) |
| [`src/backend/Modules/Payroll/AGENTS.md`](src/backend/Modules/Payroll/AGENTS.md) | Payroll + EOS Module (Phase 4) |
| [`infra/AGENTS.md`](infra/AGENTS.md) | Docker + CI/CD |
| [`infra/docker/AGENTS.md`](infra/docker/AGENTS.md) | docker-compose + init-scripts |
| [`infra/.github/AGENTS.md`](infra/.github/AGENTS.md) | GitHub Actions workflows |

---

## 📐 معايير الكود (Code Standards)

### C# / Backend

- **Nullable Reference Types** مفعّلة (`<Nullable>enable</Nullable>`) — لا تترك null warnings
- **Async/Await**: كل IO-bound method يكون `async Task<T>`، لا تحجب الـ thread
- **Naming**: PascalCase للأسماء العامة، camelCase للمتغيرات المحلية والـ params
- **Comments**: **بالعربي** — المالك يفهمها أكثر. الـ code identifiers بالإنجليزي
- **DTOs**: في `Application/*/Dtos.cs` أو `*Dtos.cs` بجانب الـ handler
- **Entities**: في `Entities/` folder — كل entity في ملف منفصل
- **Validation**: FluentValidation، لا تتحقق داخل الـ service
- **Errors**: استخدم `Result<T>` patterns أو throw typed exceptions، لا تُرجع null بدون توثيق

### TypeScript / Frontend

- **Strict mode** مفعّل
- **Components**: Functional components فقط، مع hooks
- **Types**: TypeScript types، تجنب `any`
- **Comments**: بالعربي، الـ identifiers بالإنجليزي
- **Styling**: Tailwind CSS utility classes فقط (لا shadcn حتى الآن)
- **Auth client**: `lib/api.ts` يحوي Axios instance + JWT interceptors + localStorage

### SQL / Migrations

- **Migrations**: FluentMigrator، كل migration له version number فريد (timestamp)
- **Naming**: snake_case للجداول والأعمدة (Postgres convention)
- **Indexes**: أنشئ index على كل foreign key + أعمدة البحث الشائعة
- **Foreign Keys**: حدد `OnDelete` بشكل صريح (Cascade أو Restrict)

---

## 🧪 Testing Strategy (DEC-054)

نظام 3 طبقات (Testing Pyramid):

| Type | Location | Speed | Trigger |
|---|---|---|---|
| Unit (no DB) | `./scripts/local-verify.sh` | ~30 sec | Before every push |
| Integration (test DB) | `./scripts/local-integration.sh` (Docker) or CI Fast | ~2 min | On every push (ci-fast.yml) |
| Smoke (HF Space) | CI Deploy → auto-rollback check | ~10 min | On PR merge to develop (ci-deploy.yml) |

**Local testing إلزامي قبل push.** لا تدفع كود لا يجتاز `./scripts/local-verify.sh`.

---

## 🌿 Git Workflow

### Branch Strategy

- `main` — فرع الإنتاج، كل push يخضع لـ PR + review
- `develop` — فرع التطوير النشط (Integration branch) — كل الـ features تندمج فيه أولاً، ثم PR من `develop` → `main`
- `feature/<phase>-<scope>` — لكل feature
- `fix/<issue>` — لإصلاحات بسيطة
- `chore/<task>` — للصيانة (تحديث deps، توثيق)

### 🤝 توزيع الصلاحيات (Worker vs Owner Contract)

> **مهم:** الـ Workers والـ Owner (Mavis) عندهم صلاحيات مختلفة جداً.

| الفعل | Worker | Owner (Mavis) |
|------|--------|---------------|
| Commit + push على `feature/*` | ✅ | ✅ |
| فتح PR `feature/*` → `develop` | ✅ (في الـ prompt) | ✅ |
| فتح PR `develop` → `main` | ❌ | ✅ **المالك فقط** |
| Squash merge إلى `develop` | ❌ | ✅ |
| Squash merge إلى `main` | ❌ | ✅ |
| حذف `feature/*` branches | ❌ | ✅ |
| Push إلى `main` | ❌ | ❌ (لا أحد مباشرة) |
| تعديل `Program.cs` (modules list) | ❌ | ✅ |
| تعديل `AGENTS.md` files | ❌ | ✅ |

**Defense in depth (حتى لو worker أخطأ):**
1. **CI gating**: PR لا يُدمج إلا لو CI passes
2. **Base branch verification**: workers فقط يفتحون PRs لـ `develop` (ليس `main`)
3. **Squash merge**: حتى لو دخل commits مشبوهة، squash يضغطها في commit واحد موثّق
4. **Review**: المالك يراجع قبل merge
5. **Branch protection** (لو فعّلته على GitHub): main محمي تماماً

**Verified workflow (Phase 4):**
- Worker يكتب commits + يفتح PR #11 (`feature/phase-4-payroll-schema` → `develop`)
- CI يفحص
- المالك يراجع + `gh pr merge --squash --delete-branch`
- develop HEAD: `1e2f01f feat(payroll): Phase 4.1 - Payroll schema`

### Commit Convention

نستخدم Conventional Commits:

```
feat(identity): add refresh token rotation
fix(auth): handle expired access token correctly
docs(agents): implement DOX framework
chore(deps): bump Marten to 7.34
refactor(shared): extract TenantContext
test(auth): add JwtTokenService tests
```

### PR Rules

- عنوان PR واضح + وصف بـ "what" و"why"
- ربط بـ Issue أو Phase tag إن وُجد
- CI يمر قبل المراجعة
- Squash merge للـ `main`، يحتفظ بتاريخ الـ commits في `develop`

---

## 🌍 Multi-tenancy Convention

كل entity في أي module **يجب** أن يحتوي على `TenantId` (Guid). الـ `TenantContext` يُملأ من JWT claim `tenant_id` عبر `TenantMiddleware`. أي استعلام DB يجب أن يفلتر بـ `tenant_id` (القاعدة لاحقة — حالياً Auth module فقط يطبّقها، باقي الـ modules تبدأ مع Phase 1).

---

## 🔐 Secrets & Environment

- **لا تُحفظ** أي secrets في git (`appsettings.Production.json`, `.env`, tokens)
- `appsettings.json` يحوي placeholders فقط
- `appsettings.Development.json` موجود في repo (مع secrets dev فقط)
- الإنتاج: نستخدم environment variables أو Docker secrets

---

## 🛠️ Jimi Tech-Lead Tools (DEC-055, 2026-07-22)

Jimi (session `408773242015948` = "خطة-النظام") has elevated privileges to manage infrastructure:

### HF Space Control (`huggingface_hub` v1.24.0+)

| Capability | Tool | When |
|---|---|---|
| Deploy | `hf` CLI or `huggingface_hub.HfApi()` | After CI passes (manual trigger) |
| Start/Stop/Restart | `HfApi().run_space()` | When stale or rate-limited |
| Logs | `HfApi().space_logs()` | Debug deploy failures |
| Status | `https://huggingface.co/api/spaces/Anas-Assaket/erp-system` | Check before deploy |

**Token**: `HF_TOKEN` env var (sourced from `/workspace/.mavis/secrets/hf.token`, chmod 600)

### Neon DB Control (`psycopg2` + Neon API)

| Capability | Tool | When |
|---|---|---|
| SQL queries | `psycopg2.connect(NEON_URL)` | Read-only inspection, schema audit |
| Schema | `psql \d` or query `information_schema` | Migration verification |
| Migrations | FluentMigrator (in code, not Neon API) | Applied via app startup |
| Logs | Neon Console (https://console.neon.tech) | When queries fail |

**Token**: `NEON_API_KEY` env var (MCP-compatible) + `NEON_URL` for direct PG
**Project**: `lingering-feather-01780772` (erp-system-db, aws-eu-central-1, PG 16)

### ⚠️ Connection Lifecycle (CRITICAL — Anas Mandate)

**Rule**: Every connection → open → use → **close**. Never leave open.

```python
# Correct
import os, psycopg2
conn = psycopg2.connect(os.environ['NEON_URL'])
try:
    cursor.execute("SELECT ...")
    rows = cursor.fetchall()
finally:
    conn.close()  # ALWAYS
```

**Why**: Idle connections = paid Neon compute. Closed = free. Don't burn tokens.

### ⚠️ HF Rate Limit Warning

Currently the cloud sandbox IP (`47.253.4.207`) is rate-limited by HF (HTTP 429).
- **Don't retry immediately** — wait or work on other tasks
- HF Space auto-deploys from `develop` branch every push (via GitHub Action)
- Manual restart needed only when auto-deploy stalls (>10 min)

### Cross-Reference

- DEC-055: `/workspace/.mavis/DEC-2026-07-22-055-hf-neon-control-tools.md`
- Portal: https://anas600.github.io/brainstorming-lab/portals/04-erp-system/decisions/
- Backup tokens in: `/workspace/.mavis/secrets/` (chmod 600)

---

## 👥 Work Division (DEC-055, per Anas)

| Tool/Task | Owner |
|---|---|
| HF Hub CLI / Protel | Mavis |
| HF Space deploy (ERP-PORTAL) | Jimi |
| ERPNext execution | Jimi |
| BSY Configuration 2 | Jimi |
| Postgres config | Jimi |
| **Push** changes to ERP-SYSTEM | Jimi |
| Review Jimi's push | Mavis (DevOps) |
| Forward to Lab | Mavis (if complex) |

### Workflow (per Anas, FINAL)

1. **Jimi** يعمل شغل (BSY, Postgres, HF deploy)
2. **Jimi** يعمل push
3. **Jimi** يبعت "done" عبر Channel 5
4. **Mavis** يراجع كـ coordinator + DevOps
5. **Mavis** يحوّل للفريق التحليلي لو في قرارات معقدة

### Communication

- **Channel 5** (`communicate` tool) = standard communication
- Mavis = leader/reviewer, Jimi = implementation lead
- No cron job needed (Channel 5 is more flexible)

### Git Push from Jimi Sandbox

Jimi's local GITHUB_TOKEN has `repo` scope for `anas600/ERP-SYSTEM`. Pattern:

```python
# Use GitHub API (most reliable from cloud sandbox)
import urllib.request, json, base64
token = open('/root/.mavis/secrets/github.token').read().strip()
req = urllib.request.Request(
    'https://api.github.com/repos/anas600/ERP-SYSTEM/contents/AGENTS.md',
    data=json.dumps({
        'message': 'docs: ...',
        'content': base64.b64encode(open('AGENTS.md', 'rb').read()).decode(),
        'branch': 'develop'
    }).encode(),
    headers={'Authorization': f'token {token}', 'Content-Type': 'application/json'}
)
urllib.request.urlopen(req)
```

---

## 📅 Phase Status

| Phase | المحتوى | الحالة |
|-------|---------|--------|
| Phase 0 | Foundation + Identity | ✅ مكتمل (PR #1) |
| Phase 1 | Finance Core (CoA, Journal, GL, Rules Engine) | ✅ مكتمل (PR #2) |
| Phase 1.5 | Multi-Company Foundation (Companies, CostCenters) | ✅ مكتمل (PR #3) |
| Phase 2.1 | Projects Module (Project, Task, Resource, Budget) | ✅ مكتمل (PR #4) |
| Phase 2.2-2.3 | Inventory Core + Stock Movements | ✅ مكتمل (PR #5, #6) |
| Phase 2.4 | Event Bus + Integration (Outbox pattern) | ✅ مكتمل (PR #7) |
| Phase 2.5 | Reports + Polish (12 endpoints + 2 events) | ✅ مكتمل (PR #8) |
| **Phase 2.5+** | **Frontend integration (Next.js 8 pages) + Auth + Tailwind UI** | ✅ مكتمل |
| **Phase 3** | **Procurement Core (Vendor + PO + GR + Bill) + AppShell + 8 UI components** | ✅ مكتمل |
| **Phase 3.5** | **HR Core (Department + Employee + Attendance + Leave)** | ✅ مكتمل |
| **Phase 4** | **Payroll + EOS (Salary Structure, PayrollRun, Libya Tax, EOS Calculator, Payslip view)** | ✅ مكتمل (PR #11/#12/#13 → main #14) |
| **Phase 5.A Sprint 1** | **AR Foundation (Customers + SalesInvoices + Receipts + Aging AR)** | ✅ مكتمل (PR قادم) |

راجع [`docs/PLAN.md`](docs/PLAN.md) للتفاصيل الكاملة.

---

## 📝 Changelog (آخر التحديثات)

### 2026-06-24b — Mavis Telegram Architecture Guide 🆕

- [`docs/MAVIS-TELEGRAM-GUIDE.html`](docs/MAVIS-TELEGRAM-GUIDE.html): 🆕 دليل Mavis + Telegram التقني — معمارية Sessions، Routing، Lifecycle، Scenarios، الأوامر، توصيات التنظيف (25KB)

### 2026-06-24 — Phase 3: Procurement + HR + Frontend Foundation

**التغييرات المطبّقة:**

| المنطقة | التغيير |
|---------|---------|
| **Backend (جديد)** | Procurement Module (4 entities + 5 repos + 4 services + 11 endpoints + 7 جداول + 1 migration) + HR Core Module (4 entities + repos + services + controller + 4 جداول + 1 migration) |
| **Frontend (جديد)** | AppShell layout (sidebar + topbar + breadcrumb) + 8 UI components (Button, Input, Select, Table, Badge, Card, Modal, PageHeader) + 12 صفحة (Procurement: vendors/POs/GRs/Bills list+form، HR: employees/attendance/leaves list+form) |
| **API Contracts** | `procurementApi.*` و `hrApi.*` في `lib/api.ts` بنفس النمط (axios + JWT) |
| **Migrations** | `20260623_120000_CreateProcurementTables.cs` + `20260623_130000_CreateHRTables.cs` |
| **AGENTS.md جديدة** | `src/backend/Modules/HR/AGENTS.md` (Procurement كان موجود) — فهرسة كاملة في الـ root |
| **Phase Status** | Phase 3 + Phase 3.5 + Phase 4 → ✅ مكتمل، Phase 5 → 📋 قادم |
| **توثيق** | `docs/research/` (Daftra, ERPNext, Odoo, gap-analysis) + `docs/RELEASE-REPORT-PHASE3.html` (23KB) |
| **E2E Test** | 12/12 PASS — 100% — مسجّل في `docs/E2E-TEST-RESULT.json` |

**قاعدة جديدة للـ workflow:** كل المهام الكبيرة (modules جديدة + frontend + research) لا بد من تحديث الـ AGENTS.md files المعنية + إضافة entry في `docs/CHANGELOG.md` + commit منفصل.

### 2026-06-17 — توثيق vs كود: تسوية الحقائق

**التغييرات المطبّقة في AGENTS.md files بناءً على الكود الفعلي:**

| الملف | التغيير |
|------|--------|
| `AGENTS.md` (root) | PostgreSQL 16 → **15**؛ shadcn/ui → Tailwind CSS (مع تنبيه)؛ إضافة Phase 2.5+ |
| `src/frontend/AGENTS.md` | إزالة shadcn من Tech Stack؛ تحديث Auth contracts (إزالة subdomain)؛ إضافة lib/api.ts في الهيكل |
| `src/backend/Modules/Identity/AGENTS.md` | إضافة `BaseCurrency` للـ Register؛ توثيق Slugify (subdomain يُحسب تلقائياً)؛ إضافة `HoldingCompanyId` |
| `infra/docker/AGENTS.md` | إضافة قسم init-scripts (ينشئ DBs من `POSTGRES_MULTIPLE_DATABASES`) |
| `infra/docker/docker-compose.dev.yml` | `postgres:16-alpine` → `postgres:15-alpine` (تطابق AGENTS) |
| `src/frontend/lib/api.ts` | ✅ **إصلاح bug:** إزالة `subdomain` من `RegisterRequest`؛ استبداله بـ `BaseCurrency` |
| `src/frontend/app/register/page.tsx` | ✅ **إصلاح bug:** إزالة حقل subdomain من الـ form (كان يتم تجاهله من قبل الـ backend) |
| `docs/CHANGELOG.md` | جديد — سجل التغييرات |

---

## 🤝 لما تنضم للـ repo (AI Agent جديد)

1. اقرأ هذا الملف (root AGENTS.md) كاملاً
2. ارجع للـ AGENTS.md الخاصة بالمجلد اللي بتشتغل فيه
3. افهم الـ patterns المستخدمة (Dapper + Marten + Multi-tenancy)
4. لا تخترع patterns جديدة — اتبع الموجود
5. اكتب tests لكل feature جديد
6. حدّث AGENTS.md المعني إذا أضفت pattern جديد أو غيّرت بنية

---

**حافظ على هذا الملف محدّثاً** عند إضافة AGENTS.md جديدة أو tech جديد.

---

## 🌿 Branching Strategy (DEC-052)

This project uses **GitHub Flow + develop branch**:

- `main` = production (protected — required reviews + CI check)
- `develop` = integration (protected — required reviews + CI check)
- `feature/*`, `fix/*`, `hotfix/*`, `docs/*` = working branches

### Before starting work:

1. Read AGENTS.md (this file)
2. Check existing branches: `git branch -a`
3. Create branch from `develop` (or `main` for hotfixes):
   ```bash
   git checkout develop
   git pull origin develop
   git worktree add ../wt-$(name) -b <branch-name> develop
   ```

### Branch naming:

- `feature/<epic>-<description>` (e.g., `feature/M1-add-login`)
- `fix/<issue-number>-<description>` (e.g., `fix/123-alburj-bug`)
- `hotfix/<description>` (e.g., `hotfix/critical-prod-fix`)
- `docs/<description>` (e.g., `docs/update-readme`)

See `.github/BRANCHING.md` for full details.

---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project has an analytical team connected via the **Brainstorming Lab** repo.

### Hub

- **Hub repo**: https://github.com/anas600/brainstorming-lab
- **Session folder**: `portals/02-session-002/`

### How to read from the hub

- **Default**: Work from **local context** — AGENTS.md, RUNBOOK.md, source code, git history.
- **When to read from hub**: **ONLY when explicitly instructed by the analytical team** (e.g., "read SYSTEM.md §4" or "see decisions/DEC-042").
- **Read specific files, not all**: Each directive names the file. Don't read SYSTEM.md + ROLE-CLARIFICATION.md + SESSION.md every task — that's a token waste.

### Hub files (read on-demand)

| File | When |
|---|---|
| `SYSTEM.md` | Constitution (referenced by section number) |
| `CROSS-TEAM-COORDINATION.md` | Cross-team protocol (read once, then reference) |
| `board.md` | Live ERP-SYSTEM progress (read for context) |
| `tasks.md` | Task tracker (read for your pending tasks) |
| `decisions/DEC-NNN-*.md` | Specific decision file (when cited) |

### Pattern

1. Receive directive (mention "read X" or "see DEC-NNN")
2. Read the specific referenced file
3. Do the work
4. Push + report back
5. Wait for review

### Token efficiency

- Reading a specific file: ~50 tokens in the directive
- Reading the whole hub every task: ~500 tokens (10× waste)
- Rule: **only read what's referenced**
