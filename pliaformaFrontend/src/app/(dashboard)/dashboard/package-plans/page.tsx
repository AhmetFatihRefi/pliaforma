"use client";

import { useCallback, useEffect, useState } from "react";
import { packagePlanService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import type { PackagePlanDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Table, TableEmpty, TD, TH, THead, TR, Textarea } from "@/components/ui";

export default function PackagePlansPage() {
  const [items, setItems] = useState<PackagePlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<PackagePlanDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await packagePlanService.list());
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
    if (!confirm(`"${p.name}" planı silinsin mi?`)) return;
    try {
      await packagePlanService.remove(p.id);
      await load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Silinemedi.");
    }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Paket Planları</h1>
          <p className="mt-1 text-sm text-muted">Öğrencilere satılacak paket şablonları.</p>
        </div>
        <Button onClick={() => { setEditing(null); setFormOpen(true); }}>+ Yeni plan</Button>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead><TR><TH>Ad</TH><TH>Ders</TH><TH>Fiyat</TH><TH>Geçerlilik</TH><TH>Durum</TH><TH className="text-right">İşlem</TH></TR></THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={6}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={6}>Henüz paket planı yok.</TableEmpty>
          ) : (
            items.map((p) => (
              <TR key={p.id}>
                <TD className="font-medium">{p.name}</TD>
                <TD>{p.creditCount}</TD>
                <TD>{p.price.toLocaleString("tr-TR")} ₺</TD>
                <TD className="text-muted">{p.validityDays ? `${p.validityDays} gün` : "Süresiz"}</TD>
                <TD><Badge tone={p.isActive ? "success" : "neutral"}>{p.isActive ? "Aktif" : "Pasif"}</Badge></TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => { setEditing(p); setFormOpen(true); }}>Düzenle</Button>
                    <Button variant="ghost" size="sm" onClick={() => remove(p)}>Sil</Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      {formOpen && <PlanModal plan={editing} onClose={() => setFormOpen(false)} onDone={() => { setFormOpen(false); load(); }} />}
    </div>
  );
}

function PlanModal({ plan, onClose, onDone }: { plan: PackagePlanDto | null; onClose: () => void; onDone: () => void }) {
  const [form, setForm] = useState({
    name: plan?.name ?? "",
    description: plan?.description ?? "",
    creditCount: String(plan?.creditCount ?? 8),
    price: String(plan?.price ?? 0),
    validityDays: plan?.validityDays != null ? String(plan.validityDays) : "",
    isActive: plan?.isActive ?? true,
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      const body = {
        name: form.name,
        description: form.description || undefined,
        creditCount: Number(form.creditCount),
        price: Number(form.price),
        validityDays: form.validityDays ? Number(form.validityDays) : null,
      };
      if (plan) await packagePlanService.update(plan.id, { ...body, isActive: form.isActive });
      else await packagePlanService.create(body);
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
