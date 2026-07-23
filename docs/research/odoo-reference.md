# Odoo كمرجع Industry-Standard — ملف موجز

> مرجع مكثّف عن **Odoo** كنظام ERP مفتوح المصدر تجاري رائد، لاستخلاص الأنماط المعمارية و UX/UI القابلة للتطبيق في **ERP-SYSTEM**. التفاصيل الكاملة موجودة في `daftra-features.md` و `erpnext-features.md`.

**التاريخ:** 23 يونيو 2026 — **المصادر:** Odoo 19.0 Docs الرسمية — **الإصدار المرجعي:** Odoo 19.0.

---

## 1. لماذا Odoo كمرجع؟

**Odoo** أكبر **open-source ERP تجاري** في العالم — **+12M مستخدم**، **+44K تطبيق** على App Store، نموذج **Open Core** (Community مجاني + Enterprise مدفوع). يُعدّ **مرجع الصناعة (industry-standard)** لأنه يجمع بين مرونة المصدر المفتوح، نضج الهندسة المعمارية، وتغطية SMB/Enterprise كاملةً في منصة واحدة قابلة للتوسعة بالـ modules.

---

## 2. أهم 10 أنماط معمارية تبنّاها Odoo

### 2.1 Modular Apps
كل وظيفة = **module** مستقل داخل `addons/` (`__manifest__.py` + Python + XML + assets). تركيب/إزالة أي module دون لمس الباقي — تفعيل/تعطيل ميزات كاملة من واجهة الإعدادات. **→ ERP-SYSTEM:** نمذجة كل وحدة (Identity, Finance, Inventory, Projects, Reports, Notifications) كـ module منفصل بمسار واضح.

### 2.2 Inheriting & Extending (لا تُعدّل الـ core)
آليتان رسميتان: **`_inherit`** على Python class لإضافة/تعديل fields, methods, constraints بدون لمس كود model الأصلي، و**View inheritance** عبر `inherit_id` + `<xpath expr="..." position="after">` لتعديل أي view بـ XPath. > "Allow you to extend existing third-party apps without changing them directly." — Odoo 19 Docs, Ch.12. **→ ERP-SYSTEM:** قابلية الترقية بلا كسر، فصل الـ customizations في طبقة خاصة.

### 2.3 Cron Jobs & Scheduled Actions (`ir.cron`)
مهام دورية تُعرَّف في `ir.cron`: اسم، interval (`interval_number`, `interval_type`: minutes/hours/days)، كود Python، retry logic، وتنفيذ logging. أمثلة: تذكير الفواتير المتأخرة، إعادة حساب المخزون، مزامنة البنك. **→ ERP-SYSTEM:** خلفية jobs للتقارير، التسويات، الإشعارات، الأرشفة.

### 2.4 Wizards (`TransientModel`)
**TransientModel** = model مؤقت يُحذف تلقائيًا بعد العملية. لعمليات متعددة الخطوات: تأكيد أمر بيع، استيراد CSV، دمج جهات اتصال، إقفال سنة مالية. **→ ERP-SYSTEM:** Setup/Import/Period Close Wizard بدل شاشة ثقيلة واحدة.

### 2.5 Server Actions (`ir.actions.server`)
إجراءات قابلة لإعادة الاستخدام تُحفَّز من زر في الـ Form View، أو قائمة Action في List View، أو من Cron. أنواعها: Execute Python، Create/Write Record، Multi، Send Email، Webhook. **→ ERP-SYSTEM:** أتمتة بدون كود (Approve, Reject, Archive, Duplicate).

### 2.6 Email Templates (`mail.template`)
قوالب بريد QWeb + placeholders (`${object.partner_id.name}`) تُرسَل على events (تأكيد طلب، فاتورة مستحقة، عرض سعر). لغات متعددة وlayouts قابلة للتخصيص. **→ ERP-SYSTEM:** template واحد عربي/إنجليزي، عدة triggers.

### 2.7 QWeb Templates
محرك templating XML مدمج لـ: **PDF Reports** (فواتير، عروض أسعار، كشوف حساب) عبر `wkhtmltopdf`؛ **Web pages** (eCommerce، Customer Portal)؛ **Kanban cards** (بطاقات ديناميكية)؛ **Email body**. **→ ERP-SYSTEM:** طباعة احترافية مع تخصيص Studio-like.

### 2.8 Chatter (نشاط/محادثات على الـ record)
كل model يرث من `mail.thread` يحصل تلقائيًا على: شريط رسائل (`message_ids`)، Followers (`message_follower_ids`)، Activities المجدولة (`activity_ids`)، Tracking للحقول المهمة. **مثال:** `_inherit = ['mail.thread', 'mail.activity.mixin']`. **→ ERP-SYSTEM:** Audit Log مدمج + تواصل سياقي في كل entity جوهري.

### 2.9 Multi-Company بـ Allowed Companies
المستخدم يفعّل عدة شركات في نفس الجلسة (`allowed_companies`) ويرى/يعدّل سجلاتها دفعةً واحدة. عبر: `company_id` field (مع `check_company=True`)، `company_dependent=True` على الحقول، `_default_company_id` و group `base.group_multi_company`. **→ ERP-SYSTEM:** جاهز لتوسعة Multi-Company — فصل بيانات مع تجربة موحدة.

