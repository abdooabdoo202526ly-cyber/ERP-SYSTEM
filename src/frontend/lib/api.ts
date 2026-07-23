// API client للـ ERP-SYSTEM
// يستخدم localStorage لحفظ الـ token

import axios, { AxiosInstance } from 'axios';

// في الإنتاج (HF Spaces): نستخدم same-origin (Caddy reverse proxy)
// في dev: NEXT_PUBLIC_API_URL=http://localhost:5000
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '';

// v1.0.19: Monkey-patch window.fetch to auto-attach Authorization header
// for any /api/* call. This fixes the 22 pages that use raw fetch('/api/...')
// without the token. Idempotent — runs once.
if (typeof window !== 'undefined' && !(window as any).__fetchPatched) {
  const originalFetch = window.fetch.bind(window);
  window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    try {
      const url =
        typeof input === 'string'
          ? input
          : input instanceof URL
            ? input.toString()
            : (input as Request).url;
      if (url && (url.startsWith('/api/') || url.startsWith('api/'))) {
        const token = localStorage.getItem('accessToken');
        const headers: Record<string, string> = {};
        if (init?.headers) {
          const h = init.headers as any;
          if (h instanceof Headers) {
            h.forEach((v, k) => { headers[k] = v; });
          } else if (Array.isArray(h)) {
            h.forEach(([k, v]) => { headers[k] = v as string; });
          } else {
            Object.assign(headers, h);
          }
        }
        if (token && !headers['Authorization'] && !headers['authorization']) {
          headers['Authorization'] = `Bearer ${token}`;
        }
        init = { ...init, headers };
      }
    } catch {
      // If we can't determine the URL, fall through with original fetch
    }
    return originalFetch(input as any, init);
  };
  (window as any).__fetchPatched = true;
}

export const api: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 30000,
});

// Request interceptor: أضف JWT token تلقائياً
api.interceptors.request.use((config) => {
  if (typeof window !== 'undefined') {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// Response interceptor: اعرض errors بشكل أنيق
api.interceptors.response.use(
  (r) => r,
  (err) => {
    if (err.response?.status === 401) {
      if (typeof window !== 'undefined') {
        localStorage.removeItem('accessToken');
        localStorage.removeItem('user');
        window.location.href = '/login';
      }
    }
    // v1.0.21: surface validation details (400) in the error message
    // so the form page can show the actual reason.
    if (err.response?.status === 400 && err.response?.data) {
      const data = err.response.data;
      let detail = '';
      if (data.detail) {
        detail = data.detail;
      } else if (data.errors && typeof data.errors === 'object') {
        // ASP.NET ProblemDetails format
        const parts: string[] = [];
        for (const [field, msgs] of Object.entries(data.errors)) {
          if (Array.isArray(msgs)) parts.push(`${field}: ${msgs.join(', ')}`);
        }
        detail = parts.join(' | ');
      } else if (data.title) {
        detail = data.title;
      }
      if (detail) {
        err.message = `[400] ${detail}`;
      }
    }
    return Promise.reject(err);
  }
);

// ============ Types ============
// ملاحظة: الـ contracts تطابق AuthDtos.cs في الـ backend (C#).
//   - Register: TenantName يُنشئ tenant جديد + Subdomain يُحسب عبر Slugify
//   - Login:    TenantId (Guid) اختياري للبحث داخل tenant محدد

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  tenantName: string;
  baseCurrency?: string;     // optional, default "LYD"
}

export interface LoginRequest {
  email: string;
  password: string;
  tenantId?: string;         // optional (Guid) — إن لم يُرسل، بحث شامل
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  user: UserInfo;
  holdingCompanyId?: string;
}

export interface UserInfo {
  id: string;
  tenantId: string;
  email: string;
  fullName: string;
  roles: string[];
}

// ============ Finance ============
export interface Account {
  id: string;
  tenantId: string;
  companyId?: string;
  code: string;
  name: string;
  description?: string;
  type: number;  // 1=Asset, 2=Liability, 3=Equity, 4=Revenue, 5=Expense
  normalBalance: number;  // 1=Debit, 2=Credit
  parentAccountId?: string;
  isPostable: boolean;
  isActive: boolean;
  isIntercompany: boolean;
  createdAt: string;
  updatedAt: string;
}

export const ACCOUNT_TYPES: Record<number, string> = {
  1: 'أصول',
  2: 'خصوم',
  3: 'حقوق ملكية',
  4: 'إيرادات',
  5: 'مصروفات',
};

// ============ Inventory ============
export interface Item {
  id: string;
  tenantId: string;
  companyId?: string;
  sku: string;
  barcode?: string;
  name: string;
  description?: string;
  categoryId?: string;
  unitOfMeasureId?: string;
  itemType: number;     // 1=RawMaterial, 2=FinishedGood, 3=Consumable, 4=Service
  costingMethod: number; // 1=FIFO, 2=LIFO, 3=Average, 4=Standard
  standardCost?: number; // request field
  averageCost: number;   // response field (computed)
  reorderLevel: number;
  reorderQuantity: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

// ============ Projects ============
export interface Project {
  id: string;
  tenantId: string;
  companyId: string;
  costCenterId: string;
  code: string;
  name: string;
  description?: string;
  status: number;  // 1=Planning, 2=Active, 3=OnHold, 4=Completed, 5=Cancelled
  budget: number;
  startDate: string;
  endDate?: string;
  isActive: boolean;
  createdAt: string;
}

export const PROJECT_STATUSES: Record<number, string> = {
  1: 'تخطيط',
  2: 'نشط',
  3: 'معلق',
  4: 'مكتمل',
  5: 'ملغي',
};

// ============ Reports ============
export interface TrialBalanceRow {
  accountId: string;
  accountCode: string;
  accountName: string;
  accountType: number;
  debit: number;
  credit: number;
}

// ============ Procurement ============
// الـ DTOs تطابق Contracts في `src/backend/Modules/Procurement/Application/Dtos.cs`
// (Backend مبني في فرع منفصل — هذا الـ contract المتوقع بناءً على gap-analysis.md §3)

export interface Vendor {
  id: string;
  tenantId: string;
  code?: string;             // DEC-081: backend `VendorResponse.Code`
  name: string;
  email?: string;
  phone?: string;
  address?: string;
  taxNumber?: string;
  website?: string;          // DEC-081: backend `VendorResponse.Website`
  currency: string;
  paymentTerms: string; // Net30, Net60, Cash
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export const PAYMENT_TERMS: Record<string, string> = {
  Cash: 'نقدي',
  Net15: 'صافي 15 يوم',
  Net30: 'صافي 30 يوم',
  Net60: 'صافي 60 يوم',
  Net90: 'صافي 90 يوم',
};

// PO Status: Draft=1, Pending=2, Approved=3, Sent=4, Received=5, Cancelled=6
export const PO_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'بانتظار الموافقة',
  3: 'معتمد',
  4: 'مُرسل للمورّد',
  5: 'مُستلَم',
  6: 'ملغي',
};

export const PO_STATUS_VARIANTS: Record<number, 'neutral' | 'warning' | 'info' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'warning',
  3: 'info',
  4: 'info',
  5: 'success',
  6: 'danger',
};

export interface PurchaseOrderLine {
  id: string;
  itemId: string;
  itemName?: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  subTotal: number;
}

export interface PurchaseOrder {
  id: string;
  tenantId: string;
  poNumber: string;
  vendorId: string;
  vendorName?: string;
  projectId?: string;        // v1.0.34
  projectName?: string;      // v1.0.34
  costCenterId?: string;     // v1.0.34
  costCenterName?: string;   // v1.0.34
  status: number;
  orderDate: string;
  expectedDate?: string;
  currency: string;
  totalAmount: number;
  notes?: string;
  lines: PurchaseOrderLine[];
  createdAt: string;
}

// GR Status: Draft=1, Received=2, Cancelled=3
export const GR_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُستلَم',
  3: 'ملغي',
};

export const GR_STATUS_VARIANTS: Record<number, 'neutral' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'success',
  3: 'danger',
};

export interface GoodsReceiptLine {
  id: string;
  itemId: string;
  itemName?: string;
  quantity: number;
  notes?: string;
}

export interface GoodsReceipt {
  id: string;
  tenantId: string;
  grNumber: string;
  purchaseOrderId: string;
  poNumber?: string;
  poStatus?: string;        // DEC-031: enriched
  vendorName?: string;
  vendorId?: string;
  vendorCode?: string;      // DEC-031: enriched
  status: number;
  receivedDate: string;
  warehouseId: string;
  warehouseName?: string;
  warehouseCode?: string;   // DEC-031: enriched
  notes?: string;           // DEC-031: enriched
  currency?: string;
  lines: GoodsReceiptLine[];
  createdAt: string;
}

