"use client";

import { useCallback, useEffect, useState } from "react";
import { packagePlanService, classTypeService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import type { PackagePlanDto, ClassTypeDto, PackagePlanInput } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Table, TableEmpty, TD, TH, THead, TR, Textarea, useConfirm, useToast } from "@/components/ui";

export default function PackagePlansPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const [items, setItems] = useState<PackagePlanDto[]>([]);
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<PackagePlanDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [plans, cts] = await Promise.all([packagePlanService.list(), classTypeService.list(true)]);
      setItems(plans);
      setClassTypes(cts);
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Planlar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const remove = async (p: PackagePlanDto) => {
    if (!(await confirm({ title: "Planı sil", tone: "danger", confirmLabel: "Sil", message: `"${p.name}" planı silinecek. Bu plana bağlı stüdyo/abonelik varsa işlem engellenebilir. Geri alınamaz.` }))) return;
    try {
      await packagePlanService.remove(p.id);
      await load();
      toast.success("Plan silindi.");
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Silinemedi.");
    }
  };

  const scopeLabel = (p: PackagePlanDto) => {
    if (p.allowAllClassTypes) return "Tüm gruplar";
    if (p.classTypeIds.length === 0) return "⚠ Seçilmemiş";
    const names = p.classTypeIds
      .map((id) => classTypes.find((c) => c.id === id)?.name)
      .filter(Boolean);
    return names.length ? names.join(", ") : `${p.classTypeIds.length} grup`;
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Paket Planları</h1>
          <p className="mt-1 text-sm text-muted">Öğrencilere satılacak paket şablonları — ders grubu kapsamı ve fiyat geçerlilikleri.</p>
        </div>
        <Button onClick={() => { setEditing(null); setFormOpen(true); }}>+ Yeni plan</Button>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead><TR><TH>Ad</TH><TH>Ders</TH><TH>Geçerli grup</TH><TH>Fiyat</TH><TH>Geçerlilik</TH><TH>Durum</TH><TH className="text-right">İşlem</TH></TR></THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={7}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={7}>Henüz paket planı yok.</TableEmpty>
          ) : (
            items.map((p) => (
              <TR key={p.id}>
                <TD className="font-medium">{p.name}</TD>
                <TD>{p.creditCount}</TD>
                <TD className="text-muted">{scopeLabel(p)}</TD>
                <TD>
                  {p.currentEffectivePrice.toLocaleString("tr-TR")} ₺
                  {p.nextEffectivePrice != null && p.nextEffectiveFrom && (
                    <span className="ml-1 block text-xs text-muted">
                      Yeni: {p.nextEffectivePrice.toLocaleString("tr-TR")} ₺ · {p.nextEffectiveFrom}
                    </span>
                  )}
                </TD>
                <TD className="text-muted">{p.validityDays ? `${p.validityDays} gün` : "Süresiz"}</TD>
                <TD><Badge tone={p.isActive ? "success" : "neutral"}>{p.isActive ? "Aktif" : "Pasif"}</Badge></TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => { setEditing(p); setFormOpen(true); }}>Düzenle</Button>
                    <Button variant="danger" size="sm" onClick={() => remove(p)}>Sil</Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      {formOpen && (
        <PlanModal
          plan={editing}
          classTypes={classTypes}
          onClose={() => setFormOpen(false)}
          onDone={() => { setFormOpen(false); load(); }}
        />
      )}
    </div>
  );
}

