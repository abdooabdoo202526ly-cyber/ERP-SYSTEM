# 📚 docs/AGENTS.md

> مجلد التوثيق الرئيسي للمشروع.

## شو فيه

### 📋 Plans & Reports
- [`PLAN.md`](PLAN.md) — الخطة الكاملة للمشروع (8-10 أسابيع)
- [`CHANGELOG.md`](CHANGELOG.md) — سجل التغييرات الموثّقة
- [`SETUP-LOCAL.md`](SETUP-LOCAL.md) — دليل التشغيل بدون Docker (PostgreSQL 15 محلياً)
- [`FINAL-INTEGRATION-REPORT.md`](FINAL-INTEGRATION-REPORT.md) — تقرير Phase 2.5+
- [`RELEASE-REPORT-PHASE3.html`](RELEASE-REPORT-PHASE3.html) — 🆕 تقرير Phase 3 (HTML، RTL، Chart.js، 23KB)
- [`RELEASE-REPORT-PHASE4.html`](RELEASE-REPORT-PHASE4.html) — 🆕 تقرير Phase 4 (HTML، RTL، 28KB)
- [`SMOKE-TEST-REPORT.md`](SMOKE-TEST-REPORT.md) — تقرير smoke test للـ Phase 2.5+
- [`E2E-TEST-RESULT.json`](E2E-TEST-RESULT.json) — 🆕 نتائج E2E للـ Phase 3 (12/12 PASS)
- [`E2E-TEST-PHASE4/`](E2E-TEST-PHASE4/) — 🆕 نتائج E2E للـ Phase 4 (14/14 PASS)

### 🔬 Competitive Research (Phase 3)
- [`research/daftra-features.md`](research/daftra-features.md) — بحث Daftra (60KB)
- [`research/erpnext-features.md`](research/erpnext-features.md) — بحث ERPNext (64KB)
- [`research/odoo-reference.md`](research/odoo-reference.md) — مرجع Odoo (9.6KB)
- [`research/gap-analysis.md`](research/gap-analysis.md) — تحليل الفجوات + Phase 3 Scope (31KB، 8 أقسام)

## Conventions

- كل ملف markdown عالي المستوى يبدأ بـ H1 واضح
- الخطط والتقارير: استخدم التاريخ في اسم الملف (`2026-06-14-feature-name.md`)
- القرارات المعمارية الكبيرة: اكتب ADR (Architecture Decision Record) في `docs/adr/`

## لما تشتغل هنا

- حدّث `PLAN.md` عند تغيير phase أو sprint
- بعد أي تغيير كبير، تأكد أن الـ root `AGENTS.md` يعكس الوضع
- **🆕 عند إكمال phase جديد:**
  1. حدّث جدول `Phase Status` في الـ root AGENTS.md
  2. أضف entry في `docs/CHANGELOG.md` (في الأعلى)
  3. حدّث `PLAN.md` (الحالة + الـ PR)
  4. أنشئ release report في `docs/RELEASE-REPORT-<PHASE>.html` (إن كان إطلاق كبير)

## بعد التعديل

- إذا أضفت ملف جديد، اربطه من `root AGENTS.md` إذا كان مهماً
- إذا غيّرت خطة phase، حدّث جدول `Phase Status` في الـ root

## مرتبطة بـ

- [`../AGENTS.md`](../AGENTS.md) — التوثيق الجذر


---

## 🤝 Cross-Team Coordination (Brainstorming Lab)

This project works with an analytical team via the **Brainstorming Lab**.

- **When to read from hub**: ONLY when explicitly instructed by the analytical team
- **Default**: Work from local context (this file + root `AGENTS.md` + source code)
- **Hub repo**: https://github.com/anas600/brainstorming-lab/tree/main/portals/02-session-002/

See root [`AGENTS.md`](../AGENTS.md) for full cross-team protocol.

Token-efficient: ~50 tokens per cross-team directive (vs 500+ for full re-paste).