// Bill Status: Draft=1, Posted=2, Paid=3, Cancelled=4
export const BILL_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُرحَّل',
  3: 'مُدفوع',
  4: 'ملغي',
};

export const BILL_STATUS_VARIANTS: Record<number, 'neutral' | 'info' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'info',
  3: 'success',
  4: 'danger',
};

export interface VendorBillLine {
  id: string;
  itemId: string;
  itemName?: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  subTotal: number;
}

export interface VendorBill {
  id: string;
  tenantId: string;
  billNumber: string;
  goodsReceiptId: string;
  grNumber?: string;
  vendorId: string;
  vendorName?: string;
  status: number;
  billDate: string;
  dueDate?: string;
  currency: string;
  subTotal: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  lines: VendorBillLine[];
  journalEntryId?: string; // DEC-081: backend `VendorBillResponse.JournalEntryId`
  postedAt?: string;       // DEC-081: backend `VendorBillResponse.PostedAt`
  createdAt: string;
}

// ============ HR ============
// الـ DTOs تطابق Contracts في `src/backend/Modules/HR/Application/Dtos.cs`

// Leave Type: Annual=1, Sick=2, Emergency=3, Unpaid=4
export const LEAVE_TYPES: Record<number, string> = {
  1: 'سنوية',
  2: 'مرضية',
  3: 'طارئة',
  4: 'بدون راتب',
};

// Leave Status: Pending=1, Approved=2, Rejected=3
export const LEAVE_STATUSES: Record<number, string> = {
  1: 'بانتظار الموافقة',
  2: 'معتمدة',
  3: 'مرفوضة',
};

export const LEAVE_STATUS_VARIANTS: Record<number, 'warning' | 'success' | 'danger'> = {
  1: 'warning',
  2: 'success',
  3: 'danger',
};

// Attendance Type: CheckIn=1, CheckOut=2
export const ATTENDANCE_TYPES: Record<number, string> = {
  1: 'حضور',
  2: 'انصراف',
};

// NOTE: `Department` is defined later in the file (T4-HR-Details section) with
//   extra fields (managerName, createdAt, updatedAt) for cross-team consumption.
//   Refer to the bottom of this file for the canonical definition.

export interface Employee {
  id: string;
  tenantId: string;
  employeeNumber: string;
  fullName: string;
  email: string;
  phone?: string;
  nationalId?: string;
  departmentId?: string;
  departmentName?: string;
  jobTitle?: string;
  hireDate: string;
  terminationDate?: string;
  baseSalary: number;
  isActive: boolean;
  createdAt: string;
}

// T4-HR-Details: payload لـ PUT /api/hr/employees/{id} (UpdateEmployeeRequest).
export interface UpdateEmployeePayload {
  fullName: string;
  email?: string;
  phone?: string;
  nationalId?: string;
  departmentId?: string;
  jobTitle?: string;
  hireDate: string;
  terminationDate?: string;
  baseSalary: number;
  isActive: boolean;
}

// T4-HR-Details: payload لـ PUT /api/hr/leaves/{id} (UpdateLeaveRequestDto).
// employeeId غير قابل للتعديل (لمنع نقل طلب إجازة بين موظفين).
export interface UpdateLeaveRequestPayload {
  leaveType: number;
  startDate: string;
  endDate: string;
  reason?: string;
  notes?: string;
}

// Attendance record as returned by GET /api/hr/attendance (T4).
// الـ BE يرسل Type كـ enum (CheckIn=1, CheckOut=2) — نُبقيه كـ number مع
//   مَركَز mapping في ATTENDANCE_TYPES أعلاه.
export interface AttendanceRecord {
  id: string;
  tenantId: string;
  employeeId: string;
  employeeName?: string;
  type: number; // 1=CheckIn, 2=CheckOut
  timestamp: string;
  notes?: string;
  ipAddress?: string;
  createdAt?: string;
}

export interface LeaveRequest {
  id: string;
  tenantId: string;
  employeeId: string;
  employeeName?: string;
  leaveType: number;
  startDate: string;
  endDate: string;
  totalDays: number;
  status: number;
  reason?: string;
  approverId?: string;
  approverName?: string;
  approvedAt?: string;
  notes?: string;
  createdAt: string;
}

// ============ Payroll ============
// الـ DTOs تطابق Contracts في `src/backend/Modules/Payroll/Application/Dtos.cs`
// الـ state machine: Draft=1, Processing=2, Posted=3, Cancelled=4

export const PAYROLL_RUN_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'قيد المعالجة',
  3: 'مُرحَّل',
  4: 'ملغي',
};

export const PAYROLL_RUN_STATUS_VARIANTS: Record<number, 'neutral' | 'warning' | 'info' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'warning',
  3: 'success',
  4: 'danger',
};

// PayrollItem status: Draft=1, Processed=2, Posted=3, Cancelled=4
export const PAYROLL_ITEM_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُعالَج',
  3: 'مُرحَّل',
  4: 'ملغي',
};

// SalaryComponentType: Earning=1, Deduction=2
export const COMPONENT_TYPES: Record<number, 'earning' | 'deduction'> = {
  1: 'earning',
  2: 'deduction',
};

export const COMPONENT_TYPE_LABELS: Record<number, string> = {
  1: 'مستحق',
  2: 'مستقطع',
};

export interface PayrollRun {
  id: string;
  tenantId: string;
  periodStart: string;
  periodEnd: string;
  status: number;
  totalGross: number;
  totalNet: number;
  processedAt?: string;
  postedAt?: string;
  notes?: string;
  createdAt: string;
  itemsCount?: number;
}

export interface PayslipComponent {
  id: string;
  componentType: number;
  name: string;
  amount: number;
  sortOrder: number;
}

export interface PayrollItem {
  id: string;
  tenantId: string;
  payrollRunId: string;
  employeeId: string;
  employeeNumber?: string;
  employeeName?: string;
  baseSalary: number;
  grossSalary: number;
  taxAmount: number;
  socialInsuranceEmployee: number;
  netSalary: number;
  status: number;
  paymentDays: number;
  notes?: string;
  components: PayslipComponent[];
}

export interface Payslip extends PayrollItem {}

export interface EosResponse {
  employeeId: string;
  employeeNumber?: string;
  employeeName?: string;
  hireDate: string;
  terminationDate: string;
  yearsOfService: number;
  monthlySalary: number;
  eosAmount: number;
  formula: string;
}

export interface CreatePayrollRunRequest {
  periodStart: string;
  periodEnd: string;
  notes?: string;
}

// ============ Accounts Receivable (AR) ============
// الـ DTOs تطابق Contracts في `src/backend/Modules/AccountsReceivable/Application/Dtos.cs`

export interface Customer {
  id: string;
  tenantId: string;
  companyId: string;
  code: string;
  name: string;
  nameEn?: string;
  taxId?: string;
  email?: string;
  phone?: string;
  address?: string;
  creditLimit?: number;
  paymentTermsDays: number;
  isActive: boolean;
}

export interface SalesInvoiceLine {
  id: string;
  lineNumber: number;
  description: string;
  itemId?: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
  lineTotal: number;
}

// SalesInvoice status: Draft=1, Sent=2, PartiallyPaid=3, Paid=4, Overdue=5, Cancelled=6
export const SALES_INVOICE_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُرسل',
  3: 'مدفوع جزئياً',
  4: 'مدفوع',
  5: 'متأخر',
  6: 'ملغي',
};

export const SALES_INVOICE_STATUS_VARIANTS: Record<number, 'neutral' | 'info' | 'warning' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'info',
  3: 'warning',
  4: 'success',
  5: 'danger',
  6: 'danger',
};

export interface SalesInvoice {
  id: string;
  tenantId: string;
  customerId: string;
  customerName?: string;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate?: string;
  currencyCode: string;
  exchangeRate: number;
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  paidAmount: number;
  outstanding: number;
  status: number;
  notes?: string;
  projectId?: string;
  postedAt?: string;
  journalEntryId?: string;
  createdAt: string;
  lines: SalesInvoiceLine[];
  allocations: ReceiptAllocation[];
}

export const PAYMENT_METHODS: Record<string, string> = {
  Cash: 'نقدي',
  Bank: 'بنك',
  Transfer: 'تحويل',
  Check: 'شيك',
};

export interface ReceiptAllocation {
  id: string;
  salesInvoiceId: string;
  salesInvoiceNumber?: string;
  amountApplied: number;
}

export interface Receipt {
  id: string;
  tenantId: string;
  customerId: string;
  customerName?: string;
  receiptNumber: string;
  receiptDate: string;
  amount: number;
  currencyCode: string;
  paymentMethod?: string;
  notes?: string;
  postedAt?: string;
  journalEntryId?: string;
  createdAt: string;
  allocations: ReceiptAllocation[];
}