function PlanModal({ plan, classTypes, onClose, onDone }: {
  plan: PackagePlanDto | null;
  classTypes: ClassTypeDto[];
  onClose: () => void;
  onDone: () => void;
}) {
  const [form, setForm] = useState({
    name: plan?.name ?? "",
    description: plan?.description ?? "",
    creditCount: String(plan?.creditCount ?? 8),
    price: String(plan?.currentEffectivePrice ?? plan?.price ?? 0),
    validityDays: plan?.validityDays != null ? String(plan.validityDays) : "",
    isActive: plan?.isActive ?? true,
    allowAllClassTypes: plan?.allowAllClassTypes ?? true,
    classTypeIds: plan?.classTypeIds ?? [],
    futurePrice: plan?.nextEffectivePrice != null ? String(plan.nextEffectivePrice) : "",
    futureEffectiveFrom: plan?.nextEffectiveFrom ?? "",
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const toast = useToast();

  const toggleClassType = (id: string) =>
    setForm((f) => ({
      ...f,
      classTypeIds: f.classTypeIds.includes(id) ? f.classTypeIds.filter((x) => x !== id) : [...f.classTypeIds, id],
    }));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    // Gelecek fiyat: ikisi de gerekli — biri boşsa sessizce yutma, uyar.
    if ((form.futurePrice !== "") !== (form.futureEffectiveFrom !== "")) {
      setErr("Gelecek fiyat için hem yeni fiyat hem geçerlilik tarihi girilmeli (ya da ikisini de boş bırakın).");
      return;
    }
    // Kapsam: 'tüm gruplar' kapalıysa en az bir grup seçilmeli.
    if (!form.allowAllClassTypes && form.classTypeIds.length === 0) {
      setErr("En az bir ders grubu seçin veya 'Tüm ders gruplarında geçerli'yi işaretleyin.");
      return;
    }
    setBusy(true);
    setErr(null);
    try {
      const hasFuture = form.futurePrice !== "" && form.futureEffectiveFrom !== "";
      const body: PackagePlanInput = {
        name: form.name,
        description: form.description || undefined,
        creditCount: Number(form.creditCount),
        price: Number(form.price),
        validityDays: form.validityDays ? Number(form.validityDays) : null,
        allowAllClassTypes: form.allowAllClassTypes,
        classTypeIds: form.allowAllClassTypes ? [] : form.classTypeIds,
        futurePrice: hasFuture ? Number(form.futurePrice) : null,
        futureEffectiveFrom: hasFuture ? form.futureEffectiveFrom : null,
      };
      if (plan) await packagePlanService.update(plan.id, { ...body, isActive: form.isActive });
      else await packagePlanService.create(body);
      toast.success(plan ? "Plan kaydedildi." : "Plan oluşturuldu.");
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={plan ? "Planı düzenle" : "Yeni paket planı"}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Ad"><Input value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} required /></FormField>
        <FormField label="Açıklama (ops.)"><Textarea value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} /></FormField>
        <div className="grid grid-cols-3 gap-3">
          <FormField label="Ders sayısı"><Input type="number" min={1} value={form.creditCount} onChange={(e) => setForm((f) => ({ ...f, creditCount: e.target.value }))} required /></FormField>
          <FormField label="Fiyat (₺)"><Input type="number" min={0} step="0.01" value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: e.target.value }))} required /></FormField>
          <FormField label="Geçerlilik (gün)"><Input type="number" min={1} value={form.validityDays} onChange={(e) => setForm((f) => ({ ...f, validityDays: e.target.value }))} placeholder="süresiz" /></FormField>
        </div>

        {/* Sprint 14: ders grubu kapsamı */}
        <Card className="space-y-3 p-3">
          <label className="flex items-center gap-2 text-sm font-medium text-foreground">
            <input type="checkbox" checked={form.allowAllClassTypes} onChange={(e) => setForm((f) => ({ ...f, allowAllClassTypes: e.target.checked }))} />
            Tüm ders gruplarında geçerli
          </label>
          {!form.allowAllClassTypes && (
            <div className="space-y-2">
              <p className="text-xs text-muted">Bu plan yalnızca seçilen ders gruplarında kullanılabilir.</p>
              <div className="flex flex-wrap gap-2">
                {classTypes.map((c) => (
                  <label key={c.id} className="flex items-center gap-1.5 rounded-md border border-border px-2 py-1 text-sm">
                    <input type="checkbox" checked={form.classTypeIds.includes(c.id)} onChange={() => toggleClassType(c.id)} />
                    {c.name}
                  </label>
                ))}
              </div>
              {form.classTypeIds.length === 0 && (
                <Alert kind="warning">Hiç ders grubu seçilmedi — bu plan hiçbir derste kullanılamaz.</Alert>
              )}
            </div>
          )}
        </Card>

        {/* Sprint 14: gelecek fiyat (opsiyonel) */}
        <Card className="space-y-3 p-3">
          <p className="text-sm font-medium text-foreground">Gelecek fiyat (opsiyonel)</p>
          <p className="text-xs text-muted">Belirtilen tarihte otomatik geçerli olur. Mevcut öğrencilerin paketleri etkilenmez.</p>
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Yeni fiyat (₺)"><Input type="number" min={0} step="0.01" value={form.futurePrice} onChange={(e) => setForm((f) => ({ ...f, futurePrice: e.target.value }))} placeholder="—" /></FormField>
            <FormField label="Geçerlilik tarihi"><Input type="date" value={form.futureEffectiveFrom} onChange={(e) => setForm((f) => ({ ...f, futureEffectiveFrom: e.target.value }))} /></FormField>
          </div>
        </Card>

        {plan && (
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
            Aktif
          </label>
        )}
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Kaydet</Button>
        </div>
      </form>
    </Modal>
  );
}
