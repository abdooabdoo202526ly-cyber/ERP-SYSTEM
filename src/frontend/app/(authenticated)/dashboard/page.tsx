'use client';

// صفحة لوحة التحكم (Dashboard) — محسّنة بـ KPI tiles + Quick Actions + Recent Activity

import { useEffect, useState } from 'react';
import { formatDate, formatTime } from '@/lib/utils';
import Link from 'next/link';
import {
  Truck,
  FileText,
  UserCog,
  Boxes,
  Plus,
  Users,
  PackageCheck,
  Receipt,
  Activity,
  Wallet,
  Briefcase,
} from 'lucide-react';
import { Card, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  authApi,
  financeApi,
  inventoryApi,
  projectsApi,
  procurementApi,
  hrApi,
  reportsApi,
  arApi,
  Account,
  Item,
  Project,
  PurchaseOrder,
} from '@/lib/api';

interface RecentItem {
  id: string;
  title: string;
  subtitle: string;
  badge: string;
  date: string;
  href: string;
}

export default function DashboardPage() {
  const { user } = useAuth();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [vendorsCount, setVendorsCount] = useState(0);
  const [openPoCount, setOpenPoCount] = useState(0);
  const [employeesCount, setEmployeesCount] = useState(0);
  const [lowStockCount, setLowStockCount] = useState(0);
  const [recent, setRecent] = useState<RecentItem[]>([]);
  const [loading, setLoading] = useState(true);
  // v1.0.33: financial KPIs
  const [ytdRevenue, setYtdRevenue] = useState(0);
  const [ytdNetIncome, setYtdNetIncome] = useState(0);
  const [totalAssets, setTotalAssets] = useState(0);
  const [arOutstanding, setArOutstanding] = useState(0);

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [accRes, invRes, projRes, venRes, poRes, empRes, ytdRes, bsRes, arRes] = await Promise.allSettled([
        financeApi.listAccounts(),
        inventoryApi.listItems(),
        projectsApi.listProjects(),
        // Fixed: use typed procurementApi (goes to localhost:5000, not localhost:3000)
        procurementApi.listVendors(),
        procurementApi.listPOs(),
        hrApi.listEmployees(),
        // v1.0.33: financial KPIs
        (() => {
          const today = new Date();
          const yearStart = new Date(today.getFullYear(), 0, 1).toISOString();
          const todayStr = today.toISOString();
          return reportsApi.incomeStatement(yearStart, todayStr);
        })(),
        reportsApi.balanceSheet(new Date().toISOString()),
        arApi.aging(new Date().toISOString()).catch(() => null),
      ]);

      if (accRes.status === 'fulfilled') setAccounts(accRes.value);
      if (invRes.status === 'fulfilled') {
        setItems(invRes.value);
        setLowStockCount(
          invRes.value.filter((i) => (i.averageCost ?? 0) >= 0 && i.reorderLevel > 0).length
        );
      }
      if (projRes.status === 'fulfilled') setProjects(projRes.value);

      if (venRes.status === 'fulfilled' && Array.isArray(venRes.value)) {
        setVendorsCount(venRes.value.length ?? 0);
      }
      if (poRes.status === 'fulfilled' && Array.isArray(poRes.value)) {
        // PO Status: Draft=1, Pending=2, Approved=3, Sent=4, Received=5, Cancelled=6
        const open = poRes.value.filter(
          (p: PurchaseOrder) => p.status !== 5 && p.status !== 6
        );
        setOpenPoCount(open.length ?? 0);
        // آخر 5 POs كـ recent activity
        const recentPOs: RecentItem[] = poRes.value
          .slice(-5)
          .reverse()
          .map((p: PurchaseOrder) => ({
            id: p.id,
            title: p.poNumber,
            subtitle: p.vendorName || p.vendorId,
            badge: String(p.status),  // status is number (enum: 1=Draft, 2=Pending, etc.)
            date: p.createdAt,
            href: '/procurement/purchase-orders',
          }));
        setRecent((prev) => [...recentPOs, ...prev].slice(0, 5));
      }
      if (empRes.status === 'fulfilled' && Array.isArray(empRes.value)) {
        setEmployeesCount(empRes.value.length ?? 0);
      }

      // v1.0.33: Financial KPIs
      if (ytdRes.status === 'fulfilled' && ytdRes.value) {
        const r: any = ytdRes.value;
        setYtdRevenue(Number(r.totalRevenue ?? r.revenue ?? 0));
        setYtdNetIncome(Number(r.netIncome ?? 0));
      }
      if (bsRes.status === 'fulfilled' && bsRes.value) {
        setTotalAssets(Number(bsRes.value.totalAssets ?? 0));
      }
      if (arRes.status === 'fulfilled' && arRes.value) {
        // arApi.aging يرجع { total, customers[] } أو [buckets]
        const r: any = arRes.value;
        const total = r.total ?? (Array.isArray(r) ? r.reduce((s: number, b: any) => s + Number(b.amount ?? 0), 0) : 0);
        setArOutstanding(Number(total));
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      {/* Header ترحيبي */}
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-800">
          مرحباً، {user?.fullName?.split(' ')[0] || 'مستخدم'} 👋
        </h1>
        <p className="text-sm text-gray-500 mt-1">نظرة عامة على نشاط الشركة</p>
      </div>

      {/* KPI Tiles */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <Link href="/procurement/vendors" className="block">
          <Card accent="blue" className="hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">إجمالي الموردين</p>
                <p className="text-3xl font-bold text-gray-800 mt-1">{vendorsCount}</p>
                <p className="text-xs text-gray-400 mt-1">مورّد مُسجَّل</p>
              </div>
              <div className="h-12 w-12 rounded-lg bg-blue-50 flex items-center justify-center">
                <Truck className="h-6 w-6 text-blue-600" />
              </div>
            </div>
          </Card>
        </Link>

        <Link href="/procurement/purchase-orders" className="block">
          <Card accent="yellow" className="hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">أوامر شراء مفتوحة</p>
                <p className="text-3xl font-bold text-gray-800 mt-1">{openPoCount}</p>
                <p className="text-xs text-gray-400 mt-1">قيد التنفيذ</p>
              </div>
              <div className="h-12 w-12 rounded-lg bg-yellow-50 flex items-center justify-center">
                <FileText className="h-6 w-6 text-yellow-600" />
              </div>
            </div>
          </Card>
        </Link>

        <Link href="/hr/employees" className="block">
          <Card accent="green" className="hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-500">موظفين نشطين</p>
                <p className="text-3xl font-bold text-gray-800 mt-1">{employeesCount}</p>
                <p className="text-xs text-gray-400 mt-1">في الشركة</p>
              </div>
              <div className="h-12 w-12 rounded-lg bg-green-50 flex items-center justify-center">
                <UserCog className="h-6 w-6 text-green-600" />
              </div>
            </div>
          </Card>
        </Link>

        <Card accent="red">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm text-gray-500">أصناف منخفضة المخزون</p>
              <p className="text-3xl font-bold text-gray-800 mt-1">{lowStockCount}</p>
              <p className="text-xs text-gray-400 mt-1">تحت حد الطلب</p>
            </div>
            <div className="h-12 w-12 rounded-lg bg-red-50 flex items-center justify-center">
              <Boxes className="h-6 w-6 text-red-600" />
            </div>
          </div>
        </Card>
      </div>

      {/* Quick Actions + Recent Activity */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-6">
        <Card title="إجراءات سريعة" description="إنشاء سريع" className="lg:col-span-1">
          <div className="grid grid-cols-2 gap-2">
            <Link
              href="/procurement/purchase-orders/new"
              className="flex flex-col items-center gap-1 p-3 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-colors"
            >
              <Plus className="h-5 w-5 text-blue-600" />
              <span className="text-xs font-semibold text-gray-700">أمر شراء</span>
            </Link>
            <Link
              href="/procurement/vendors/new"
              className="flex flex-col items-center gap-1 p-3 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-colors"
            >
              <Truck className="h-5 w-5 text-blue-600" />
              <span className="text-xs font-semibold text-gray-700">مورّد</span>
            </Link>
            <Link
              href="/hr/employees/new"
              className="flex flex-col items-center gap-1 p-3 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-colors"
            >
              <UserCog className="h-5 w-5 text-blue-600" />
              <span className="text-xs font-semibold text-gray-700">موظف</span>
            </Link>
            <Link
              href="/procurement/goods-receipts/new"
              className="flex flex-col items-center gap-1 p-3 rounded-lg border border-gray-200 hover:border-blue-300 hover:bg-blue-50 transition-colors"
            >
              <PackageCheck className="h-5 w-5 text-blue-600" />
              <span className="text-xs font-semibold text-gray-700">استلام</span>
            </Link>
          </div>
        </Card>

        <Card title="آخر النشاطات" description="آخر 5 أوامر شراء" className="lg:col-span-2">
          {loading ? (
            <div className="py-8 text-center text-sm text-gray-500">جاري التحميل...</div>
          ) : recent.length === 0 ? (
            <div className="py-8 text-center text-sm text-gray-500">
              <Activity className="h-8 w-8 mx-auto mb-2 text-gray-300" />
              لا توجد نشاطات بعد
            </div>
          ) : (
            <ul className="divide-y divide-gray-100">
              {recent.map((r) => (
                <li key={r.id} className="py-3 flex items-center justify-between gap-3">
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-gray-800 truncate">{r.title}</p>
                    <p className="text-xs text-gray-500 truncate">{r.subtitle}</p>
                  </div>
                  <div className="flex items-center gap-2 flex-shrink-0">
                    <Badge variant="info">{r.badge}</Badge>
                    <span className="text-[10px] text-gray-400">
                      {formatDate(r.date)}
                    </span>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* Stats للوحدات الموجودة */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Link href="/finance/accounts" className="block">
          <Card accent="purple" className="hover:shadow-md transition-shadow">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-purple-50 flex items-center justify-center">
                <Wallet className="h-5 w-5 text-purple-600" />
              </div>
              <div>
                <p className="text-sm text-gray-500">الحسابات المحاسبية</p>
                <p className="text-2xl font-bold text-gray-800">{accounts.length}</p>
              </div>
            </div>
          </Card>
        </Link>
        <Link href="/inventory/items" className="block">
          <Card accent="green" className="hover:shadow-md transition-shadow">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-green-50 flex items-center justify-center">
                <Boxes className="h-5 w-5 text-green-600" />
              </div>
              <div>
                <p className="text-sm text-gray-500">الأصناف</p>
                <p className="text-2xl font-bold text-gray-800">{items.length}</p>
              </div>
            </div>
          </Card>
        </Link>
        <Link href="/projects" className="block">
          <Card accent="blue" className="hover:shadow-md transition-shadow">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-blue-50 flex items-center justify-center">
                <Briefcase className="h-5 w-5 text-blue-600" />
              </div>
              <div>
                <p className="text-sm text-gray-500">المشاريع</p>
                <p className="text-2xl font-bold text-gray-800">{projects.length}</p>
              </div>
            </div>
          </Card>
        </Link>
      </div>

      {/* v1.0.33: Financial KPIs + Quick Reports */}
      <div className="mt-6">
        <h2 className="text-lg font-bold text-gray-800 mb-3">📈 المؤشرات المالية</h2>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-4">
          <Link href="/finance/reports/income-statement" className="block">
            <Card className="hover:shadow-md transition-shadow bg-gradient-to-br from-emerald-50 to-emerald-100 border-emerald-300">
              <p className="text-xs text-emerald-700">الإيرادات (السنة)</p>
              <p className="text-2xl font-bold font-mono text-emerald-900">{ytdRevenue.toLocaleString()}</p>
              <p className="text-[10px] text-emerald-600 mt-1">LYD</p>
            </Card>
          </Link>
          <Link href="/finance/reports/income-statement" className="block">
            <Card className="hover:shadow-md transition-shadow bg-gradient-to-br from-blue-50 to-blue-100 border-blue-300">
              <p className="text-xs text-blue-700">صافي الدخل (السنة)</p>
              <p className="text-2xl font-bold font-mono text-blue-900">{ytdNetIncome.toLocaleString()}</p>
              <p className="text-[10px] text-blue-600 mt-1">LYD</p>
            </Card>
          </Link>
          <Link href="/finance/reports/balance-sheet" className="block">
            <Card className="hover:shadow-md transition-shadow bg-gradient-to-br from-purple-50 to-purple-100 border-purple-300">
              <p className="text-xs text-purple-700">إجمالي الأصول</p>
              <p className="text-2xl font-bold font-mono text-purple-900">{totalAssets.toLocaleString()}</p>
              <p className="text-[10px] text-purple-600 mt-1">LYD</p>
            </Card>
          </Link>
          <Link href="/finance/reports/balance-sheet" className="block">
            <Card className="hover:shadow-md transition-shadow bg-gradient-to-br from-amber-50 to-amber-100 border-amber-300">
              <p className="text-xs text-amber-700">إجمالي الذمم المدينة</p>
              <p className="text-2xl font-bold font-mono text-amber-900">{arOutstanding.toLocaleString()}</p>
              <p className="text-[10px] text-amber-600 mt-1">LYD</p>
            </Card>
          </Link>
        </div>
        <div className="flex flex-wrap gap-2">
          <Link href="/finance/reports/income-statement">
            <span className="inline-block bg-emerald-100 text-emerald-800 px-3 py-1 rounded-full text-xs font-semibold hover:bg-emerald-200 cursor-pointer">📊 P&L</span>
          </Link>
          <Link href="/finance/reports/balance-sheet">
            <span className="inline-block bg-blue-100 text-blue-800 px-3 py-1 rounded-full text-xs font-semibold hover:bg-blue-200 cursor-pointer">📈 الميزانية</span>
          </Link>
          <Link href="/finance/reports/cash-flow">
            <span className="inline-block bg-purple-100 text-purple-800 px-3 py-1 rounded-full text-xs font-semibold hover:bg-purple-200 cursor-pointer">💰 التدفقات النقدية</span>
          </Link>
          <Link href="/finance/reports/trial-balance">
            <span className="inline-block bg-amber-100 text-amber-800 px-3 py-1 rounded-full text-xs font-semibold hover:bg-amber-200 cursor-pointer">📉 ميزان المراجعة</span>
          </Link>
          <Link href="/finance/aging-ar">
            <span className="inline-block bg-red-100 text-red-800 px-3 py-1 rounded-full text-xs font-semibold hover:bg-red-200 cursor-pointer">⏳ أعمار الذمم</span>
          </Link>
        </div>
      </div>
    </div>
  );
}