export interface ArAgingBucket {
  bucket0To30: number;
  bucket31To60: number;
  bucket61To90: number;
  bucket91To120: number;
  bucket120Plus: number;
  total: number;
}

export interface ArAgingRow {
  customerId: string;
  customerCode: string;
  customerName: string;
  buckets: ArAgingBucket;
}

export interface ArAgingReport {
  asOfDate: string;
  rows: ArAgingRow[];
  grandTotal: ArAgingBucket;
}

// ============ AR API ============
// endpoints: /api/ar/{customers|sales-invoices|receipts|aging}

export const arApi = {
  // ----- Customers -----
  listCustomers: async (): Promise<Customer[]> => {
    const r = await api.get<Customer[]>('/api/ar/customers');
    return r.data;
  },
  getCustomer: async (id: string): Promise<Customer> => {
    const r = await api.get<Customer>(`/api/ar/customers/${id}`);
    return r.data;
  },
  createCustomer: async (data: Omit<Customer, 'id' | 'tenantId' | 'companyId' | 'isActive'>): Promise<Customer> => {
    const r = await api.post<Customer>('/api/ar/customers', data);
    return r.data;
  },
  updateCustomer: async (id: string, data: Partial<Omit<Customer, 'id' | 'tenantId' | 'companyId'>>): Promise<Customer> => {
    const r = await api.put<Customer>(`/api/ar/customers/${id}`, data);
    return r.data;
  },
  // v1.0.23: soft delete customer
  deleteCustomer: async (id: string): Promise<void> => {
    await api.delete(`/api/ar/customers/${id}`);
  },
  // v1.0.23: deactivate alias
  deactivateCustomer: async (id: string): Promise<void> => {
    await api.delete(`/api/ar/customers/${id}`);
  },

  // ----- Sales Invoices -----
  listInvoices: async (): Promise<SalesInvoice[]> => {
    const r = await api.get<SalesInvoice[]>('/api/ar/sales-invoices');
    return r.data;
  },
  getInvoice: async (id: string): Promise<SalesInvoice> => {
    const r = await api.get<SalesInvoice>(`/api/ar/sales-invoices/${id}`);
    return r.data;
  },
  createInvoice: async (data: {
    customerId: string;
    invoiceDate: string;
    dueDate?: string;
    currencyCode: string;
    exchangeRate: number;
    notes?: string;
    projectId?: string;
    lines: { description: string; quantity: number; unitPrice: number; taxRate: number; itemId?: string }[];
    postImmediately?: boolean;
  }): Promise<SalesInvoice> => {
    const r = await api.post<SalesInvoice>('/api/ar/sales-invoices', data);
    return r.data;
  },
  updateInvoice: async (id: string, data: Partial<SalesInvoice>): Promise<SalesInvoice> => {
    const r = await api.put<SalesInvoice>(`/api/ar/sales-invoices/${id}`, data);
    return r.data;
  },
  postInvoice: async (id: string): Promise<SalesInvoice> => {
    const r = await api.put<SalesInvoice>(`/api/ar/sales-invoices/${id}/post`);
    return r.data;
  },
  cancelInvoice: async (id: string): Promise<SalesInvoice> => {
    const r = await api.put<SalesInvoice>(`/api/ar/sales-invoices/${id}/cancel`);
    return r.data;
  },

  // ----- Receipts -----
  listReceipts: async (): Promise<Receipt[]> => {
    const r = await api.get<Receipt[]>('/api/ar/receipts');
    return r.data;
  },
  getReceipt: async (id: string): Promise<Receipt> => {
    const r = await api.get<Receipt>(`/api/ar/receipts/${id}`);
    return r.data;
  },
  createReceipt: async (data: {
    customerId: string;
    receiptDate: string;
    amount: number;
    currencyCode: string;
    paymentMethod?: string;
    notes?: string;
    allocations: { salesInvoiceId: string; amountApplied: number }[];
    postImmediately?: boolean;
  }): Promise<Receipt> => {
    const r = await api.post<Receipt>('/api/ar/receipts', data);
    return r.data;
  },
  postReceipt: async (id: string): Promise<Receipt> => {
    const r = await api.put<Receipt>(`/api/ar/receipts/${id}/post`);
    return r.data;
  },
  reverseReceipt: async (id: string): Promise<Receipt> => {
    const r = await api.put<Receipt>(`/api/ar/receipts/${id}/reverse`);
    return r.data;
  },

  // ----- Aging Report -----
  aging: async (asOfDate?: string): Promise<ArAgingReport> => {
    const r = await api.get<ArAgingReport>('/api/ar/aging', { params: asOfDate ? { asOfDate } : undefined });
    return r.data;
  },
};

// ============ AR Customer/Receipt namespaced aliases ============
// بعض الصفحات/الفرق تُفضّل namespace صريح `customersApi`/`receiptsApi`
// (يُماثل موارد الـ backend). هذه أغلفة رقيقة فوق `arApi`.

export const customersApi = {
  list: (): Promise<Customer[]> => arApi.listCustomers(),
  get: (id: string): Promise<Customer> => arApi.getCustomer(id),
  create: (data: Omit<Customer, 'id' | 'tenantId' | 'companyId' | 'isActive'>): Promise<Customer> =>
    arApi.createCustomer(data),
  update: (
    id: string,
    data: Partial<Omit<Customer, 'id' | 'tenantId' | 'companyId'>>
  ): Promise<Customer> => arApi.updateCustomer(id, data),
  deactivate: (id: string): Promise<void> => arApi.deactivateCustomer(id),
};

export const receiptsApi = {
  list: (): Promise<Receipt[]> => arApi.listReceipts(),
  get: (id: string): Promise<Receipt> => arApi.getReceipt(id),
  create: (data: {
    customerId: string;
    receiptDate: string;
    amount: number;
    currencyCode: string;
    paymentMethod?: string;
    notes?: string;
    allocations: { salesInvoiceId: string; amountApplied: number }[];
    postImmediately?: boolean;
  }): Promise<Receipt> => arApi.createReceipt(data),
  post: (id: string): Promise<Receipt> => arApi.postReceipt(id),
  reverse: (id: string): Promise<Receipt> => arApi.reverseReceipt(id),
};

// ============ Error extraction helper ============
// للحصول على رسالة خطأ أنيقة من Axios errors
export interface ApiError {
  detail?: string;
  message?: string;
}

export function getErrorMessage(e: unknown, fallback = 'حدث خطأ غير متوقع'): string {
  const err = e as { response?: { data?: ApiError }; message?: string };
  return err?.response?.data?.detail || err?.response?.data?.message || err?.message || fallback;
}

// ============ API helpers ============

export const authApi = {
  register: async (data: RegisterRequest): Promise<AuthResponse> => {
    const r = await api.post<AuthResponse>('/api/auth/register', data);
    if (typeof window !== 'undefined') {
      localStorage.setItem('accessToken', r.data.accessToken);
      localStorage.setItem('refreshToken', r.data.refreshToken);
      localStorage.setItem('user', JSON.stringify(r.data.user));
    }
    return r.data;
  },
  login: async (data: LoginRequest): Promise<AuthResponse> => {
    const r = await api.post<AuthResponse>('/api/auth/login', data);
    if (typeof window !== 'undefined') {
      localStorage.setItem('accessToken', r.data.accessToken);
      localStorage.setItem('refreshToken', r.data.refreshToken);
      localStorage.setItem('user', JSON.stringify(r.data.user));
    }
    return r.data;
  },
  logout: () => {
    if (typeof window !== 'undefined') {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
      localStorage.removeItem('user');
    }
  },
  me: async (): Promise<UserInfo> => {
    const r = await api.get<UserInfo>('/api/auth/me');
    return r.data;
  },
  getUser: (): UserInfo | null => {
    if (typeof window === 'undefined') return null;
    const u = localStorage.getItem('user');
    return u ? JSON.parse(u) : null;
  },
  isLoggedIn: (): boolean => {
    if (typeof window === 'undefined') return false;
    return !!localStorage.getItem('accessToken');
  },
};

export const financeApi = {
  listAccounts: async (): Promise<Account[]> => {
    const r = await api.get<Account[]>('/api/finance/accounts');
    return r.data;
  },
  // v1.0.21: single-account fetch
  getAccount: async (id: string): Promise<Account> => {
    const r = await api.get<Account>(`/api/finance/accounts/${id}`);
    return r.data;
  },
  createAccount: async (data: Partial<Account>): Promise<Account> => {
    const r = await api.post<Account>('/api/finance/accounts', data);
    return r.data;
  },
  updateAccount: async (id: string, data: Partial<Account>): Promise<Account> => {
    const r = await api.put<Account>(`/api/finance/accounts/${id}`, data);
    return r.data;
  },
  // v1.0.21: delete (soft delete via IsActive=false on the backend)
  deleteAccount: async (id: string): Promise<void> => {
    await api.delete(`/api/finance/accounts/${id}`);
  },
  trialBalance: async (asOfDate: string): Promise<TrialBalanceReport> => {
    const r = await api.get<TrialBalanceReport>('/api/reports/finance/trial-balance', {
      params: { asOfDate },
    });
    return r.data;
  },
};

