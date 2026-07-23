# ERPNext — بحث شامل عن الميزات، الموديولات، والـ Architecture

> **تاريخ الإعداد:** 2026-06-23
> **المُعدّ:** Mavis (general agent) — مَهمة `research-erpnext` ضمن خطة `plan_b5ae4fc0` (إكمال Phase 3+ مع البحث التنافسي)
> **الغرض:** ملف مرجعي تنافسي يُقارَن لاحقاً مع Daftra و Odoo و ERP-SYSTEM في `gap-analysis.md`.
> **الترخيص والـ source:** ERPNext صدر تحت **GPL-3.0-only**، مشروع مفتوح المصدر تابع لشركة Frappe Technologies Pvt. Ltd. (الهند)، المصدر على [github.com/frappe/erpnext](https://github.com/frappe/erpnext).

---

## جدول المحتويات

1. [نظرة عامة (Overview)](#1-نظرة-عامة-overview)
2. [الموديولات الكاملة (Modules)](#2-الموديولات-الكاملة-modules)
3. [أفضل 18 ميزة تقنية فريدة (Unique Technical Features)](#3-أفضل-18-ميزة-تقنية-فريدة-unique-technical-features)
4. [الـ Architecture وهيكل قاعدة البيانات](#4-الـ-architecture-وهيكل-قاعدة-البيانات)
5. [قدرات الـ API (REST, Webhooks, Frappe Client)](#5-قدرات-الـ-api-rest-webhooks-frappe-client)
6. [روابط المصادر](#6-روابط-المصادر)
7. [القيود والتحديات (للمقارنة)](#7-القيود-والتحديات-للمقارنة)
8. [التسعير ونماذج النشر](#8-التسعير-ونماذج-النشر)
9. [سجل الإصدارات الرئيسي (للسابقة التاريخية)](#9-سجل-الإصدارات-الرئيسي-للسابقة-التاريخية)

---

## 1. نظرة عامة (Overview)

**ERPNext** هو نظام تخطيط موارد المؤسسة (ERP) متكامل، حر ومفتوح المصدر، طوّرته شركة **Frappe Technologies Pvt. Ltd.** ومقرها بنغالور (الهند). النظام مبني على **Frappe Framework** — إطار عمل full-stack مكتوب بـ **Python + JavaScript** يستخدم **MariaDB** (أو PostgreSQL منذ v12) كقاعدة بيانات، و **Redis** للكاش والـ pub-sub، و **Node.js** لتشغيل خادم Socket.IO للـ realtime. صدرت النسخة الأولى في يونيو 2010، وآخر إصدار مستقر وقت كتابة هذا التقرير هو **v15.76.0** (أغسطس 2025)، مع **v16** beta الذي صدر في ديسمبر 2025.

**نموذج النشر مزدوج:**

- **Self-hosted (مجاني تماماً)**: تنصيب على خوادم Linux (Ubuntu موصى به) عبر أداة `bench`، تحصل على كامل الـ source code والـ control على الـ infrastructure. لا رسوم ترخيص إطلاقاً.
- **Frappe Cloud (مدفوع)**: استضافة مُدارة من Frappe نفسها، تبدأ من **$5/شهر** للخطة المشتركة، وتصل إلى **$400+/شهر** للخطط المخصصة. تتوفر أيضاً **Enterprise Services** مع SLAs ودعم مخصص.

**المجتمع:** مجتمع نشط جداً مع آلاف المساهمين، Frappe School للتدريب، Frappe Cloud Marketplace للتطبيقات، FrappeVerse Africa و Frappeverse India للفعاليات، وFOSS United كمنظمة غير ربحية داعمة. حصلت Frappe على استثمار بقيمة ₹10 crore ($1.3M) من Zerodha/Rainmatter في نوفمبر 2020. النظام مذكور في **Gartner FrontRunners** لـ ERP، ويُستخدم من شركات صغيرة ومتوسطة وصناعات متنوعة (تصنيع، توزيع، تعليم، صحة، جمعيات، خدمات مالية).

---

## 2. الموديولات الكاملة (Modules)

> **ملاحظة هامة للمقارنة:** اعتمدنا نفس تصنيف ملف `daftra-features.md` لإتاحة المقارنة العادلة 1:1.

### 2.1. Accounting (المحاسبة)

موديول محاسبي شامل ومتعدد العملات، يُغطي كامل دورة المحاسبة:

- **Chart of Accounts** مرن قابل للتخصيص بالكامل (شجرة حسابات غير محدودة)
- **General Ledger** مع Journal Entries يدوية وتلقائية
- **Accounts Receivable / Accounts Payable** (ذمم مدينة/دائنة)
- **Sales Invoice, Purchase Invoice, Credit Note, Debit Note**
- **Payment Entry** متعدد الأساليب (نقد، شيك، تحويل بنكي، بطاقة)
- **Payment Reconciliation** مع البنوك
- **Payment Ledger** (سجل المدفوعات التفصيلي، أُضيف في v14)
- **Multi-Currency Accounting** مع إعادة تقييم العملة تلقائياً (Auto Currency Exchange Revaluation أُضيف في v15)
- **Multi-Company** بقوائم مالية مُوَحَّدة (Consolidated Financial Statements)
- **Cost Centers** و **Accounting Dimensions** (شرائح محاسبية قابلة للتخصيص)
- **Budgeting** (مقارنات الموازنة بالفعلي)
- **Taxes & Charges Templates** (ضريبة قيمة مضافة مهيأة للسعودية KSA، تنزانيا، الهند GST، إلخ)
- **Financial Reports**: Balance Sheet, Profit and Loss, Cash Flow, Trial Balance
- **Tax Withholding** (خصم ضريبة من المنبع)
- **Provisional Accounting** للخدمات والمصروفات المسبقة
- **Perpetual Accounting** لأنظمة الخدمات
- **Finance Book** (دفاتر مالية متعددة) — أُضيف في v11
- **Asset Capitalization** و **Asset Grouping/Splitting** (v14)
- **Currency Exchange Revaluation** (تقييم فروقات العملة)
- **Period Closing Voucher** (إقفال الفترات المحاسبية)
- **الإفصاحات الضريبية المحلية**: KSA, Tanzania, India GST, UAE VAT

### 2.2. Sales (المبيعات)

- **Customer** master (يستخدم نموذج "Party" الموحد للعملاء والموردين)
- **Quotation** (عروض أسعار) مع إمكانية الإصدار PDF مهيأ
- **Sales Order** (أوامر بيع) مع workflow للموافقة
- **Delivery Note** (سند التسليم) مع ربط المخزون
- **Sales Invoice** (فاتورة بيع) مع الضرائب
- **Sales Return** (مرتجعات)
- **Recurring Sales** (فواتير دورية)
- **Sales Partner** (وكلاء/مسوقين بالعمولة)
- **Pricing Rules** متقدمة (خصومات حسب الكمية، العميل، المنطقة)
- **Promotional Scheme** (حملات ترويجية، أُضيف v12)
- **Sales Order Stock Reservation** (حجز المخزون لأمر البيع، v15)
- **Payment Schedule** (جدول دفعات للعميل)
- **Customer Credit Limit** (سقف ائتماني)
- **Loyalty Program** (نقاط الولاء)
- **Multi-UOM** في البيع (وحدات قياس متعددة، v8)
- **Sales Analytics & Reports**

### 2.3. Purchase (المشتريات / Procurement)

- **Supplier** master (نفس نموذج Party)
- **Material Request** (طلب مواد داخلي)
- **Request for Quotation (RFQ)** و **Supplier Quotation**
- **Purchase Order** مع workflow موافقة
- **Purchase Receipt** (سند استلام بضاعة — Goods Receipt Note)
- **Purchase Invoice** (فاتورة مشتريات)
- **Purchase Return**
- **Subcontracting** (تصنيع خارجي/مقاول باطن) — أُعيد تصميمه في v14
- **Purchase Analytics & Reports**
- **Landed Cost Voucher** (تسعير التكلفة الكلية: شحن، جمارك، تأمين)
- **Purchase Order Item Status** (حالة البند: استلم/فاتورة/مدفوع)
- **Auto-Purchase Receipt** من فاتورة المورد
- **Landed Cost** allocation على الـ items

### 2.4. Stock (المخازن / Inventory / Warehouse)

- **Item** master مع Variants (متغيرات الصنف: لون، حجم، إلخ)
- **Item Group** (مجموعات هرمية)
- **Warehouse** (مخازن متعددة، شجرية)
- **Stock Entry** (حركة مخزون)
- **Stock Reconciliation** (تسوية جرد فعلي)
- **Inventory Dimension** (أبعاد المخزون: دفعة، رقم سيريال، رف، مشروع — أُضيف v14)
- **Batch Tracking** و **Serial Number Tracking** (تتبع أرقام السيريال/الدفعات)
- **Bin Location** (مواقع داخل المستودع)
- **Stock Ledger Entry** (دفتر الأستاذ المخزني)
- **Stock Reservation against Sales Order** (v15)
- **Price List** متعددة (بعملات مختلفة)
- **Item Valuation** (تقييم المخزون: FIFO / Moving Average)
- **Scrap Management** (إدارة الهالك، v14)
- **Delivery Trip** (رحلة التوزيع)
- **Pick List** (قائمة انتقاء للـ Wave Picking)
- **Warehouse Capacity Planning**
- **Stock Aging & Stock Summary reports**

### 2.5. Manufacturing (التصنيع)

- **Bill of Materials (BOM)** متعدد المستويات (Multi-level BOM)
- **BOM Template** و **Multi-level BOM Creator** (v15)
- **Production Plan** (خطة الإنتاج)
- **Work Order** (أمر شغل)
- **Job Card** (بطاقة عمل لكل محطة)
- **Workstation** (محطة عمل)
- **Operation** (عملية تصنيع)
- **Routing** (مسار التصنيع)
- **Subcontracting** (مقاول باطن) — تدفق جديد في v14
- **Production Tracking** على مستوى Job Card (v16)
- **Quality Inspection** أثناء التصنيع
- **BOM Explorer** (مستعرض شجرة الـ BOM، v12)
- **Production Forecast** (توقعات الإنتاج، v13)
- **Capacity Planning**
- **Downtime Tracking** (تتبع الأعطال)
- **Scrap/Waste Management** للعمليات

### 2.6. HR (الموارد البشرية)

- **Employee** master (بيانات شاملة)
- **Department, Designation, Grade, Branch, Location** (هيكل تنظيمي)
- **Employee Onboarding / Offboarding** (v11)
- **Employment Contract**
- **Attendance** (CheckIn / CheckOut) مع GPS location
- **Auto Attendance** (من الجيولوكation، v12)
- **Leave Application** و **Leave Policy** (سياسات إجازات)
- **Leave Type** (أنواع إجازات: سنوية، مرضية، طارئة)
- **Leave Ledger** (دفتر رصيد الإجازات، v12)
- **Leave Allocation** (تخصيص رصيد)
- **Holiday List** (قائمة العطل الرسمية)
- **Shift Management** (إدارة المناوبات، Shift Plan في v11)
- **Shift Assignment**
- **Employee Advance** (سلفة موظف)
- **Expense Claim** (مطالبة مصروف)
- **Travel Request** (طلب سفر)
- **Employee Loan** (قرض موظف، v8)
- **Employee Performance** (إدارة الأداء)
- **Appraisal** (تقييم أداء)
- **Recruitment** (Job Opening, Job Applicant, Interview)
- **Staffing Plan** (خطة التوظيف، v11)
- **Training Program / Event**
- **Energy Points** (نظام Gamification، v12)
- **Organisational Chart** (الهيكل التنظيمي التفاعلي، v14)
- **PWA Mobile App** لموديول HR (v15)

### 2.7. Payroll (الرواتب)

- **Salary Component** (مكونات الراتب: أساسي، بدل سكن، بدل نقل، خصومات)
- **Salary Structure** (هيكل الراتب)
- **Salary Structure Assignment** (تعيين الهيكل للموظف)
- **Salary Slip** (قسيمة الراتب)
- **Payroll Entry** (معالجة شهرية للرواتب)
- **Payroll Period** (فترة الرواتب)
- **Income Tax Slab** (شرائح ضريبة الدخل)
- **Tax Declaration** (إقرار ضريبي)
- **Provident Fund** (تأمينات/صندوق تقاعد)
- **Gratuity** (مكافأة نهاية الخدمة)
- **Payroll per Tax Declaration** (v11)
- **Accrual system in Payroll** (v8)
- **Multi-Country Payroll** (India PF/PT، إلخ)
- **Payroll Integration** مع Accounting (ينشئ Journal Entry تلقائياً)
- **EOS (End of Service) calculation** للعقود المنتهية
- **Retrospective Payroll Component** (حساب بأثر رجعي)

### 2.8. Projects (المشاريع)

- **Project** (مشروع) مع أنواع: External, Internal
- **Task** (مهمة) مع Task Type و Task Weight
- **Gantt Chart** (عرض جانت التفاعلي)
- **Kanban View** (لوحة كانبان)
- **Calendar View**
- **Task Dependency** (تبعيات بين المهام)
- **Project Template** (قوالب مشاريع، v12)
- **Timesheet** (بطاقة دوام) — تُحسب تلقائياً مقابل الـ Billing Rate
- **Activity Cost** (تكلفة النشاط)
- **Project Profitability** (ربحية المشروع)
- **Project Budgeting**
- **Billing based on Timesheet** (فوترة العميل بناءً على ساعات العمل)
- **Sales Invoice from Project** (فاتورة من مشروع)
- **Purchase Invoice from Project**
- **Project Dashboard** مع KPIs
- **Milestones** (نقاط بارزة)
- **Issue Tracking** (تتبع المشاكل) عبر موديول Support

### 2.9. CRM (إدارة علاقات العملاء)

- **Lead** (عميل محتمل) — في الـ top of the funnel
- **Lead Source** tracking
- **Lead Assignment Rule** (توزيع تلقائي للرصاص)
- **Opportunity** (فرصة) — مع Probability % و Expected Closing Date
- **Quotation from Opportunity** (عرض سعر من الفرصة)
- **Customer** (عميل مُحَوَّل)
- **Contact** (جهة اتصال) و **Address** (عناوين متعددة لكل عميل)
- **Pipeline View** (عرض مسار المبيعات)
- **Sales Funnel Analytics**
- **Email Integration** (ربط البريد الوارد بـ Lead/Issue، v8)
- **Newsletter** و **Email Campaign** (v12)
- **Exotel Call Integration** (v12) + Twilio
- **Google Contacts sync** (v12)
- **Campaign** و **Campaign Management**
- **Lost Reason** tracking
- **Competitor Tracking** (متابعة المنافسين)
- **Chatter** (timeline على كل document: مكالمات، إيميلات، ملاحظات)
- **Social Media Post** من ERPNext (v13)

### 2.10. Assets (الأصول الثابتة)

- **Asset** master (أصل ثابت) مع Auto-Creation من Purchase Invoice
- **Asset Category** (مجموعات الأصول)
- **Asset Location** و **Asset Custodian**
- **Depreciation Method** (طرق الإهلاك: Straight Line, Written Down Value, Manual)
- **Depreciation Schedule** (جدول الإهلاك التلقائي)
- **Finance Book** للأصول (دفاتر محاسبية متعددة)
- **Asset Value Adjustment** (تعديل قيمة الأصل)
- **Asset Repair** (صيانة الأصل)
- **Asset Scrap / Disposal** (استبعاد الأصل)
- **Asset Movement** (نقل الأصل بين مواقع)
- **Asset Capitalization** (v14) — تحويل المصروفات إلى أصل
- **Asset Grouping and Splitting** (v14) — تجميع/تقسيم الأصول
- **Asset Activity Tracking** (v15)
- **CWIP Accounting** (Capital Work In Progress، v11)
- **Serialised Assets** (أصول مُسلسلة، v11)
- **Asset Depreciation** (v7)

### 2.11. POS (نقطة البيع)

- **POS Profile** (إعدادات نقطة البيع)
- **POS Invoice** (فاتورة بيع فورية)
- **POS Settings** (إعدادات: طابعة، قارئ باركود، طريقة دفع)
- **Offline Mode** (يعمل بدون إنترنت، v7)
- **Online/Offline sync** (مزامنة عند عودة الاتصال)
- **Multiple Payment Modes** (نقد، بطاقة، آجل)
- **Barcode Scanner Integration** (دعم قارئ باركود)
- **POS Stock Update in real-time** (v15)
- **Customer Display** (شاشة عرض العميل)
- **Receipt Printer** integration
- **Cash Drawer** support
- **Shift Management in POS** (فتح/إغلاق وردية)
- **POS Closing Entry** (تقرير إغلاق الوردية)
- **Return / Exchange** في الـ POS
- **Saved Cart** (حفظ سلة مُؤقتة)
- **Multi-warehouse POS**
- **PWA Mobile POS** (responsive على التابلت)

### 2.12. Quality (إدارة الجودة)

- **Quality Inspection** (فحص جودة) — مرتبط بـ Purchase Receipt / Manufacturing / Delivery
- **Quality Inspection Template** (قوالب فحص قابلة للتخصيص)
- **Quality Procedure** (إجراءات الجودة)
- **Quality Goal** (أهداف الجودة)
- **Quality Review** (مراجعة الجودة)
- **Quality Action** (إجراء تصحيحي)
- **Non-Conformance** (حالات عدم المطابقة)
- **Sample Size Definition** (حجم العينة المطلوب فحصها)
- **Reading fields** (نموذج قراءة قابل للتخصيص لكل صفة)
- **Acceptable Range** (نطاق مقبول لكل صفة)
- **Quality Inspection Report**
- **AQL (Acceptable Quality Level)** support
- **Integration with BOM** (فحص مواد خام للتصنيع)

### 2.13. Website (الموقع الإلكتروني)

- **Website Builder** بالسحب والإفلات (drag & drop)
- **Web Page** (صفحة ويب) مع محتوى قابل للتعديل
- **Blog Post** (مقالات)
- **Web Form** (نموذج على الموقع، v8)
- **Portal** (بوابة العميل/المورد لمشاهدة بياناتهم)
- **Website Theme** (سمات جاهزة)
- **SEO Settings** (Meta tags, sitemap, robots.txt)
- **Top Bar** و **Navbar** customization
- **Footer customization**
- **Banner / Carousel** items
- **Multilingual Website** (دعم لغات متعددة مع ترجمة محتوى)
- **Static Page** generation
- **LMS Module** (Learning Management System) — أُضيف v12
- **Customer Portal** (العميل يرى: طلبات، فواتير، إشعارات)
- **Supplier Portal** (المورد يرى: POs، GRNs، Bills)

### 2.14. eCommerce (المتجر الإلكتروني)

- **Shopping Cart** للعملاء
- **Item Display Page** (صفحة صنف كاملة)
- **Category Pages** (صفحات تصنيف)
- **Checkout Flow** (تدفق الشراء)
- **Payment Gateway Integration**: Stripe, Razorpay, Braintree, PayPal
- **Shipping Integration**: ساعي البريد المحلي
- **Order Management** (إدارة طلبات المتجر)
- **eCommerce Settings** (إعدادات: ضريبة، شحن، بوابة دفع)
- **Tax Templates** للعرض
- **Wishlist** (قائمة مفضلة)
- **Product Reviews**
- **Coupon Code / Discount** support
- **Inventory Sync** مع Stock module
- **eCommerce Analytics** (conversion rate, cart abandonment)

### 2.15. HelpDesk (الدعم الفني / Support)

- **Issue** (تذكرة دعم) — كل مشكلة من العميل
- **Issue Type** (تصنيف التذاكر)
- **Issue Priority** (أولوية)
- **SLA (Service Level Agreement)** — أُضيف v12، يربط SLA بـ Issue Type
- **SLA on custom documents** (v13)
- **Service Level** و **Service Day**
- **Support Team** و **Support Rotation**
- **Issue Assignment Rule** (توزيع تلقائي)
- **Issue Timeline** (timeline كامل للتذكرة)
- **Email-to-Issue** (تحويل الإيميل الوارد لتذكرة)
- **Customer Feedback** (v8)
- **Resolution Time** tracking
- **First Response Time** tracking
- **Knowledge Base** (مقالات مساعدة، v8)
- **Canned Responses** (ردود جاهزة)
- **Auto-Assignment** (توزيع آلي بناءً على الـ rules)
- **Support Analytics** dashboards

### 2.16. Healthcare (الرعاية الصحية)

موديول متخصص كامل من Frappe Healthcare ([github.com/frappe/healthcare](https://github.com/frappe/healthcare)):

- **Patient** master (بيانات المريض)
- **Patient Appointment** (حجز موعد)
- **Practitioner** (طبيب/ممارس)
- **Clinical Procedure** (إجراء سريري)
- **Patient Encounter** (لقاء سريري)
- **Vital Signs** (العلامات الحيوية)
- **Lab Test** و **Sample Collection**
- **Lab Test Template** (قوالب تحاليل)
- **Prescription** (وصفة طبية) و **Prescription Dosage**
- **In-patient / Out-patient** (داخلي/خارجي)
- **Inpatient Record** (سجل تنويم)
- **Inpatient Medication Order** (v13)
- **Nursing Task**
- **Medical Code Standards**: ICD-10, SNOMED-CT
- **Healthcare Service Unit** (أقسام المستشفى)
- **Medical Department**
- **Health Insurance** (تأمين صحي)
- **Patient Medical History**
- **Birth Records, Death Records**

### 2.17. Education (التعليم)

موديول متخصص لـ Schools / Universities ([github.com/frappe/education](https://github.com/frappe/education)):

- **Student** master
- **Student Applicant** (مُتقدم)
- **Program** (برنامج أكاديمي) و **Course** (مادة)
- **Student Group** (شعبة/فصل)
- **Academic Year, Term, Academic Term**
- **Student Attendance**
- **Student Log** (سجل ملاحظات)
- **Assessment Plan** (خطة تقييم)
- **Assessment Result** (نتيجة الطالب)
- **Grading Scale** (مقياس درجات)
- **Student Fee Structure** (هيكل الرسوم)
- **School Fees Management** (v9)
- **Fee Schedule, Fee Invoice, Fee Payment**
- **LMS — Learning Management System** (v12)
  - Course content
  - Quiz / Assignment
  - Video lessons
  - Progress tracking
- **Student Leave Application**
- **Transportation** (باص المدرسة)
- **Hostel** (إقامة داخلية)
- **Guardian** management
- **Bulk Attendance** (حضور جماعي)
- **Assessment Report Card** (شهادة درجات)

### 2.18. Agriculture (الزراعة)

موديول متخصص أُضيف في v10:

- **Crop** master (محصول)
- **Crop Cycle** (دورة زراعية)
- **Crop Batch** (دفعة محصول)
- **Crop Cultivation** (زراعة فعلية)
- **Crop Health** (صحة النبات)
- **Fertilizer** tracking
- **Soil Analysis** (تحليل تربة)
- **Water Source** (مصدر مياه)
- **Irrigation Schedule** (جدول ري)
- **Pest & Disease** tracking
- **Harvest** (حصاد)
- **Yield** tracking (إنتاجية)
- **Agriculture Task**
- **Plantation** type
- **Linked to Land** (أرض زراعية)
- **Weather Integration** (بيانات الطقس)
- **Agriculture Analytics & Reports**

### 2.19. Non-Profit (الجمعيات غير الربحية)

موديول أُضيف في v10، موجه لـ NGOs والجمعيات الخيرية:

- **Member** (عضو) master
- **Membership Type** (نوع العضوية)
- **Membership** (اشتراك)
- **Membership Renewal** (تجديد)
- **Donor** (متبرع)
- **Donation** (تبرع) — receipt
- **Donation Campaign**
- **Donation Type** (نوع التبرع)
- **Grant** (منحة)
- **Volunteer** (متطوع)
- **Volunteer Type** و **Volunteer Availability**
- **Chapter** (فرع محلي)
- **Beneficiary** (مستفيد)
- **Non-Profit Program** (برنامج)
- **Tax Exemption Certificate** (شهادة إعفاء ضريبي)
- **Donor Reports** (تقارير المتبرعين)
- **Integration with Accounting** (تتبع التبرعات كإيرادات مُصنّفة)

---

## 3. أفضل 18 ميزة تقنية فريدة (Unique Technical Features)

> هذه هي الميزات التي تميّز ERPNext عن باقي الـ ERPs، وتُمثّل الـ "innovation moat" الذي تبنيه Frappe. اعتمدنا في هذا التصنيف على ما ذُكر صراحةً في [docs.frappe.io](https://docs.frappe.io) و [github.com/frappe/erpnext](https://github.com/frappe/erpnext) و [frappe.io/erpnext/modules](https://frappe.io/erpnext/modules).

### 3.1. DocTypes (النماذج المُعَرَّفة كبيانات — Metadata-Driven Models)

**الوصف:** كل "جدول" في ERPNext هو في الحقيقة `DocType` (مُعرَّف في جدول `tabDocType` نفسه). الحقول (`Data Fields`) معرّفة في جدول `tabDocField`، والصلاحيات في `tabDocPerm`، والإجراءات في `tabDocType Action`، وحقول الـ child tables في `tabDocField` بنوع `Table`. **كل شيء هو data — حتى الـ data model نفسه**.

**الفائدة:**
- إنشاء نموذج جديد بالكامل من واجهة الـ Desk في 30 ثانية، بدون سطر Python واحد.
- لا حاجة لـ migrations عند إضافة DocType جديد.
- الـ Desk يُولِّد form view + list view + calendar/kanban/gallery views تلقائياً.

**مثال على إنشاء DocType:** `Settings → Customize → DocType → New → {name, fields, permissions, naming series}`.

### 3.2. Custom Fields (إضافة حقول بدون كود)

**الوصف:** إمكانية إضافة حقول جديدة لأي DocType (حتى الـ core doctypes مثل `Customer`, `Item`, `Sales Order`) من واجهة **Customize Form** بدون كتابة سطر Python. يمكن إضافة أي نوع حقل: Data, Text, Int, Float, Currency, Date, Datetime, Check, Select, Link, Dynamic Link, Table, Attach, Attach Image, Color, Geolocation, Barcode, Password, Rating, Signature, Markdown Editor, Code, Table MultiSelect, إلخ.

**الفائدة:** تكييف ERPNext لقطاع محدد (مثل: "رقم التسجيل الضريبي المحلي" لـ Sales Invoice في ليبيا) في دقيقتين، upgrade-safe — لا يتأثر عند ترقية النظام.

### 3.3. Workflow Engine (محرك سير العمل)

**الوصف:** أداة مرئية + declarative لبناء workflows متعددة الحالات لأي DocType (Draft → Pending Approval → Approved → Rejected → Cancelled). كل transition يحدد الـ Role المسموح له + الـ next state + الـ شروط الشرطية (Conditional Workflows أُضيفت في v11).

**الميزات:**
- **Conditional Workflows**: شرط Python يتحقق قبل الـ transition.
- **Workflow Action**: زر يظهر فقط في الـ state الحالي بناءً على صلاحية المستخدم.
- **Workflow Email Alert**: إيميل تلقائي عند الـ state change.
- **Workflow Document State** field على كل DocType لعرض الحالة.

**مثال:** `PO Workflow: Draft → Pending → Approved by Finance → Approved by CEO → Sent to Vendor → Received → Closed`.

### 3.4. Print Formatter — قوالب طباعة ديناميكية (Jinja + HTML + CSS)

**الوصف:** كل DocType له `Print Format` افتراضي. يمكن إنشاء Print Format مخصص بـ:
- **Print Format Builder** (Drag & Drop — أُعيد تصميمه في v14)
- **Custom HTML + CSS** مع **Jinja templating** للوصول لـ `{{ doc.customer_name }}`, `{{ doc.get_formatted('grand_total') }}`, loops, conditions, macros.

**الفائدة:** فواتير، عروض أسعار، إيصالات، شهادات — كلها قابلة للتخصيص البصري الكامل بدون كود، مع منطق ديناميكي (تكرار بنود، حسابات، تنسيق تاريخ حسب اللغة).

**مثال:**
```jinja
<h1>{{ doc.name }}</h1>
<table>
  {% for item in doc.items %}
    <tr><td>{{ item.item_name }}</td><td>{{ item.qty }}</td><td>{{ item.rate }}</td></tr>
  {% endfor %}
  <tr><td colspan="2">Total</td><td>{{ doc.grand_total }}</td></tr>
</table>
```

### 3.5. Scheduled Job System + Background Jobs (RQ + Frappe Scheduler)

**الوصف:** نظام مزدوج:
1. **`frappe.enqueue(method, queue='short')`** — لإرسال دوال Python للـ background workers (3 queues افتراضية: `short` (5min), `default` (5min), `long` (25min)، قابلة للتخصيص في `common_site_config.json`).
2. **`scheduler_events` hook** في `hooks.py` — لتشغيل tasks دورية (hourly, daily, weekly, monthly, cron، إلخ).

**الفائدة:** فصل العمليات الطويلة عن request-response (لا يعلق الـ user)، يدعم ملايين الـ background jobs، مع RQ (Redis Queue) كـ queue manager. الـ scheduler في production يُدار عبر supervisor + systemd، ويُمكَّن بـ `bench --site <site> enable-scheduler`.

**الـ tech stack:** Redis (queue + cache + pub-sub) + RQ workers + Frappe Scheduler (long-running while loop).

### 3.6. Realtime Updates عبر WebSocket (Socket.IO + Redis Pub-Sub)

**الوصف:** خادم Node.js مُنفصل يستقبل events من Python عبر Redis pub-sub، ويبثّها للمتصفحات المتصلة عبر Socket.IO. الـ API على العميل:
- `frappe.realtime.on('event_name', callback)` للاشتراك
- `frappe.realtime.emit('event_name', data)` لإرسال
- `frappe.publish_realtime('event', data)` من Python
- `frappe.publish_progress(50, title='...')` لشريط تقدم

**الـ Rooms المتاحة:**
- `all` — كل المستخدمين
- `user:{username}` — مستخدم محدد
- `doctype:{doctype}` — مشترك تلقائي عند فتح list view
- `doc:{doctype}/{name}` — مشترك تلقائي عند فتح form

**الفائدة:** إشعارات لحظية، chat، تحديثات لوحة تحكم بدون refresh، تعاون multi-user، تعقب تغييرات الـ stock. multi-tenant via namespace (`/{sitename}`).

### 3.7. Role-Based Permissions على مستوى الـ Field

**الوصف:** ثلاث طبقات صلاحيات:
1. **DocType-level**: أي Role يمكنه read/write/create/delete/submit/cancel/submitted/amend لأي DocType.
2. **User-level**: `User Permission` لتقييد مستخدم برؤية مستندات شركات/مناطق/فروع محددة فقط.
3. **Field-level**: عبر `Perm Level` على كل field — مثلاً حقل `salary` مرئي فقط لـ Role "HR Manager" (perm_level=1) ومخفي عن "Employee" (perm_level=0).

**الفائدة:** تحكم دقيق بالصلاحيات يُشبه Odoo Record Rules. مثال: "موظف المبيعات يرى فقط عملاء منطقته".

### 3.8. Multi-Company + Multi-Currency + Consolidated Financial Statements

**الوصف:**
- **Multi-Company**: في `ERPNext`، Company جدول مستقل، كل document يحدد `company`. يمكن للشركة الواحدة أن تكون هي نفسها "Group" (مجموعة) وتحتوي شركات فرعية. قوائم مالية مُوَحَّدة (Consolidated Balance Sheet, P&L, Cash Flow) تُولَّد تلقائياً.
- **Multi-Currency**: كل transaction له `accounting_currency` و `transaction_currency` + `exchange_rate` و `gain/loss account` للفروقات. أسعار صرف يومية مربوطة بـ `Currency Exchange` table.
- **Auto Currency Exchange Revaluation** (v15): يُعيد تقييم الذمم المدينة/الدائنة آخر الشهر بآخر سعر صرف.
- **Inter-company Journal Entry** (v11): قيود بين شركات المجموعة بدون ضريبة.
- **Finance Book** (v11): دفاتر محاسبية متعددة (مثلاً: IFRS + محلي).

**الفائدة:** أم ideal للمجموعات والشركات متعددة الجنسيات.

### 3.9. Audit Trail كامل (Version + Submittable DocTypes + Document Versioning)

**الوصف:** نظام مراجعة تاريخية متعدد الطبقات:
1. **`Version` DocType** يحتفظ بنسخة من كل تعديل على أي DocType (يفعَّل على مستوى DocType).
2. **Submittable DocTypes**: docs قابلة للـ submit (لا يُعدَّل إلا بإلغاء وإصدار amended version).
3. **Document Versioning** (v8): مع `Cancel and Amend` — عند الإلغاء، يُنشأ doc جديد بـ version مُرقَّم.
4. **Audit Trail Tool** (v13): يقارن حتى **5 نسخ amended** معروضة side-by-side (Fields Changed, Rows Updated, Rows Added/Removed).

**الفائدة:** تتبّع تاريخي كامل لكل transaction، mandatory للـ SOX compliance.

### 3.10. Data Import Tool (مستورد البيانات)

**الوصف:** أداة `Data Import` في Desk تستقبل CSV/Excel وتُحدّد:
- الـ DocType الهدف
- mapping الأعمدة → الحقول
- معاينة قبل الاستيراد
- تنبيه للأخطاء (validation مسبق)

**الفائدة:** نقل بيانات من ERP قديم (مثلاً: من Excel أو SAP) في دقائق. يدعم أيضاً **Data Export** لنفس الـ DocTypes، و**Update Items via Data Import** لتعديل بالجملة.

### 3.11. Report Builder — Query Reports + Script Reports + Chart Builder

**الوصف:** ثلاث أنواع تقارير:
1. **Report Builder (no-code)**: في `Setup → Report Builder` — اختر DocType + fields + filters + grouping + chart type → يُنشئ report مخصص.
2. **Query Report (SQL)**: ملف `.sql` + ملف `.js` للأعمدة، يعرض report معقد.
3. **Script Report (Python)**: ملف `.py` يستدعي ORM ويُرجع أعمدة.
4. **Chart Builder** (v12): drag & drop لرسوم بيانية من أي report.
5. **Dashboards Bootstrapped** (v13): لكل موديول dashboard جاهز بـ KPIs و charts.

**الفائدة:** أي مستخدم (أو مطور) يبني تقريره بنفسه بدون الاعتماد على فريق BI.

### 3.12. Webhooks (تكامل HTTP Out)

**الوصف:** يُعرَّف Webhook على DocType محدد + event (after_insert, on_update, on_submit, on_cancel, on_trash) + شرط (`condition: doc.grand_total > 10000`) + URL الهدف + method (POST/PUT) + headers (لـ authentication). عند تحقق الشرط، يُرسَل POST تلقائياً بـ JSON payload.

**الـ authentication:** Bearer token, Basic Auth، أو API key في الـ header.

**الفائدة:** ربط ERPNext مع أي نظام خارجي (payment gateway, e-commerce, WMS, IoT) بدون كتابة glue code.

### 3.13. REST API (Auto-generated per DocType)

**الوصف:** لكل DocType، يولّد Frappe تلقائياً REST API:
- `GET /api/resource/{doctype}` — list مع pagination, filters, sort, fields
- `GET /api/resource/{doctype}/{name}` — record واحد
- `POST /api/resource/{doctype}` — create
- `PUT /api/resource/{doctype}/{name}` — update
- `DELETE /api/resource/{doctype}/{name}` — delete

**Filter syntax:** `?filters=[["status","=","Open"],["grand_total",">",1000]]`
**Pagination:** `?limit_start=0&limit_page_length=20`
**Expand child docs:** `?expand=["items"]`

**بالإضافة:** Remote method calls عبر `/api/method/<dotted.module.path>` لأي Python function تم وضع decorator `@frappe.whitelist()` عليها.

**3 طرق authentication:**
1. **Token**: `Authorization: token api_key:api_secret`
2. **Password** (cookie-based session): `POST /api/method/login`
3. **OAuth2 Bearer access_token**

### 3.14. Frappe Client (Python Library + JS Client)

**الوصف:** [`frappe/frappe-client`](https://github.com/frappe/frappe-client) — مكتبة Python بسيطة:
```python
from frappeclient import FrappeClient
client = FrappeClient("https://example.com", "api_key", "api_secret")
client.insert_one({"doctype": "ToDo", "description": "Hello"})
client.get_list("ToDo", fields=["name", "status"], filters=[["status","=","Open"]])
```

**في JS** الـ frontend (Frappe Desk نفسه) يستخدم `frappe.call()`، `frappe.db.get_list()`, `frappe.db.get_doc()`.

**الفائدة:** تكامل ERPNext مع أي نظام Python/JS في 5 أسطر.

### 3.15. Custom Apps (Modular Architecture)

**الوصف:** Frappe يستخدم بنية `App` — كل app هو Python package مستقل في `bench/apps/`. الـ core هو `frappe` + `erpnext`، والباقي apps اختيارية: `hrms`, `crm`, `helpdesk`, `insights`, `wiki`, `drive`, `gameplan`, `lms`, `healthcare`, `education`, `agriculture`, `non_profit`, `payments`. كل app يضيف DocTypes خاصة به. يمكنك إنشاء custom app عبر `bench new-app myapp` ويظهر في `hooks.py`.

**الفائدة:** upgrade-safe — تحديثات core لا تكسر customizations. الـ Marketplace ([frappecloud.com/marketplace](https://frappecloud.com/marketplace)) يضم تطبيقات جاهزة.

### 3.16. Server Scripts + Client Scripts (Python + JS)

**الوصف:** في `System Settings → Server Scripts`، يمكن لمستخدم بصلاحية `System Manager` كتابة Python code يعمل في hook events (`doc_events.before_insert`, `doc_events.on_submit`, `api_method`, إلخ) بدون نشر ملف. كذلك `Client Scripts` بـ JavaScript للـ frontend validation/UI logic. **كلاهما upgrade-safe**.

**أمان:** يُنفَّذ في RestrictedPython sandbox (مع تحذير من RCE إن لم يُضبَط — CVE-2023 منشور).

### 3.17. Naming Series (توليد أرقام تلقائية قابلة للتخصيص)

**الوصف:** كل DocType له `naming_series` pattern (مثلاً `SINV-.YYYY.-.####`). يدعم prefixes حسب السنة، الـ Company، الـ Branch. إعادة ترقيم، prefixes مخصصة، custom Python function لتوليد الـ name.

**الفائدة:** أرقام فواتير منظمة (SINV-2026-0001) قابلة للقراءة، لا UUIDs مُتعقّدة.

### 3.18. PWA Mobile App (إصدار Mobile/Tablet)

**الوصف:** منذ v15، يوجد **PWA Mobile App** رسمي لموديول HR (وأكثر) يتيح للموظفين:
- CheckIn/CheckOut مع GPS
- عرض الـ Salary Slip
- تقديم Leave Application
- Expense Claim
- يعمل offline ويُمزَامَن عند العودة للـ online

**الفائدة:** تجربة mobile-first لمن يحتاج ERP في الميدان (HR، Inventory، Manufacturing floor، Sales van).

---

## 4. الـ Architecture وهيكل قاعدة البيانات

### 4.1. نظرة معمارية (3-Tier + Service-Oriented)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    Client (Browser / PWA)                            │
│   - Vanilla JS + Vue + Frappe Desk UI                                │
│   - Socket.IO client for realtime                                    │
│   - frappe.call() JS API                                             │
└─────────────────────────────────────────────────────────────────────┘
                              ↑ HTTPS / WSS
                              │
┌──────────────────────┬──────┴──────────┬────────────────────────────┐
│  Nginx (Reverse     │  Python Web     │  Node.js Socket.IO          │
│  Proxy, port 80/443)│  Server (Gunicorn│  Server (port 9000)         │
│  Static files       │  / Waitress)    │  Realtime bridge            │
│  SSL termination    │  REST API       │  Redis pub-sub subscriber    │
└──────────────────────┴──────┬──────────┴────────────────────────────┘
                              │
┌──────────────────────┬──────┴──────────┬────────────────────────────┐
│  Redis               │  Frappe        │  Background Workers (RQ)    │
│  - Cache             │  Scheduler     │  - short, default, long     │
│  - Queue (RQ)        │  - hooks.py    │  - Process tasks            │
│  - Pub-Sub (Realtime)│  - cron-like   │  - Email queue              │
└──────────────────────┴────────────────┴────────────────────────────┘
                              │
                  ┌───────────┴───────────┐
                  │  MariaDB 10.3+        │  PostgreSQL 12+ (since v12)
                  │  (or PostgreSQL)      │  - tabDocType, tabDocField,
                  │  - كل الـ DocTypes    │  - tabDocPerm, tabVersion,
                  │  - الإصدار الافتراضي  │  - tabUser, tabRole,
                  │                       │  - tabSingles, tabSeries
                  └───────────────────────┘
```

### 4.2. المكوّنات التقنية بالتفصيل

| المكوّن | التقنية | الإصدار | الدور |
|---------|---------|---------|------|
| **Backend Language** | Python | 3.10+ | كل منطق الأعمال، REST API، Server Scripts |
| **Backend Framework** | Frappe Framework | v15.x (latest) | ORM, Permissions, Hooks, WebSockets |
| **Database (افتراضي)** | MariaDB | 10.3+ | كل الـ transactional data + metadata |
| **Database (بديل)** | PostgreSQL | 12+ | مدعوم رسمياً منذ v12، بنفس الـ features |
| **Cache + Queue + Pub-Sub** | Redis | 6+ | Job queue (RQ)، Cache، Socket.IO bridge |
| **Realtime Server** | Node.js + Socket.IO | Node 18+ | بث events للمتصفحات |
| **Web Server** | Gunicorn (أو Waitress على Windows) | — | WSGI server |
| **Reverse Proxy** | Nginx | — | SSL، static files، port 80/443 |
| **Deployment CLI** | bench (Frappe) | v5+ | install, migrate, update, restart |
| **Process Manager** | Supervisor / systemd | — | إدارة العمليات في production |
| **Frontend** | Vanilla JS + Vue.js 2 | — | Frappe Desk UI (لا React/Angular) |
| **Mobile** | PWA (Progressive Web App) | — | يعمل على iOS + Android بدون app store |

### 4.3. مخطط البيانات (Database Schema)

- **كل DocType = جدول MariaDB** اسمه `tab<DocType>` (مثلاً: `tabSales Invoice`, `tabCustomer`).
- **`tabDocType`** يحوي تعريف كل الـ DocTypes (الاسم، الـ module، الـ naming rule).
- **`tabDocField`** يحوي تعريف كل حقل (label, fieldtype, options, reqd, permlevel).
- **`tabDocPerm`** يحوي صلاحيات كل DocType per role.
- **`tabSingles`** يحوي الـ Single DocTypes (مثل `System Settings`, `Company`).
- **`tabSeries`** يحوي آخر رقم في كل naming series.
- **`tabVersion`** يحوي snapshot لكل تعديل (للـ Audit Trail).
- **`tabUser`, `tabRole`, `tabUserRole`, `tabUserPermission`** للـ users والصلاحيات.
- **`tabCommunication`** يحوي كل إيميل/مكالمة مرتبطة بأي DocType (الإشعارات + Email Inbox).
- **`tabFile`** يحوي كل ملف مرفوع (attach + file_url).
- **`tabComment`** يحوي comments على أي DocType.
- **`tabActivity Log`** يحوي الـ audit log.

**لا يوجد ORM ثقيل:** Frappe يستخدم `frappe.db.sql()` + DML helpers (`frappe.get_doc()`, `doc.insert()`, `doc.save()`, `frappe.db.get_list()`) — قريب من Dapper-style في .NET.

### 4.4. Multi-Tenancy

- **كل tenant = `site` folder** تحت `frappe-bench/sites/<site_name>/`.
- الـ `site_config.json` يحوي الـ database credentials.
- **Single codebase** يخدم جميع الـ sites (multi-tenant via sites folder + namespace in socket.io).
- `bench new-site <name>` ينشئ site جديد.
- `bench set-nginx-host` يولّد nginx config.

### 4.5. Document Versioning & Concurrency

- **Optimistic Locking**: `modified` timestamp + `docstatus` + `name`.
- **Conflict detection**: عند save، يقارن الـ `modified` — إن تغيّر منذ الـ load، يرفع `VersionMismatchError`.
- **No row-level locking by default** — heavy transactional workflows تحتاج explicit `frappe.db.sql("SELECT ... FOR UPDATE")`.

### 4.6. High Availability (ملخص)

- فصل الـ tiers على servers مستقلة (Database, Web, Workers, Realtime).
- **MariaDB replication** (master + replicas + read scaling).
- **Redis Sentinel / Cluster** للـ HA على الـ cache + queue.
- **Load balancer** (HAProxy) أمام web servers.
- **Backups** يومية + S3-compatible storage (`bench backup --with-files`).

---

## 5. قدرات الـ API (REST, Webhooks, Frappe Client)

### 5.1. REST API — Auto-Generated per DocType

**Authentication (3 طرق):**

```http
# 1. Token (مُوصى به للـ integrations)
GET /api/method/frappe.auth.get_logged_user
Authorization: token <api_key>:<api_secret>

# 2. Password (cookie-based — للـ web)
POST /api/method/login
Content-Type: application/json
{"usr": "user@example.com", "pwd": "password"}

# 3. OAuth2 Bearer
GET /api/method/frappe.auth.get_logged_user
Authorization: Bearer <access_token>
```

**CRUD Endpoints (auto-generated لكل DocType):**

```http
# List
GET /api/resource/Customer?fields=["name","customer_name"]&filters=[["territory","=","Riyadh"]]&limit_start=0&limit_page_length=20&order_by=creation%20desc
→ {"data": [...]}

# Get one
GET /api/resource/Sales Invoice/SINV-2026-0001?expand=["items"]
→ {"data": {...}}

# Create
POST /api/resource/Sales Invoice
Content-Type: application/json
{"customer": "Cust-001", "items": [{"item_code": "ITM-01", "qty": 5}]}
→ 201 Created

# Update
PUT /api/resource/Sales Invoice/SINV-2026-0001
Content-Type: application/json
{"notes": "Updated notes"}
→ 200 OK

# Delete
DELETE /api/resource/Sales Invoice/SINV-2026-0001
→ {"message": "ok"}
```

**Remote Method Calls:**

```python
# server.py
import frappe

@frappe.whitelist()
def my_custom_method(customer_id, include_invoices=False):
    # ... business logic
    return {"status": "ok", "data": [...]}
```

```http
GET /api/method/myapp.server.my_custom_method?customer_id=Cust-001
→ {"message": {"status": "ok", "data": [...]}}
```

**File Upload:**

```http
POST /api/method/upload_file
Authorization: token <key>:<secret>
Content-Type: multipart/form-data
file=@/path/to/invoice.pdf
→ {"message": {"file_url": "/files/invoice.pdf", "file_name": "invoice.pdf"}}
```

### 5.2. Webhooks (HTTP Out)

**التعريف من الـ Desk:**

- **DocType**: `Sales Invoice`
- **Event**: `on_submit` (بعد التأكيد)
- **Condition**: `doc.grand_total > 10000`
- **URL**: `https://external-system.com/webhook/erpnext`
- **Method**: `POST`
- **Headers**: `Authorization: Bearer <secret>`

**Trigger:** عند تحقق الشرط، يُرسَل HTTP request بـ JSON payload (الـ doc كامل).

**Authentication options:**
- `Authorization: Bearer <token>`
- `Authorization: Basic <base64(user:pass)>`
- Custom headers (API key)

**Use cases:** إرسال فاتورة لـ payment gateway، إخطار WMS، تحديث CRM خارجي، إرسال Slack notification.

### 5.3. Frappe Client (Python Library)

```python
# install: pip install frappe-client
from frappeclient import FrappeClient

client = FrappeClient(
    url="https://example.erpnext.com",
    api_key="abcd1234",
    api_secret="efgh5678"
)

# CRUD
client.insert_one({"doctype": "ToDo", "description": "Buy milk"})
doc = client.get_doc("ToDo", "T-00001")
client.update(doc)
client.delete("ToDo", "T-00001")

# List with filters
todos = client.get_list(
    "ToDo",
    fields=["name", "description", "status"],
    filters=[["status", "=", "Open"]],
    limit_page_length=50
)

# Custom method
result = client.get_api("myapp.server.custom_method", params={"key": "value"})
```

### 5.4. Frappe Desk JS API (في الـ Frontend)

```javascript
// frappe.call() — generic API call
frappe.call({
    method: "frappe.client.get",
    args: { doctype: "Customer", name: "Cust-001" },
    callback: (r) => console.log(r.message)
});

// frappe.db — convenience helpers
frappe.db.get_list("Customer", { filters: { territory: "Riyadh" } })
        .then(docs => console.log(docs));
frappe.db.get_doc("Sales Invoice", "SINV-2026-0001")
        .then(doc => console.log(doc));
frappe.db.insert({ doctype: "ToDo", description: "..." });
frappe.db.set_value("Customer", "Cust-001", "customer_name", "New Name");

// frappe.form — form-level events
frappe.ui.form.on("Sales Invoice", {
    refresh(frm) { /* runs on form load */ },
    customer(frm) { /* on field change */ },
    before_save(frm) { /* validation */ }
});
```

### 5.5. Limits and Rate Limiting

- **Default rate limit**: 60 requests/second (per user) — قابل للتعديل.
- **No API key rotation mechanism** — يجب تغيير الـ secret يدوياً.
- **Large bulk operations**: استخدم `frappe.enqueue()` بدل sync API call.
- **Long-running**: استخدم `frappe.flags.in_patch` أو `frappe.flags.ignore_permissions` في الـ context الملائم.

---

## 6. روابط المصادر

### Sources الرسمية (Primary)

1. [ERPNext — Wikipedia (en)](https://en.wikipedia.org/wiki/ERPNext) — نظرة عامة، تاريخ، موديولات، ترخيص، سجل إصدارات
2. [ERPNext — Frappe.io](https://frappe.io/erpnext) — الموقع الرسمي + Modules overview
3. [ERPNext Modules Page](https://frappe.io/erpnext/modules) — قائمة الموديولات الكاملة والـ Industries
4. [ERPNext on GitHub (frappe/erpnext)](https://github.com/frappe/erpnext) — الـ source code
5. [Frappe Framework — Introduction](https://docs.frappe.io/framework/user/en/introduction) — الإطار التقني
6. [Frappe REST API Documentation](https://docs.frappe.io/framework/user/en/api/rest) — توثيق REST API الكامل
7. [Frappe Background Jobs](https://docs.frappe.io/framework/user/en/api/background_jobs) — enqueue + scheduler events
8. [Frappe Realtime (Socket.IO)](https://docs.frappe.io/framework/user/en/api/realtime) — توثيق الـ realtime
9. [Frappe Audit Trail](https://docs.frappe.io/framework/user/en/audit-trail) — Versioning + Audit Trail tool
10. [Frappe Webhooks](https://docs.frappe.io/framework/user/en/guides/integration/webhooks) — توثيق Webhooks
11. [Frappe Client (Python library)](https://github.com/frappe/frappe-client) — Python wrapper للـ API
12. [Frappe Healthcare](https://github.com/frappe/healthcare) — موديول Healthcare مفتوح المصدر
13. [Frappe Education](https://github.com/frappe/education) — موديول Education مفتوح المصدر
14. [Print Format Documentation](https://docs.frappe.io/erpnext/print-format) — Print Format Builder + Jinja
15. [Multi-Currency Accounting](https://docs.frappe.io/erpnext/multi-currency-accounting) — Multi-currency setup
16. [Hooks Documentation](https://docs.frappe.io/framework/user/en/python-api/hooks) — hooks.py + scheduler events

### Secondary Sources (مراجعات ومقارنات)

17. [ERPNext Deep Dive 2026 — DevDiligent](https://devdiligent.com/blog/erpnext-deep-dive/) — تحليل شامل، modules، architecture، pricing
18. [Versioning and Audit Trail — Frappe Blog](https://frappe.io/blog/erpnext-features/versioning-and-audit-trail) — تطور نظام الـ Audit Trail
19. [Multi Company Management — GreyCube](https://greycube.in/blog/general/streamlining-business-operations-managing-multi-companies-with-erpnext) — Multi-company
20. [Scaling ERPNext (PDF)](https://erpnext.com/files/Scaling%20ERPNext.pdf) — توثيق High Availability
21. [Customizing ERPNext No-Code — Solufy](https://www.solufyerp.com/erp-blog/erpnext-no-code-customization/) — Custom Fields + DocTypes
22. [How to Build Custom Fields, Reports, and Workflows — Nexeves](https://nexeves.com/blog/ERPNext/how-to-build-custom-fields-reports-and-workflows-in-erpnext) — Customization
23. [Jinja and ERPNext Print Formats — Alain Berger](https://alainber.medium.com/jinja-and-erpnext-print-formats-demystified-9ab548cd6fd2) — Print Format details
24. [Frappe MariaDB Integration (CSDN)](https://blog.csdn.net/gitblog_00346/article/details/150959139) — Database architecture (مصدر تقني)
25. [Frappe High Availability Reference Architecture](https://discuss.frappe.io/t/erpnext-high-availability-reference-architecture/73755) — HA discussion
26. [ERPNext V8 Wiki — Custom DocPerms + Kanban + Workflows](https://github.com/frappe/erpnext/wiki/ERPNext-Version-8) — تاريخص الـ features
27. [Frappe 101 — Introduction to Frappe Framework — RedGate](https://www.red-gate.com/simple-talk/development/web/an-introduction-to-frappe-framework-features-and-benefits/)
28. [CRUNCHBASE — Frappe Technologies Profile](https://www.crunchbase.com/organization/erpnext) — معلومات الشركة
29. [Custom Field Documentation](https://docs.frappe.io/erpnext/custom-field) — Custom Field
30. [Websoft9 ERPNext Guide](https://support.websoft9.com/docs/next/erpnext) — Installation + features

---

## 7. القيود والتحديات (للمقارنة)

> من الإنصاف أن نذكر ما لا يُجيده ERPNext — مفيد لـ gap-analysis.

1. **UI/UX أقل بريقاً من Odoo**: Frappe Desk عملي لكن ليس "polished" مثل Odoo 17. الـ styling أقل عصرية، لكن functional 100%.
2. **JS API documentation ضعيف**: توثيق Frappe Python ممتاز، لكن توثيق الـ JavaScript client (frappe.ui, frappe.form) أقل تغطية.
3. **Limited Marketplace**: ~300+ apps في Frappe Cloud Marketplace، لكن أقل بكثير من Odoo (~40,000+ modules).
4. **Customization تحتاج developer**: لا يُناسب non-technical admins. أدوات low-code (Custom Fields, Print Format) ممتازة، لكن الـ workflow المتقدم + custom scripts تحتاج Python/JS.
5. **Scaling complexity**: self-hosted في high-traffic يحتاج DBA + DevOps. Frappe Cloud يُلغي هذا، لكن بسعر.
6. **No advanced marketing automation** (مثل Odoo Email Marketing) — newsletter موجود لكن basic.
7. **No native mobile app** (PWA فقط) — Odoo عنده iOS/Android native apps.
8. **Single-language website per site**: multilingual تحتاج setup إضافي.
9. **Healthcare module** (frappe/healthcare) ليس certified بأي معيار دولي (HIPAA-ready لكن not certified).
10. **No built-in BI/Analytics tool** متقدم — Insights متاح لكنه basic. Grafana + Metabase integration مطلوبة.
11. **No native e-commerce frontend** قوي مثل Shopify — الـ eCommerce module في Frappe محدود لـ SMBs.
12. **Education module** مُلائم لـ K-12 لكن less mature للـ Higher Ed (لا LMS مُتكامل قابل للمنافسة مع Moodle).
13. **Historical financial reports** (Multi-Period Comparison, Year-over-Year) في P&L يحتاج custom report.
14. **Project Profitability** يعمل لكن يحتاج setup دقيق للـ billing rate و cost rate per employee.

---

## 8. التسعير ونماذج النشر

### 8.1. الترخيص

- **الـ software**: **GPL-3.0-only** — حر ومفتوح المصدر 100%. لا رسوم ترخيص.
- **التوزيع**: أي تعديل يجب أن يبقى GPL-3.0.
- **الاستخدام التجاري**: مسموح بالكامل (SaaS، on-premise، للغير).
- **Frappe Cloud Source Code**: غير مفتوح (proprietary parts للـ Cloud infrastructure).

### 8.2. خيارات النشر

| الخيار | التكلفة الشهرية التقريبية | الـ Stack | التحكم | المميزات |
|--------|---------------------------|---------|--------|----------|
| **Self-hosted Community** | تكلفة الـ server فقط (~$10-50/mo على VPS، $100+/mo على bare-metal) | Linux + MariaDB + Redis + Node + Frappe | كامل | مجاني تماماً، تخصيص كامل، ملكية البيانات |
| **Frappe Cloud (Shared)** | يبدأ من **$5/mo** لـ 1 site + 1 user | Frappe managed | متوسط | سهل الإعداد، auto-scaling، backups، SSL، email |
| **Frappe Cloud (Dedicated)** | **$100-400+/mo** | Dedicated resources | مرتفع | Dedicated CPU/RAM، SLAs، priority support |
| **Hybrid** | Self-hosted + Frappe Cloud for backup/monitoring | كلاهما | كامل | DR + Cloud backup |
| **Enterprise Services** | على عرض (custom contract) | أي deployment | كامل | SLA، dedicated CSM، product warranty، training |

### 8.3. تكاليف خفية (Total Cost of Ownership)

- **Implementation Cost**: $5,000-50,000+ حسب الـ scope (Setup + Customization + Training + Data Migration).
- **Annual Maintenance**: 15-20% من implementation cost/سنة.
- **Training**: $500-2,000/user لـ Frappe School certification.
- **Custom Apps Development**: $50-200/hour من certified Frappe partner.
- **Hosting** (إن لم يكن self-hosted): $5-400+/mo.
- **Third-party connectors**: $0-100/mo لكل integration.

### 8.4. Pricing Page

- **Frappe Cloud pricing**: <https://frappecloud.com/erpnext/pricing>
- **Enterprise Sales**: <https://frappe.io/erpnext/enterprise>
- **14-day free trial**: <https://frappecloud.com/erpnext/signup>

---

## 9. سجل الإصدارات الرئيسي (للسابقة التاريخية)

> من Wikipedia + GitHub releases — مفيد لفهم تطور الـ features.

| الإصدار | التاريخ | التغييرات الجوهرية |
|---------|---------|-------------------|
| **1.0** | يونيو 2010 | أول إصدار، نُشر على Google Code. |
| **2.0** | يوليو 2012 | توسع الموديولات. |
| **3.0** | أبريل 2013 | — |
| **4.0** | فبراير 2014 | أُدخلت **App architecture** في Frappe. |
| **5.0** | 19 مايو 2015 | Item Variants, **Print Format Builder**, Sharing, Starring, Document Timelines, **Multi-Currency**, Party model. |
| **6.0** | 2 سبتمبر 2015 | ERPNext Schools, Calendar View, DocType exports. |
| **7.0** | 22 يوليو 2016 | **Online/Offline POS**, Asset Depreciation, **Payment Entry**, **Timesheets**, Dashboards, Editable grid, Quick Entry view. |
| **8.0** | 30 مارس 2017 | Global Search, **Kanban View**, **Document Versioning**, Delete and Restore, **Email Inbox**, Employee Loan, Enhanced POS, Multiple UOMs, **Accrual Payroll**, **Custom Permissions**, Customer Feedback, School Assessment. |
| **9.0** | 26 سبتمبر 2017 | **Healthcare Domain**, Subscription, School Fees, New Setup Wizard. |
| **10.0** | 29 ديسمبر 2017 | **Agriculture Domain**, **Non-profit Domain**, Data Import upgrades, Employee Advance, Item Variants. |
| **11.0** | 10 ديسمبر 2018 | **Multi-company consolidated FS**, Payroll per Tax Declaration, **Employee Onboarding/Offboarding**, **Finance Book**, CWIP Accounting, Staffing Plan, Inter-company JE, Exchange Rate Revaluation, Leave Policy, **Conditional Workflows**, Serialised Assets, Tax Withholding, Shift Plan, Budgeting in Material Request. |
| **12.0** | 22 يوليو 2019 | **Graphical Dashboard**, **Custom Report with Chart Builder**, **Postgres Support**, Multi-select Field, Enhanced Website, Improved Pricing Rule, **Accounting Dimensions**, Invoice Discounting, BOM Explorer, Auto Attendance, Leave Ledger, Promotional Scheme, **SLA**, Email Campaign, **LMS**, **Quality Management System**, Production Planning, **Project Template**, New Desktop, Keyboard Navigation, **Assignment Rule**, Exotel Call, Milestones, Auto Repeat, Document Follow, **Energy Points**, Google Contacts, PDF Encryption, **Raw Printing**, Web Form Refactor. |
| **13.0** | 2020 | Custom Desk, SLA on custom documents, **Bootstrapped Dashboards**, In-patient module (Healthcare), Module Onboarding, **Event streaming**, **Audit Trail tool**, POS Invoicing, Production Forecast, Social Media post, India PF/PT, Conditional Mandatory, BOM/JV template, India GST reports. |
| **14.0** | 1 أغسطس 2022 | **Customizable Workspaces**, **New Print Format Builder**, **Subcontracting flow**, **Organisational Chart**, Tab View, **Warehouse Management & Inventory Dimensions**, Scrap management, **Payment Ledger**, **KSA and Tanzania accounting**, Asset Grouping and Splitting, **Asset Capitalisation**, Bulk Transaction Processing. |
| **15.0** | 10 سبتمبر 2023 | **Multi-level BOM creator**, **Auto Currency Exchange Revaluation**, **POS stock update in real-time**, Financial Ratios report, Accounting Dimension Balancing, **Asset Activity Tracking**, **Print Format Designer**, Advance Payment in separate Liability, **PWA Mobile app for HR**, **Stock reservation against Sales Order**, **Frappe Builder**. |
| **16.0** | 10 ديسمبر 2025 | Iconic dashboard, Production tracking on Job card level, Subcontracting inward flow, better landed cost valuation, accounting for purchase items in P&L, much better performance. |

---

## ملخص تنفيذي (Executive Summary)

**ERPNext** هو **ERP متكامل، مفتوح المصدر بالكامل (GPL-3.0)، مبني على Frappe Framework**، يغطي 19+ موديول أساسي ومتخصص (Accounting, Sales, Purchase, Stock, Manufacturing, HR, Payroll, Projects, CRM, Assets, POS, Quality, HelpDesk, Website, eCommerce, Healthcare, Education, Agriculture, Non-Profit). قوّته الجوهرية تكمن في **18+ ميزة تقنية فريدة** أبرزها:

- **DocTypes + Custom Fields** (نماذج مُعرَّفة كبيانات، تخصيص بدون كود)
- **Workflow Engine** متعدد الحالات
- **Print Format Builder** مع Jinja templating
- **Scheduled Job System** + **Background Jobs** عبر Redis + RQ
- **Realtime Updates** عبر Socket.IO + Redis pub-sub
- **Role-Based Permissions** بثلاث طبقات (DocType + User + Field)
- **Multi-company + Multi-currency** مع قوائم مالية مُوَحَّدة
- **Audit Trail** كامل (Versioning + Submittable + Audit Tool)
- **Data Import Tool** بـ CSV/Excel
- **Report Builder** (Query + Script + Chart + Dashboard)
- **Webhooks** + **Auto-generated REST API** + **Frappe Client** (Python)

**الـ Architecture** ثلاثي الطبقات: Python (Gunicorn) + Node.js (Socket.IO) + MariaDB/PostgreSQL + Redis، مع multi-tenancy عبر sites folder. يدعم Self-hosted مجاني بالكامل أو Frappe Cloud بأسعار تبدأ من $5/شهر.

**للمقارنة مع ERP-SYSTEM (نظامنا):** نرى في ERPNext مرجعاً ممتازاً لـ:
- **Metadata-driven data modeling** (DocTypes) — يمكن تطبيق مفهوم مماثل عبر EF Core conventions + JSON metadata.
- **Workflow Engine declarative** — استبدل الـ hardcoded transitions بـ workflow definitions في DB.
- **Print Format Builder** (Jinja-style) — استبدل الـ PDF generators الجامدة بـ templating engine.
- **Scheduled Jobs + Background processing** — استخدم Hangfire (موجود في .NET ecosystem) مع نفس النمط.
- **Realtime Updates** — SignalR في .NET يعطي نفس النتيجة.
- **REST API auto-generation per resource** — استخدم ASP.NET OData أو Minimal APIs reflection.
- **Field-level permissions** — استبدل الـ role-based السميك بـ attribute-based + per-field guards.

**المجالات التي يتفوق فيها ERP-SYSTEM على ERPNext (لفجوة التنفيذ):**
- **No-code customization per Arabic-speaking SMBs** — لا يدعم ERPNext dialectic Arabic accounting libyan-style (حقول: بنود ضريبة محلية، صيغ فواتير محددة لـ LYD، إلخ) بدون custom work.
- **RTL + Arabic-first UI** — Frappe يدعم RTL لكن الـ templates تحتاج تخصيص لكل موديول.
- **MENA-specific compliance**: ZATCA (السعودية), ETA (مصر), LPA (ليبيا) — ليست built-in.
- **Lightweight deployment** — ERPNext يحتاج Linux + MariaDB + Redis + Node (~2GB RAM minimum). ERP-SYSTEM بـ PostgreSQL فقط أبسط.
- **SMB-friendly UX** — Frappe Desk كثيف (50+ doctype في sidebar)، يحتاج custom walkthroughs.

---

**نهاية ملف `erpnext-features.md`** — هذا الملف جاهز للقراءة كمدخل في `gap-analysis.md` (المهمة التالية).