### 2.10 Record Rules (`ir.rule`) للـ Multi-Tenancy و Security
قواعد row-level (Domain filters) مرتبطة بـ Groups: Row-Level Security، Multi-Company isolation، Field-Level Access. **مثال:** `<record model="ir.rule"><field name="domain_force">[('company_id','in',company_ids)]</field></record>`. **→ ERP-SYSTEM:** Rule-based filter بدل hardcoded WHERE — عزل company داخل نفس الـ tenant.

---

## 3. أهم 3 أنماط UX/UI

### 3.1 View Types متعددة لنفس الـ Model
**Form** (تفاصيل السجل + tabs + status bar)، **List** (جدول sortable/edit-inline)، **Kanban** (بطاقات drag-and-drop بين Stages)، **Calendar** (تقويم)، **Graph/Pivot** (تحليلات)، **Gantt** (مشاريع)، **Activity** (كل الـ activities عبر الـ models). المستخدم يختار الواجهة الأنسب دون قفز بين شاشات.

### 3.2 Search View: Filters / Group By / Search
شريط موحد في كل List/Kanban: **Search box** ذكي، **Filters** مسبقة (My Drafts, Late Invoices, This Month) قابلة للحفظ كـ Custom Filter، **Group By** (حسب الحالة/العميل/الفترة)، **Comparison** (هذا الشهر vs الماضي)، **Favorites** كزر في الشريط. تجربة موحّدة لا تحتاج بناء شاشات بحث خاصة لكل وحدة.

### 3.3 Dashboard بـ KPI Tiles + Charts
Dashboards (Accounting, Inventory, Purchase) تجمع: **KPI Tiles** بأرقام كبيرة + نسبة التغيير + سهم لأعلى/أسفل، **Charts** تفاعلية (line/bar/pie/cohort)، **Quick Action Buttons**، **Drill-down** من tile إلى List View مع filter مُطبَّق. لوحة Executive بقوالب جاهزة قابلة للتخصيص.

---

## 4. دروس مستفادة لـ ERP-SYSTEM

| # | النمط | التطبيق المقترح |
|---|------|----------------|
| 1 | **Modular Apps** | كل module في مجلد منفصل مع manifest يوضح الـ dependencies. |
| 2 | **Inheritance** | قاعدة **"لا تعدّل كود core module من module آخر"** — extension hooks واضحة. |
| 3 | **Cron Jobs** | HostedService بسيط في الـ Backend لاستدعاء handlers دورية. |
| 4 | **Wizards** | شاشة wizard لعمليات متعددة الخطوات بدل شاشة ثقيلة واحدة. |
| 5 | **Server Actions** | مكتبة إجراءات قابلة لإعادة الاستخدام تُربط بأزرار. |
| 6 | **Email Templates** | Templates موحدة عربي/إنجليزي مع preview قبل الإرسال. |
| 7 | **QWeb** | محرر قوالب تقارير/PDF مع variables وconditionals. |
| 8 | **Chatter** | كل entity جوهري يحصل على Activity Log + Comments + Audit Trail. |
| 9 | **Multi-Company** | `company_id` على entities المالية + Rules للعزل (Phase 5+). |
| 10 | **Record Rules** | Rule-based filter لاستكمال TenantContext بدل hardcoded WHERE. |

**UX Quick Wins:** (أ) اعرض نفس الـ List Page بأوضاع Table/Cards/Timeline يُبدّلها المستخدم. (ب) شريط Search موحد في كل صفحة قائمة. (ج) Dashboard في الـ Home بكروت KPI + رسم بياني واحد.

---

## 5. روابط مرجعية رسمية (3 روابط)

1. **Architecture Overview — Odoo 19.0:** https://www.odoo.com/documentation/19.0/developer/tutorials/server_framework_101/01_architecture.html
2. **Actions Reference (Server/Cron/Wizards) — Odoo 19.0:** https://www.odoo.com/documentation/19.0/developer/reference/backend/actions.html
3. **Multi-company Guidelines — Odoo Developer Docs:** https://www.odoo.com/documentation/19.0/developer/howtos/company.html

---

## 6. أولوية التطبيق في ERP-SYSTEM

- **Phase 2.5+ (الحالي):** Modular Apps + Search View الموحد.
- **Phase 3:** Inheritance + Server Actions + Email Templates (تمهيد لـ Open API وStudio-like UI).
- **Phase 4:** Chatter + Wizards + Cron (Collaboration + Reports دورية).
- **Phase 5+:** QWeb Reports + Multi-Company + Record Rules (Enterprise-level).

**حدود البحث:** ملف موجز تنفيذي وليس توثيقًا تقنيًا كاملاً. الأنماط العشرة هي الأكثر نضجًا وانتشارًا وقابلة للتطبيق مباشرةً. كل الأمثلة والروابط تشير إلى **Odoo 19.0**؛ الأنماط الأساسية لم تتغيّر جوهريًا منذ Odoo 12.

> **التوصية:** اقرأ القسمين 2 و 3 فقط — تحوي كل ما يلزم لاتخاذ قرارات معمارية سريعة في ERP-SYSTEM.