export const inventoryApi = {
  listItems: async (): Promise<Item[]> => {
    const r = await api.get<Item[]>('/api/inventory/items');
    return r.data;
  },
  // v1.0.22: complete CRUD for items
  getItem: async (id: string): Promise<Item> => {
    const r = await api.get<Item>(`/api/inventory/items/${id}`);
    return r.data;
  },
  createItem: async (data: Partial<Item>): Promise<Item> => {
    const r = await api.post<Item>('/api/inventory/items', data);
    return r.data;
  },
  updateItem: async (id: string, data: Partial<Item>): Promise<Item> => {
    const r = await api.put<Item>(`/api/inventory/items/${id}`, data);
    return r.data;
  },
  deleteItem: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/items/${id}`);
  },
  // Unit of measure
  listUoms: async (): Promise<UnitOfMeasure[]> => {
    const r = await api.get<UnitOfMeasure[]>('/api/inventory/uom');
    return r.data;
  },
  createUom: async (data: Partial<UnitOfMeasure>): Promise<UnitOfMeasure> => {
    const r = await api.post<UnitOfMeasure>('/api/inventory/uom', data);
    return r.data;
  },
  updateUom: async (id: string, data: Partial<UnitOfMeasure>): Promise<UnitOfMeasure> => {
    const r = await api.put<UnitOfMeasure>(`/api/inventory/uom/${id}`, data);
    return r.data;
  },
  deactivateUom: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/uom/${id}`);
  },
  // Categories
  listItemCategories: async (): Promise<ItemCategory[]> => {
    const r = await api.get<ItemCategory[]>('/api/inventory/categories');
    return r.data;
  },
  createItemCategory: async (data: Partial<ItemCategory>): Promise<ItemCategory> => {
    const r = await api.post<ItemCategory>('/api/inventory/categories', data);
    return r.data;
  },
  // v1.0.23: complete CRUD for item categories
  getItemCategory: async (id: string): Promise<ItemCategory> => {
    const r = await api.get<ItemCategory>(`/api/inventory/categories/${id}`);
    return r.data;
  },
  updateItemCategory: async (id: string, data: Partial<ItemCategory>): Promise<ItemCategory> => {
    const r = await api.put<ItemCategory>(`/api/inventory/categories/${id}`, data);
    return r.data;
  },
  deleteItemCategory: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/categories/${id}`);
  },
  // Warehouses
  listWarehouses: async (): Promise<Warehouse[]> => {
    const r = await api.get<Warehouse[]>('/api/inventory/warehouses');
    return r.data;
  },
  // v1.0.23: complete CRUD for warehouses
  getWarehouse: async (id: string): Promise<Warehouse> => {
    const r = await api.get<Warehouse>(`/api/inventory/warehouses/${id}`);
    return r.data;
  },
  createWarehouse: async (data: Partial<Warehouse>): Promise<Warehouse> => {
    const r = await api.post<Warehouse>('/api/inventory/warehouses', data);
    return r.data;
  },
  updateWarehouse: async (id: string, data: Partial<Warehouse>): Promise<Warehouse> => {
    const r = await api.put<Warehouse>(`/api/inventory/warehouses/${id}`, data);
    return r.data;
  },
  deleteWarehouse: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/warehouses/${id}`);
  },
  // v1.0.23: movements and reservations (basic stubs; type-safe wrappers in v1.0.24)
  // Note: full CRUD for movements/reservations requires dedicated interfaces
  // that don't exist yet in this file. The new/page.tsx files have their own
  // /api/inventory/movements and /api/inventory/reservations POST logic.
  // deleteReservation: backend supports DELETE /api/inventory/reservations/{id}
  // (confirmed in StockReservationsController)
  // deleteMovement: backend does NOT yet expose DELETE for movements
  // (movement is append-only in financial accounting),
};

// v1.0.22: minimal shape for category (the backend may not have a full DTO)
export interface ItemCategory {
  id: string;
  code: string;
  name: string;
  parentId?: string;
  isActive: boolean;
}

export const projectsApi = {
  listProjects: async (): Promise<Project[]> => {
    const r = await api.get<Project[]>('/api/projects');
    return r.data;
  },
  // v1.0.22: complete CRUD
  getProject: async (id: string): Promise<Project> => {
    const r = await api.get<Project>(`/api/projects/${id}`);
    return r.data;
  },
  createProject: async (data: Partial<Project>): Promise<Project> => {
    const r = await api.post<Project>('/api/projects', data);
    return r.data;
  },
  updateProject: async (id: string, data: Partial<Project>): Promise<Project> => {
    const r = await api.put<Project>(`/api/projects/${id}`, data);
    return r.data;
  },
  deleteProject: async (id: string): Promise<void> => {
    await api.delete(`/api/projects/${id}`);
  },
  // Cost centers
  listCostCenters: async (): Promise<CostCenterLite[]> => {
    const r = await api.get<CostCenterLite[]>('/api/cost-centers');
    return r.data;
  },
  createCostCenter: async (data: Partial<CostCenterLite>): Promise<CostCenterLite> => {
    const r = await api.post<CostCenterLite>('/api/cost-centers', data);
    return r.data;
  },
  updateCostCenter: async (id: string, data: Partial<CostCenterLite>): Promise<CostCenterLite> => {
    const r = await api.put<CostCenterLite>(`/api/cost-centers/${id}`, data);
    return r.data;
  },
  deleteCostCenter: async (id: string): Promise<void> => {
    await api.delete(`/api/cost-centers/${id}`);
  },
};

// v1.0.22: minimal CostCenter shape
export interface CostCenterLite {
  id: string;
  code: string;
  name: string;
  type?: number;
  isActive: boolean;
}

// ============ Procurement API ============
// endpoints: /api/procurement/{vendors|pos|grs|bills}

export const procurementApi = {
  // ----- Vendors -----
  listVendors: async (): Promise<Vendor[]> => {
    const r = await api.get<Vendor[]>('/api/procurement/vendors');
    return r.data;
  },
  getVendor: async (id: string): Promise<Vendor> => {
    const r = await api.get<Vendor>(`/api/procurement/vendors/${id}`);
    return r.data;
  },
  createVendor: async (data: Partial<Vendor>): Promise<Vendor> => {
    const r = await api.post<Vendor>('/api/procurement/vendors', data);
    return r.data;
  },
  // v1.0.22: delete vendor
  deleteVendor: async (id: string): Promise<void> => {
    await api.delete(`/api/procurement/vendors/${id}`);
  },
  updateVendor: async (id: string, data: Partial<Vendor>): Promise<Vendor> => {
    const r = await api.put<Vendor>(`/api/procurement/vendors/${id}`, data);
    return r.data;
  },
  deactivateVendor: async (id: string): Promise<void> => {
    await api.delete(`/api/procurement/vendors/${id}`);
  },

  // ----- Purchase Orders -----
  listPOs: async (): Promise<PurchaseOrder[]> => {
    const r = await api.get<PurchaseOrder[]>('/api/procurement/pos');
    return r.data;
  },
  getPO: async (id: string): Promise<PurchaseOrder> => {
    const r = await api.get<PurchaseOrder>(`/api/procurement/pos/${id}`);
    return r.data;
  },
  createPO: async (data: Partial<PurchaseOrder>): Promise<PurchaseOrder> => {
    const r = await api.post<PurchaseOrder>('/api/procurement/pos', data);
    return r.data;
  },
  approvePO: async (id: string): Promise<PurchaseOrder> => {
    // الـ backend يستخدم PUT /api/procurement/pos/{id}/approve
    const r = await api.put<PurchaseOrder>(`/api/procurement/pos/${id}/approve`);
    return r.data;
  },
  sendPO: async (id: string): Promise<PurchaseOrder> => {
    // الـ backend يستخدم PUT /api/procurement/pos/{id}/send
    const r = await api.put<PurchaseOrder>(`/api/procurement/pos/${id}/send`);
    return r.data;
  },
  // v1.0.31: cancel PO
  cancelPO: async (id: string): Promise<void> => {
    await api.put(`/api/procurement/pos/${id}/cancel`);
  },

  // ----- Goods Receipts -----
  listGRs: async (): Promise<GoodsReceipt[]> => {
    const r = await api.get<GoodsReceipt[]>('/api/procurement/grs');
    return r.data;
  },
  getGR: async (id: string): Promise<GoodsReceipt> => { // DEC-031
    const r = await api.get<GoodsReceipt>(`/api/procurement/grs/${id}`);
    return r.data;
  },
  createGR: async (data: Partial<GoodsReceipt>): Promise<GoodsReceipt> => {
    const r = await api.post<GoodsReceipt>('/api/procurement/grs', data);
    return r.data;
  },
  // v1.0.27: receive (post) a Draft GR — creates stock movements
  receiveGR: async (id: string): Promise<GoodsReceipt> => {
    const r = await api.put<GoodsReceipt>(`/api/procurement/grs/${id}/receive`);
    return r.data;
  },
  // v1.0.27: cancel a Draft GR
  cancelGR: async (id: string): Promise<void> => {
    await api.put(`/api/procurement/grs/${id}/cancel`);
  },
  // v1.0.23: complete CRUD
  deleteGR: async (id: string): Promise<void> => {
    await api.delete(`/api/procurement/grs/${id}`);
  },

  // ----- Vendor Bills -----
  listBills: async (): Promise<VendorBill[]> => {
    const r = await api.get<VendorBill[]>('/api/procurement/bills');
    return r.data;
  },
  getBill: async (id: string): Promise<VendorBill> => {
    const r = await api.get<VendorBill>(`/api/procurement/bills/${id}`);
    return r.data;
  },
  createBill: async (data: Partial<VendorBill>): Promise<VendorBill> => {
    const r = await api.post<VendorBill>('/api/procurement/bills', data);
    return r.data;
  },
  updateBill: async (id: string, data: Partial<VendorBill>): Promise<VendorBill> => {
    const r = await api.put<VendorBill>(`/api/procurement/bills/${id}`, data);
    return r.data;
  },
  deleteBill: async (id: string): Promise<void> => {
    await api.delete(`/api/procurement/bills/${id}`);
  },
  postBill: async (id: string): Promise<VendorBill> => {
    // الـ backend يستخدم PUT /api/procurement/bills/{id}/post
    const r = await api.put<VendorBill>(`/api/procurement/bills/${id}/post`);
    return r.data;
  },

  // v1.0.23: complete CRUD for Purchase Orders
  updatePO: async (id: string, data: Partial<PurchaseOrder>): Promise<PurchaseOrder> => {
    const r = await api.put<PurchaseOrder>(`/api/procurement/pos/${id}`, data);
    return r.data;
  },
  deletePO: async (id: string): Promise<void> => {
    await api.delete(`/api/procurement/pos/${id}`);
  },
};

