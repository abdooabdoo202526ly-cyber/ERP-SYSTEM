'use client';

// v1.0.34: صفحة إنشاء أمر شراء (PO) مع Cost Center + Project

import { useEffect, useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Plus as PlusIcon, Trash2, Briefcase, DollarSign } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  inventoryApi,
  projectsApi,
  Vendor,
  Item,
  PurchaseOrder,
  PurchaseOrderLine,
  Project,
  CostCenterLite,
  getErrorMessage,
} from '@/lib/api';

interface LineDraft {
  itemId: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export default function NewPurchaseOrderPage() {
  const router = useRouter();
  useAuth();
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [projects, setProjects] = useState<Project[]>([]);
  const [costCenters, setCostCenters] = useState<CostCenterLite[]>([]);
  const [vendorId, setVendorId] = useState('');
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10));
  const [expectedDate, setExpectedDate] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [notes, setNotes] = useState('');
  // v1.0.34: cost center & project
  const [projectId, setProjectId] = useState('');
  const [costCenterId, setCostCenterId] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([{ itemId: '', quantity: 1, unitPrice: 0, taxRate: 0 }]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loadingLookups, setLoadingLookups] = useState(true);

  useEffect(() => {
    loadLookups();
  }, []);

  const loadLookups = async () => {
    setLoadingLookups(true);
    try {
      const [v, i, p, cc] = await Promise.allSettled([
        procurementApi.listVendors(),
        inventoryApi.listItems(),
        projectsApi.listProjects(),
        projectsApi.listCostCenters(),
      ]);
      if (v.status === 'fulfilled') setVendors(v.value);
      if (i.status === 'fulfilled') setItems(i.value);
      if (p.status === 'fulfilled') setProjects(p.value);
      if (cc.status === 'fulfilled') setCostCenters(cc.value);
    } finally {
      setLoadingLookups(false);
    }
  };

  // عند اختيار مشروع → اشتقاق مركز التكلفة تلقائياً
  useEffect(() => {
    if (!projectId) return;
    const proj = projects.find((p) => p.id === projectId);
    if (proj && proj.costCenterId && !costCenterId) {
      setCostCenterId(proj.costCenterId);
    }
  }, [projectId, projects, costCenterId]);

  const vendorOptions = useMemo(
    () => vendors.map((v) => ({ label: v.name, value: v.id })),
    [vendors]
  );
  const itemOptions = useMemo(
    () => items.map((i) => ({ label: `${i.sku} — ${i.name}`, value: i.id })),
    [items]
  );
  const projectOptions = useMemo(
    () => [
      { label: '— بدون مشروع —', value: '' },
      ...projects.map((p) => ({ label: `${p.code} — ${p.name}`, value: p.id })),
    ],
    [projects]
  );
  const costCenterOptions = useMemo(
    () => costCenters.map((c) => ({ label: `${c.code} — ${c.name}`, value: c.id })),
    [costCenters]
  );

  const updateLine = (idx: number, key: keyof LineDraft, value: string | number) => {
    setLines((prev) =>
      prev.map((l, i) => (i === idx ? { ...l, [key]: typeof value === 'string' ? (key === 'itemId' ? value : Number(value)) : value } : l))
    );
  };

  const addLine = () => setLines((prev) => [...prev, { itemId: '', quantity: 1, unitPrice: 0, taxRate: 0 }]);
  const removeLine = (idx: number) => setLines((prev) => prev.filter((_, i) => i !== idx));

  const subTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const taxTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice * (l.taxRate / 100), 0);
  const grandTotal = subTotal + taxTotal;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!vendorId) {
      setError('يجب اختيار المورّد.');
      return;
    }
    if (!costCenterId && !projectId) {
      setError('يجب تحديد مركز التكلفة (أو اختيار مشروع ليُشتق منه).');
      return;
    }
    if (lines.length === 0 || lines.some((l) => !l.itemId || l.quantity <= 0)) {
      setError('كل بند يجب أن يحتوي على صنف وكمية صحيحة.');
      return;
    }

    setSubmitting(true);
    try {
      const linesDto: PurchaseOrderLine[] = lines.map((l, i) => ({
        id: `temp-${i}`,
        itemId: l.itemId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxRate: l.taxRate,
        subTotal: l.quantity * l.unitPrice,
      }));
      await procurementApi.createPO({
        vendorId,
        orderDate,
        expectedDate: expectedDate || undefined,
        currency,
        notes: notes || undefined,
        projectId: projectId || undefined,
        costCenterId: costCenterId || undefined,
        lines: linesDto,
      } as Partial<PurchaseOrder> & { vendorId: string; lines: PurchaseOrderLine[] });
      router.push('/procurement/purchase-orders');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء أمر الشراء.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ أمر شراء جديد"
        description="أنشئ Purchase Order جديد"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'أوامر الشراء', href: '/procurement/purchase-orders' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/procurement/purchase-orders">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4 max-w-4xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error}</div>
        )}

        <Card title="معلومات الأمر">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="المورّد *"
              value={vendorId}
              onChange={(e) => setVendorId(e.target.value)}
              options={vendorOptions}
              placeholder={loadingLookups ? 'جاري التحميل...' : 'اختر المورّد'}
              required
            />
            <Input
              label="العملة"
              value={currency}
              onChange={(e) => setCurrency(e.target.value)}
              maxLength={3}
            />
            <Input
              type="date"
              label="تاريخ الطلب"
              value={orderDate}
              onChange={(e) => setOrderDate(e.target.value)}
              required
            />
            <Input
              type="date"
              label="تاريخ التوصيل المتوقع"
              value={expectedDate}
              onChange={(e) => setExpectedDate(e.target.value)}
            />
          </div>
          <div className="mt-4">
            <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظات</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
            />
          </div>
        </Card>

        {/* v1.0.34: Cost Center + Project */}
        <Card
          title={
            <div className="flex items-center gap-2">
              <DollarSign className="h-4 w-4 text-emerald-600" />
              <span>مركز التكلفة والمشروع</span>
              <Badge variant="info">مطلوب</Badge>
            </div>
          }
          description="حدد المشروع (اختياري) أو مركز التكلفة مباشرةً. سيتم اشتقاق مركز التكلفة تلقائياً من المشروع."
        >
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="المشروع (اختياري)"
              value={projectId}
              onChange={(e) => setProjectId(e.target.value)}
              options={projectOptions}
              placeholder="اختر مشروعاً (سيُشتق منه مركز التكلفة)"
            />
            <Select
              label="مركز التكلفة *"
              value={costCenterId}
              onChange={(e) => setCostCenterId(e.target.value)}
              options={costCenterOptions}
              placeholder={projectId ? 'سيُحدّد تلقائياً من المشروع' : 'اختر مركز التكلفة'}
              required
            />
          </div>
          {projectId && !costCenterId && (
            <div className="mt-3 text-xs text-amber-700 bg-amber-50 border border-amber-200 rounded p-2">
              💡 اختر مركز التكلفة يدوياً، أو اختر مشروعاً ليتم اشتقاقه تلقائياً
            </div>
          )}
        </Card>

        <Card
          title="بنود الأمر (Lines)"
          actions={
            <Button
              type="button"
              variant="outline"
              size="sm"
              iconLeft={<PlusIcon className="h-3 w-3" />}
              onClick={addLine}
            >
              إضافة بند
            </Button>
          }
        >
          <div className="space-y-3">
            {lines.map((line, idx) => (
              <div
                key={idx}
                className="grid grid-cols-12 gap-2 items-end p-3 bg-gray-50 rounded-lg"
              >
                <div className="col-span-12 md:col-span-5">
                  <Select
                    label={idx === 0 ? 'الصنف' : undefined}
                    value={line.itemId}
                    onChange={(e) => updateLine(idx, 'itemId', e.target.value)}
                    options={itemOptions}
                    placeholder="اختر الصنف"
                    required
                  />
                </div>
                <div className="col-span-6 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'الكمية' : undefined}
                    type="number"
                    min="0.01"
                    step="0.01"
                    value={line.quantity}
                    onChange={(e) => updateLine(idx, 'quantity', e.target.value)}
                    required
                  />
                </div>
                <div className="col-span-6 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'سعر الوحدة' : undefined}
                    type="number"
                    min="0"
                    step="0.01"
                    value={line.unitPrice}
                    onChange={(e) => updateLine(idx, 'unitPrice', e.target.value)}
                    required
                  />
                </div>
                <div className="col-span-6 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'الضريبة %' : undefined}
                    type="number"
                    min="0"
                    max="100"
                    step="0.01"
                    value={line.taxRate * 100}
                    onChange={(e) => updateLine(idx, 'taxRate', (Number(e.target.value) / 100))}
                  />
                </div>
                <div className="col-span-6 md:col-span-1">
                  {lines.length > 1 && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => removeLine(idx)}
                      iconLeft={<Trash2 className="h-3 w-3" />}
                    />
                  )}
                </div>
              </div>
            ))}
          </div>

          {/* Totals */}
          <div className="mt-4 p-3 bg-emerald-50 border border-emerald-200 rounded-lg">
            <div className="grid grid-cols-3 gap-4 text-sm">
              <div>
                <div className="text-gray-600">الإجمالي قبل الضريبة</div>
                <div className="font-bold text-lg font-mono">{subTotal.toFixed(2)}</div>
              </div>
              <div>
                <div className="text-gray-600">الضريبة</div>
                <div className="font-bold text-lg font-mono">{taxTotal.toFixed(2)}</div>
              </div>
              <div>
                <div className="text-gray-600">الإجمالي شامل الضريبة</div>
                <div className="font-bold text-xl font-mono text-emerald-700">{grandTotal.toFixed(2)} {currency}</div>
              </div>
            </div>
          </div>
        </Card>

        <div className="flex justify-end gap-2">
          <Link href="/procurement/purchase-orders">
            <Button type="button" variant="ghost">إلغاء</Button>
          </Link>
          <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
            حفظ كمسودة
          </Button>
        </div>
      </form>
    </div>
  );
}