// ============ HR API ============
// endpoints: /api/hr/{employees|attendance|departments|leaves}

export const hrApi = {
  // ----- Departments -----
  listDepartments: async (): Promise<Department[]> => {
    const r = await api.get<Department[]>('/api/hr/departments');
    return r.data;
  },
  // v1.0.23: complete CRUD
  getDepartment: async (id: string): Promise<Department> => {
    const r = await api.get<Department>(`/api/hr/departments/${id}`);
    return r.data;
  },
  createDepartment: async (data: Partial<Department>): Promise<Department> => {
    const r = await api.post<Department>('/api/hr/departments', data);
    return r.data;
  },
  updateDepartment: async (id: string, data: Partial<Department>): Promise<Department> => {
    const r = await api.put<Department>(`/api/hr/departments/${id}`, data);
    return r.data;
  },
  deleteDepartment: async (id: string): Promise<void> => {
    await api.delete(`/api/hr/departments/${id}`);
  },

  // ----- Employees -----
  listEmployees: async (): Promise<Employee[]> => {
    const r = await api.get<Employee[]>('/api/hr/employees');
    return r.data;
  },
  // T4-HR-Details: GET /api/hr/employees/{id}
  getEmployee: async (id: string): Promise<Employee> => {
    const r = await api.get<Employee>(`/api/hr/employees/${id}`);
    return r.data;
  },
  createEmployee: async (data: Partial<Employee>): Promise<Employee> => {
    const r = await api.post<Employee>('/api/hr/employees', data);
    return r.data;
  },
  // T4-HR-Details: PUT /api/hr/employees/{id} — UpdateEmployeeRequest
  updateEmployee: async (id: string, data: UpdateEmployeePayload): Promise<Employee> => {
    const r = await api.put<Employee>(`/api/hr/employees/${id}`, data);
    return r.data;
  },
  // T4-HR-Details: DELETE /api/hr/employees/{id} — soft-deactivate (isActive=false)
  deactivateEmployee: async (id: string): Promise<void> => {
    await api.delete(`/api/hr/employees/${id}`);
  },

  // ----- Attendance -----
  listAttendance: async (params?: { employeeId?: string; from?: string; to?: string }): Promise<AttendanceRecord[]> => {
    const r = await api.get<AttendanceRecord[]>('/api/hr/attendance', { params });
    return r.data;
  },
  // T4-HR-Details: GET /api/hr/attendance/{id}
  getAttendance: async (id: string): Promise<AttendanceRecord> => {
    const r = await api.get<AttendanceRecord>(`/api/hr/attendance/${id}`);
    return r.data;
  },
  // T4-HR-Details: PUT /api/hr/attendance/{id} — نفس اليوم فقط (يفرضه الـ BE)
  updateAttendance: async (id: string, data: { notes?: string }): Promise<AttendanceRecord> => {
    const r = await api.put<AttendanceRecord>(`/api/hr/attendance/${id}`, data);
    return r.data;
  },
  // T4-HR-Details: DELETE /api/hr/attendance/{id} — نفس اليوم فقط
  deleteAttendance: async (id: string): Promise<void> => {
    await api.delete(`/api/hr/attendance/${id}`);
  },
  // CheckIn/CheckOut — body: { employeeId, type: 1|2 }
  recordAttendance: async (data: { employeeId: string; type: number; notes?: string }): Promise<AttendanceRecord> => {
    const r = await api.post<AttendanceRecord>('/api/hr/attendance', data);
    return r.data;
  },

  // ----- Leaves -----
  listLeaves: async (): Promise<LeaveRequest[]> => {
    const r = await api.get<LeaveRequest[]>('/api/hr/leaves');
    return r.data;
  },
  // T4-HR-Details: GET /api/hr/leaves/{id}
  getLeave: async (id: string): Promise<LeaveRequest> => {
    const r = await api.get<LeaveRequest>(`/api/hr/leaves/${id}`);
    return r.data;
  },
  createLeave: async (data: Partial<LeaveRequest>): Promise<LeaveRequest> => {
    const r = await api.post<LeaveRequest>('/api/hr/leaves', data);
    return r.data;
  },
  // T4-HR-Details: PUT /api/hr/leaves/{id} — تعديل طلب (متاح فقط إذا Pending)
  updateLeave: async (id: string, data: UpdateLeaveRequestPayload): Promise<LeaveRequest> => {
    const r = await api.put<LeaveRequest>(`/api/hr/leaves/${id}`, data);
    return r.data;
  },
  approveLeave: async (id: string): Promise<LeaveRequest> => {
    const r = await api.put<LeaveRequest>(`/api/hr/leaves/${id}/approve`);
    return r.data;
  },
  rejectLeave: async (id: string): Promise<LeaveRequest> => {
    const r = await api.put<LeaveRequest>(`/api/hr/leaves/${id}/reject`);
    return r.data;
  },
  // v1.0.23: no cancel endpoint; we use reject as the soft-cancel flow

  // ----- Payroll (Phase 4) -----
  // endpoints: /api/hr/payroll/{runs|runs/{id}|runs/{id}/{process|post|items}|eos/{empId}}
  payroll: {
    // قائمة دورات الرواتب للـ tenant (مع filter اختياري على الحالة).
    listPayrollRuns: async (params?: { status?: number }): Promise<PayrollRun[]> => {
      const r = await api.get<PayrollRun[]>('/api/hr/payroll/runs', { params });
      return r.data;
    },
    // تفاصيل دورة رواتب واحدة (Run header).
    getPayrollRun: async (id: string): Promise<PayrollRun> => {
      const r = await api.get<PayrollRun>(`/api/hr/payroll/runs/${id}`);
      return r.data;
    },
    // إنشاء دورة رواتب جديدة (Draft).
    createPayrollRun: async (data: CreatePayrollRunRequest): Promise<PayrollRun> => {
      const r = await api.post<PayrollRun>('/api/hr/payroll/runs', data);
      return r.data;
    },
    // معالجة الدورة: يحسب payslip لكل موظف نشط.
    processPayrollRun: async (id: string): Promise<PayrollRun> => {
      const r = await api.post<PayrollRun>(`/api/hr/payroll/runs/${id}/process`);
      return r.data;
    },
    // ترحيل الدورة: ينشئ JournalEntry ويحدّث الحالة إلى Posted.
    postPayrollRun: async (id: string): Promise<PayrollRun> => {
      const r = await api.post<PayrollRun>(`/api/hr/payroll/runs/${id}/post`);
      return r.data;
    },
    // قائمة payslips الدورة.
    getPayrollRunItems: async (runId: string): Promise<PayrollItem[]> => {
      const r = await api.get<PayrollItem[]>(`/api/hr/payroll/runs/${runId}/items`);
      return r.data;
    },
    // v1.0.32: جلب قسيمة راتب موظف واحد
    getPayrollRunItem: async (runId: string, empId: string): Promise<PayrollItem> => {
      const r = await api.get<PayrollItem>(`/api/hr/payroll/runs/${runId}/items/${empId}/payslip`);
      return r.data;
    },
    // تفاصيل payslip موظف واحد ضمن الدورة.
    getPayslip: async (runId: string, employeeId: string): Promise<Payslip> => {
      const r = await api.get<Payslip>(`/api/hr/payroll/runs/${runId}/items/${employeeId}/payslip`);
      return r.data;
    },
    // حساب مستحقات نهاية الخدمة (EOS) لموظف.
    getEos: async (employeeId: string, terminationDate?: string): Promise<EosResponse> => {
      const r = await api.get<EosResponse>(`/api/hr/payroll/eos/${employeeId}`, {
        params: terminationDate ? { terminationDate } : undefined,
      });
      return r.data;
    },
  },
};

// ============ STUBS for cross-team unblock ============
// NOTE: these stubs are intentionally minimal — they exist ONLY so that the
// production build (next build) doesn't fail-fast on TypeScript errors caused
// by missing exports referenced by parallel-team pages (T1 / T2 / T3 / T4).
// The owning team should replace each stub with the full implementation
// matching the corresponding backend module.
// T4-HR-Details owns: departmentsApi, salaryStructuresApi, SalaryStructure types.
// Cross-team (best-effort, minimal — replace ASAP): companiesApi, customersApi,
// receiptsApi, uomApi, warehousesApi, paymentsApi, reportsApi, and the labels.

export interface Department {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  parentId?: string;
  managerId?: string;
  managerName?: string;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export const departmentsApi = {
  list: async (includeInactive = false): Promise<Department[]> => {
    const r = await api.get<Department[]>('/api/hr/departments', { params: { includeInactive } });
    return r.data;
  },
  get: async (id: string): Promise<Department> => {
    const r = await api.get<Department>(`/api/hr/departments/${id}`);
    return r.data;
  },
  create: async (data: { code: string; name: string; parentId?: string; managerId?: string }): Promise<Department> => {
    const r = await api.post<Department>('/api/hr/departments', data);
    return r.data;
  },
  update: async (id: string, data: { name: string; parentId?: string; managerId?: string; isActive: boolean }): Promise<Department> => {
    const r = await api.put<Department>(`/api/hr/departments/${id}`, data);
    return r.data;
  },
  deactivate: async (id: string): Promise<void> => {
    await api.delete(`/api/hr/departments/${id}`);
  },
};

// ============ Salary Structure (Phase 4 — T1 owner) ============
// The full DTO set is owned by T1 (T1-Lookups-Salary) — the stubs below are
// intentionally minimal. T1 should replace them with the real definitions.

export const SALARY_COMPONENT_TYPE_LABELS: Record<number, string> = {
  1: 'مستحق (Earning)',
  2: 'مستقطع (Deduction)',
};

export interface SalaryStructureLine {
  id?: string;
  type: number; // 1 = Earning, 2 = Deduction
  name: string;
  formula?: string;
  amount: number;
  sortOrder: number;
}

export interface SalaryStructure {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  currency: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  lines: SalaryStructureLine[];
  /** مجموع المستحقات (محسوب في الـ BE). */
  totalEarnings: number;
  /** مجموع المستقطعات (محسوب في الـ BE). */
  totalDeductions: number;
}

export interface CreateSalaryStructureRequest {
  name: string;
  code: string;
  currency?: string;
  isActive?: boolean;
  lines: SalaryStructureLine[];
}

export const salaryStructuresApi = {
  list: async (includeInactive = false): Promise<SalaryStructure[]> => {
    const r = await api.get<SalaryStructure[]>('/api/hr/salary-structures', { params: { includeInactive } });
    return r.data;
  },
  get: async (id: string): Promise<SalaryStructure> => {
    const r = await api.get<SalaryStructure>(`/api/hr/salary-structures/${id}`);
    return r.data;
  },
  create: async (data: CreateSalaryStructureRequest): Promise<SalaryStructure> => {
    const r = await api.post<SalaryStructure>('/api/hr/salary-structures', data);
    return r.data;
  },
  update: async (id: string, data: CreateSalaryStructureRequest): Promise<SalaryStructure> => {
    const r = await api.put<SalaryStructure>(`/api/hr/salary-structures/${id}`, data);
    return r.data;
  },
  deactivate: async (id: string): Promise<void> => {
    await api.delete(`/api/hr/salary-structures/${id}`);
  },
};

// ============ Companies (T1-Lookups-Salary owner) ============

export interface Company {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  legalName?: string;
  parentCompanyId?: string;
  isGroup: boolean;
  baseCurrency: string;
  isActive: boolean;
  taxId?: string;
  phone?: string;
  email?: string;
  address?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CompanyTreeNode {
  company?: Company;
  children: CompanyTreeNode[];
}

export const companiesApi = {
  listCompanies: async (includeInactive = false): Promise<Company[]> => {
    const r = await api.get<Company[]>('/api/companies', { params: { includeInactive } });
    return r.data;
  },
  getTree: async (): Promise<CompanyTreeNode> => {
    const r = await api.get<CompanyTreeNode>('/api/companies/tree');
    return r.data;
  },
  getCompany: async (id: string): Promise<Company> => {
    const r = await api.get<Company>(`/api/companies/${id}`);
    return r.data;
  },
  getSubsidiaries: async (id: string): Promise<Company[]> => {
    const r = await api.get<Company[]>(`/api/companies/${id}/subsidiaries`);
    return r.data;
  },
  createHolding: async (data: { code: string; name: string; legalName?: string; baseCurrency: string }): Promise<Company> => {
    const r = await api.post<Company>('/api/companies/holding', data);
    return r.data;
  },
  addSubsidiary: async (data: { parentCompanyId: string; code: string; name: string; legalName?: string }): Promise<Company> => {
    const r = await api.post<Company>('/api/companies/subsidiary', data);
    return r.data;
  },
  deactivateCompany: async (id: string): Promise<void> => {
    await api.delete(`/api/companies/${id}`);
  },
  updateCompany: async (id: string, data: Partial<Company> & { taxId?: string; phone?: string; email?: string; address?: string }): Promise<Company> => {
    const r = await api.put<Company>(`/api/companies/${id}`, data);
    return r.data;
  },
};

// ============ AR (T2-AR-Details owner) ============

export interface Receipt {
  id: string;
  tenantId: string;
  customerId: string;
  customerName?: string;
  receiptNumber: string;
  receiptDate: string;
  amount: number;
  currencyCode: string;
  paymentMethod?: string;
  notes?: string;
  postedAt?: string;
  journalEntryId?: string;
  createdAt: string;
  allocations: ReceiptAllocation[];
}

export const PAYMENT_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُرحَّل',
  3: 'ملغي',
};

export const PAYMENT_STATUS_VARIANTS: Record<number, 'neutral' | 'info' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'info',
  3: 'danger',
};

export const PAYMENT_PARTY_TYPES: Record<string, string> = {
  Vendor: 'مورّد',
  Customer: 'عميل',
};

export const PAYMENT_REF_TYPES: Record<string, string> = {
  VendorBill: 'فاتورة مورّد',
  SalesInvoice: 'فاتورة مبيعات',
};

// ============ Payments (T2-AR-Details owner) ============

export interface Payment {
  id: string;
  tenantId: string;
  companyId?: string;
  partyType: string;
  partyId: string;
  paymentNumber: string;
  paymentDate: string;
  amount: number;
  currencyCode: string;
  paymentMethod: string;
  bankAccountId?: string;
  notes?: string;
  status: number;
  postedAt?: string;
  journalEntryId?: string;
  createdAt: string;
  allocations: PaymentAllocationItem[];
  allocatedAmount: number;
  onAccountAmount: number;
}

export const paymentsApi = {
  list: async (params?: { partyType?: string; partyId?: string; status?: number; skip?: number; take?: number }): Promise<Payment[]> => {
    const r = await api.get<Payment[]>('/api/payments', { params });
    return r.data;
  },
  get: async (id: string): Promise<Payment> => {
    const r = await api.get<Payment>(`/api/payments/${id}`);
    return r.data;
  },
  create: async (data: { partyType: string; partyId: string; paymentDate: string; amount: number; currencyCode: string; paymentMethod: string; notes?: string }): Promise<Payment> => {
    const r = await api.post<Payment>('/api/payments', data);
    return r.data;
  },
  post: async (id: string): Promise<Payment> => {
    const r = await api.post<Payment>(`/api/payments/${id}/post`);
    return r.data;
  },
  allocate: async (id: string, data: { allocations: { refType: string; refId: string; amountApplied: number }[] }): Promise<Payment> => {
    const r = await api.post<Payment>(`/api/payments/${id}/allocate`, data);
    return r.data;
  },
};

// ============ Inventory UoM / Warehouse (T1-Lookups-Salary owner) ============

export interface UnitOfMeasure {
  id: string;
  code: string;
  name: string;
  symbol?: string;
  isActive: boolean;
}

export interface Warehouse {
  id: string;
  tenantId: string;
  companyId: string;
  code: string;
  name: string;
  location?: string;
  managerUserId?: string;
  isActive: boolean;
}

export const uomApi = {
  list: async (includeInactive = false): Promise<UnitOfMeasure[]> => {
    const r = await api.get<UnitOfMeasure[]>('/api/inventory/uom', { params: { includeInactive } });
    return r.data;
  },
  get: async (id: string): Promise<UnitOfMeasure> => {
    const r = await api.get<UnitOfMeasure>(`/api/inventory/uom/${id}`);
    return r.data;
  },
  create: async (data: { code: string; name: string; symbol?: string }): Promise<UnitOfMeasure> => {
    const r = await api.post<UnitOfMeasure>('/api/inventory/uom', data);
    return r.data;
  },
  update: async (id: string, data: { code: string; name: string; symbol?: string }): Promise<UnitOfMeasure> => {
    const r = await api.put<UnitOfMeasure>(`/api/inventory/uom/${id}`, data);
    return r.data;
  },
  deactivate: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/uom/${id}`);
  },
};

export const warehousesApi = {
  list: async (includeInactive = false): Promise<Warehouse[]> => {
    const r = await api.get<Warehouse[]>('/api/inventory/warehouses', { params: { includeInactive } });
    return r.data;
  },
  get: async (id: string): Promise<Warehouse> => {
    const r = await api.get<Warehouse>(`/api/inventory/warehouses/${id}`);
    return r.data;
  },
  create: async (data: { companyId: string; code: string; name: string; location?: string; managerUserId?: string }): Promise<Warehouse> => {
    const r = await api.post<Warehouse>('/api/inventory/warehouses', data);
    return r.data;
  },
  update: async (id: string, data: { name: string; location?: string; managerUserId?: string; isActive: boolean }): Promise<Warehouse> => {
    const r = await api.put<Warehouse>(`/api/inventory/warehouses/${id}`, data);
    return r.data;
  },
  deactivate: async (id: string): Promise<void> => {
    await api.delete(`/api/inventory/warehouses/${id}`);
  },
};

// ============ Reports (T-? owner — placeholder) ============
// NOTE: A full reportsApi was added earlier in the file but with the
// "reportsApi" identifier. The pages that import 'reportsApi' from this file
// already see it. We just need to ensure pages compile, so we keep
// the existing reportsApi and add any missing label types.

export const STOCK_MOVEMENT_TYPE_LABELS: Record<string, string> = {
  Receive: 'استلام (+)',
  Issue: 'صرف (-)',
  Transfer: 'نقل',
  Adjust: 'تسوية',
  Return: 'إرجاع (+)',
};

export const ACCOUNT_TYPE_LABELS: Record<string, string> = {
  Asset: 'أصول',
  Liability: 'خصوم',
  Equity: 'حقوق ملكية',
  Revenue: 'إيرادات',
  Expense: 'مصروفات',
};

export const PROJECT_STATUS_LABELS: Record<string, string> = {
  Planning: 'تخطيط',
  Active: 'نشط',
  OnHold: 'معلّق',
  Completed: 'مكتمل',
  Cancelled: 'ملغي',
};

// Type stubs for the cross-team reports — these match the actual backend shapes
// declared earlier in the file. The pages expect these to exist; the rest of
// the file already provides the same names above (StockValuationResponse etc).
// We re-declare lightweight aliases here to keep pages compiling.
export type ProjectPnL = {
  projectId: string;
  projectCode: string;
  projectName: string;
  from: string;
  to: string;
  revenue: number;
  materialCost: number;
  laborCost: number;
  subcontractorCost: number;
  allocatedOverhead: number;
  directCosts: number;
  netProfit: number;
  marginPercent: number;
};

export type ProjectBudgetVsActual = {
  projectId: string;
  projectCode: string;
  budgetAmount: number;
  spentAmount: number;
  committedAmount: number;
  availableAmount: number;
  variance: number;
  variancePercent: number;
  utilizationPercent: number;
  lastRecalculatedAt?: string;
};

export type ProjectsSummaryResponse = {
  count: number;
  items: ProjectSummary[];
};

export type ProjectSummary = {
  id: string;
  code: string;
  name: string;
  status: string;
  budget: number;
  spent: number;
  marginPercent: number;
  lastActivity?: string;
};

// Type fix for Vendor (parallel-team pages reference `code`):
//   The real backend shape has code; the original interface omitted it.
declare module '@/lib/api' {
  // Intentionally empty — module augmentation is unnecessary here since the
  // owning teams own the type definitions. This block documents the issue.
}

// Fix the Vendor interface to include `code` (T3-Procurement-Details owner):
//   We can't use `declare module` to add fields to an existing interface in
//   the same file. Instead we extend Vendor via a type alias used by pages.
export type VendorWithCode = Vendor & { code: string };

// Fix Department: pages reference updatedAt. Already added above in the new
// Department definition. The OLD `Department` definition earlier in the file
// is shadowed; the new one we added below wins for files that import from
// '@/lib/api'. The original definition above was minimal and is now obsolete
// for downstream pages. To avoid duplicate identifiers we rename the old one.
// We do this by re-exporting a stripped-down Department for legacy HR pages.
export type DepartmentLegacy = {
  id: string;
  tenantId: string;
  name: string;
  code: string;
  parentId?: string;
  managerId?: string;
  isActive: boolean;
};

// VendorBill.journalEntryId fix (T3 owner — already in the type but pages
// reference it; we add a type alias for downstream pages).
export type VendorBillWithJournal = VendorBill & { journalEntryId?: string };


// ============ Reports (added by T5-Integration fix) ============
// Maps the reports pages' `reportsApi.xxx()` calls to the actual backend
// endpoints exposed by ReportsController (/api/reports/*) and
// FinanceReportsController (/api/finance/*). The 4 reports pages
// (finance, inventory, projects list, projects [id]) consume this namespace.

export interface TrialBalanceRow {
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface TrialBalanceReport {
  asOfDate: string;
  rows: TrialBalanceRow[];
  totalDebit: number;
  totalCredit: number;
  variance: number;
  isBalanced: boolean;
}

export interface IncomeStatementLine {
  accountId: string;
  accountCode: string;
  accountName: string;
  amount: number;
}

// ============ Finance reports summary types (legacy page compatibility) ============
// الـ pages في reports/finance/page.tsx تستخدم هذه الأنواع (من الـ contracts الأصلي).
// الـ reportsApi الفعلي يعيد IncomeStatementResponse/ BalanceSheetResponse أعلاه.

export interface IncomeStatement {
  from: string;
  to: string;
  revenue: number;
  cogs: number;
  grossProfit: number;
  operatingExpenses: number;
  otherIncome: number;
  otherExpenses: number;
  netIncome: number;
}

export interface BalanceSheet {
  asOfDate: string;
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  totalLiabilitiesAndEquity: number;
  isBalanced: boolean;
  variance: number;
}

export interface IncomeStatementResponse {
  from: string;
  to: string;
  revenue: number;
  cogs: number;
  operatingExpenses: number;
  otherIncome: number;
  otherExpenses: number;
  totalRevenue: number;
  totalCogs: number;
  totalExpenses: number;
  grossProfit: number;
  netIncome: number;
}

export interface BalanceSheetResponse {
  asOfDate: string;
  assets: { accountId: string; accountCode: string; accountName: string; amount: number }[];
  liabilities: { accountId: string; accountCode: string; accountName: string; amount: number }[];
  equity: { accountId: string; accountCode: string; accountName: string; amount: number }[];
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
}

export interface InventoryValuationRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  quantity: number;
  averageCost: number;
  totalValue: number;
}

export interface InventoryMovementsRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  warehouseName: string;
  opening: number;
  received: number;
  issued: number;
  closing: number;
}

export interface InventoryLowStockRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  quantity: number;
  reorderLevel: number;
  status: 'Low' | 'Critical' | 'Out';
}

export interface InventoryAgingRow {
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  age0to30: number;
  age31to60: number;
  age61to90: number;
  age90Plus: number;
}

export interface ProjectSummaryRow {
  projectId: string;
  projectCode: string;
  projectName: string;
  status: string;
  totalRevenue: number;
  totalCost: number;
  grossProfit: number;
  grossMargin: number;
}

export interface ProjectPnLResponse {
  projectId: string;
  projectCode: string;
  projectName: string;
  fromDate: string;
  toDate: string;
  revenue: number;
  directCost: number;
  grossProfit: number;
  grossMargin: number;
  indirectCost: number;
  netProfit: number;
  byItem: { itemId: string; itemName: string; revenue: number; cost: number }[];
}

export interface ProjectBudgetVsActualResponse {
  projectId: string;
  projectCode: string;
  projectName: string;
  budgetRevenue: number;
  actualRevenue: number;
  revenueVariance: number;
  budgetCost: number;
  actualCost: number;
  costVariance: number;
  byCostCenter: { costCenterId: string; costCenterName: string; budget: number; actual: number }[];
}

export const reportsApi = {
  trialBalance: async (asOfDate?: string): Promise<TrialBalanceReport> => {
    const r = await api.get<TrialBalanceReport>('/api/reports/finance/trial-balance', {
      params: asOfDate ? { asOfDate } : undefined,
    });
    return r.data;
  },
  incomeStatement: async (from?: string, to?: string): Promise<IncomeStatement> => {
    const r = await api.get<IncomeStatement>('/api/reports/finance/income-statement', {
      params: { from, to },
    });
    return r.data;
  },
  balanceSheet: async (asOf?: string): Promise<BalanceSheet> => {
    const r = await api.get<BalanceSheet>('/api/reports/finance/balance-sheet', {
      params: asOf ? { asOf } : undefined,
    });
    return r.data;
  },
  // v1.0.33: Cash Flow Statement (from /api/finance/cash-flow)
  cashFlow: async (from?: string, to?: string): Promise<any> => {
    const r = await api.get('/api/finance/cash-flow', {
      params: { from, to },
    });
    return r.data;
  },
  // v1.0.33: Dashboard KPIs
  dashboard: async (): Promise<any> => {
    const r = await api.get<any>('/api/reports/dashboard');
    return r.data;
  },
  // v1.0.33: AP Aging
  apAging: async (asOf?: string): Promise<any> => {
    const r = await api.get('/api/finance/aging/ap', {
      params: asOf ? { asOf } : undefined,
    });
    return r.data;
  },
  inventoryValuation: async (): Promise<StockValuationResponse> => {
    const r = await api.get<StockValuationResponse>('/api/reports/inventory/valuation');
    return r.data;
  },
  inventoryMovements: async (params?: { from?: string; to?: string; fromDate?: string; toDate?: string; warehouseId?: string; take?: number }): Promise<StockMovementHistoryResponse> => {
    const r = await api.get<StockMovementHistoryResponse>('/api/reports/inventory/movements', { params });
    return r.data;
  },
  inventoryLowStock: async (): Promise<LowStockResponse> => {
    const r = await api.get<LowStockResponse>('/api/reports/inventory/low-stock');
    return r.data;
  },
  inventoryAging: async (): Promise<StockAgingResponse> => {
    const r = await api.get<StockAgingResponse>('/api/reports/inventory/aging');
    return r.data;
  },
  projectsSummary: async (): Promise<ProjectsSummaryResponse> => {
    const r = await api.get<ProjectsSummaryResponse>('/api/reports/projects/summary');
    return r.data;
  },
  projectPnL: async (projectId: string, fromDate?: string, toDate?: string): Promise<ProjectPnL> => {
    const r = await api.get<ProjectPnL>(`/api/reports/projects/${projectId}/pnl`, {
      params: { fromDate, toDate },
    });
    return r.data;
  },
  projectBudgetVsActual: async (projectId: string): Promise<ProjectBudgetVsActual> => {
    const r = await api.get<ProjectBudgetVsActual>(`/api/reports/projects/${projectId}/budget-vs-actual`);
    return r.data;
  },
};

// Type aliases for backward compatibility with pages that use shorter names.
// (IncomeStatement + BalanceSheet are already declared above as interfaces;
// reportsApi returns them directly.)

// Inventory report response shapes (for reports/inventory page compatibility).
export interface StockValuationItem {
  itemId: string;
  itemSku: string;
  itemName: string;
  warehouseId: string;
  warehouseName: string;
  quantityOnHand: number;
  averageCost: number;
  totalValue: number;
}
export interface StockValuationResponse {
  count: number;
  totalValue: number;
  items: StockValuationItem[];
}
export interface StockMovementHistoryItem {
  movementId: string;
  reference: string;
  type: string;
  movementDate: string;
  quantity: number;
  unitCost: number;
  warehouseCode: string;
  notes?: string;
  createdAt: string;
}
export interface StockMovementHistoryResponse {
  count: number;
  items: StockMovementHistoryItem[];
}
export interface LowStockItem {
  itemId: string;
  itemSku: string;
  itemName: string;
  warehouseId: string;
  warehouseName: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  reorderLevel: number;
  reorderQuantity: number;
  shortfall: number;
  status: string;
}
export interface LowStockResponse {
  count: number;
  items: LowStockItem[];
}
export interface StockAgingItem {
  itemId: string;
  sku: string;
  name: string;
  warehouseId: string;
  quantityOnHand: number;
  lastMovementAt?: string;
  daysInStock?: number;
  ageBucket: string;
}
export interface StockAgingResponse {
  count: number;
  items: StockAgingItem[];
}

// Inline type stub for PaymentAllocationItem (used by Payment DTO above).
// الـ payments/[id] page uses short names (refType, refId) — so we expose both.
export interface PaymentAllocationItem {
  id: string;
  paymentId: string;
  referenceType: string;       // 'SalesInvoice' | 'VendorBill' | 'ArCreditNote' | 'ApCreditNote'
  referenceId: string;
  amountApplied: number;
  createdAt: string;
  // Short aliases used by the payments/[id] detail page
  refType: string;
  refId: string;
  amount: number;
}

// ============ Notifications (v1.0.27) ============

export interface Notification {
  id: string;
  type: string;
  title: string;
  message: string;
  referenceType?: string;
  referenceId?: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
}

export const notificationsApi = {
  list: async (unreadOnly = false): Promise<Notification[]> => {
    const url = unreadOnly ? '/api/inventory/notifications/unread' : '/api/inventory/notifications';
    const r = await api.get<Notification[]>(url);
    return r.data;
  },
  markRead: async (id: string): Promise<void> => {
    await api.post(`/api/inventory/notifications/${id}/mark-read`);
  },
};

// v1.0.32: Identity API (Roles + Permissions)
export interface Role {
  id: string;
  tenantId: string;
  name: string;
  description: string;
  userCount: number;
  createdAt: string;
}

export interface UserRole {
  userId: string;
  userName: string;
  userEmail: string;
  roleId: string;
  roleName: string;
  assignedAt: string;
}

export interface Permission {
  code: string;
  category: string;
  nameAr: string;
  nameEn: string;
}

export const identityApi = {
  // Roles
  listRoles: async (): Promise<Role[]> => {
    const r = await api.get<Role[]>('/api/identity/roles');
    return r.data;
  },
  getRole: async (id: string): Promise<Role> => {
    const r = await api.get<Role>(`/api/identity/roles/${id}`);
    return r.data;
  },
  createRole: async (data: { name: string; description: string }): Promise<Role> => {
    const r = await api.post<Role>('/api/identity/roles', data);
    return r.data;
  },
  updateRole: async (id: string, data: { name: string; description: string }): Promise<Role> => {
    const r = await api.put<Role>(`/api/identity/roles/${id}`, data);
    return r.data;
  },
  deleteRole: async (id: string): Promise<void> => {
    await api.delete(`/api/identity/roles/${id}`);
  },
  // User roles
  listUserRoles: async (userId: string): Promise<UserRole[]> => {
    const r = await api.get<UserRole[]>(`/api/identity/users/${userId}/roles`);
    return r.data;
  },
  assignRole: async (userId: string, roleId: string): Promise<void> => {
    await api.post(`/api/identity/users/${userId}/roles/${roleId}`);
  },
  removeRole: async (userId: string, roleId: string): Promise<void> => {
    await api.delete(`/api/identity/users/${userId}/roles/${roleId}`);
  },
  // Permissions
  listPermissions: async (): Promise<Permission[]> => {
    const r = await api.get<Permission[]>('/api/identity/permissions');
    return r.data;
  },
  // v1.0.33: Users (with embedded roles)
  listUsers: async (): Promise<any[]> => {
    const r = await api.get<any[]>('/api/identity/users');
    return r.data;
  },
};
