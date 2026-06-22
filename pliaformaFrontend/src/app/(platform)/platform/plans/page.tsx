"use client";

import { useCallback, useEffect, useState } from "react";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import {
  type BillingPeriod,
  type SubscriptionPlanDto,
  type SubscriptionPlanInput,
  billingPeriodLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, useConfirm } from "@/components/ui";

const money = (n: number, c = "TRY") => `${n.toLocaleString("tr-TR")} ${c === "TRY" ? "₺" : c}`;

const emptyPlan = (order: number): SubscriptionPlanInput => ({
  name: "", tier: "", monthlyPrice: 0, currency: "TRY", billingPeriod: "Monthly",
  introMonthlyPrice: null, introPeriodCount: null, isCustomQuote: false, isDefault: false,
  isActive: true, displayOrder: order, description: "", featuresJson: null, maxInstructors: null, maxStudents: null,
});

export default function PlatformPlansPage() {
  const confirm = useConfirm();
  const [plans, setPlans] = useState<SubscriptionPlanDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ id?: string; input: SubscriptionPlanInput } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setPlans(await platformService.plans()); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Planlar yüklenemedi."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);

  const remove = async (p: SubscriptionPlanDto) => {
    if (!(await confirm({ title: "Planı sil", tone: "danger", confirmLabel: "Sil", message: `"${p.name}" planı silinecek.` }))) return;
    try { await platformService.deletePlan(p.id); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Silinemedi."); }
  };

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Abonelik Planları</h1>
          <p className="mt-1 text-sm text-muted">Dinamik plan kataloğu. Default plan yeni stüdyolara 14g deneme açar.</p>
        </div>
        <Button size="sm" onClick={() => setEditing({ input: emptyPlan(plans.length + 1) })}>+ Yeni plan</Button>
      </div>

      {err && <Alert kind="error">{err}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : (
        <div className="space-y-3">
          {plans.map((p) => (
            <Card key={p.id}>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium text-foreground">{p.name}</span>
                    {p.tier && <span className="text-xs text-muted">· {p.tier}</span>}
                    {p.isDefault && <Badge tone="rose">Default</Badge>}
                    {!p.isActive && <Badge tone="neutral">Pasif</Badge>}
                    {p.isCustomQuote && <Badge tone="warning">Teklif</Badge>}
                  </div>
                  <p className="mt-1 text-sm text-foreground">
                    {p.isCustomQuote ? "Tekliflendirilir" : `${money(p.monthlyPrice, p.currency)} / ${billingPeriodLabels[p.billingPeriod]}`}
                    {p.introMonthlyPrice != null && p.introPeriodCount != null && (
                      <span className="ml-2 text-xs text-primary">(ilk {p.introPeriodCount} dönem {money(p.introMonthlyPrice, p.currency)})</span>
                    )}
                  </p>
                  {p.description && <p className="mt-0.5 text-xs text-muted">{p.description}</p>}
                </div>
                <div className="flex shrink-0 gap-2">
                  <Button variant="ghost" size="sm" onClick={() => setEditing({ id: p.id, input: { ...p } })}>Düzenle</Button>
                  <Button variant="ghost" size="sm" onClick={() => remove(p)}>Sil</Button>
                </div>
              </div>
            </Card>
          ))}
          {plans.length === 0 && <Card><p className="text-sm text-muted">Henüz plan yok.</p></Card>}
        </div>
      )}

      {editing && (
        <PlanModal
          id={editing.id}
          initial={editing.input}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); load(); }}
        />
      )}
    </div>
  );
}

function PlanModal({ id, initial, onClose, onSaved }: {
  id?: string; initial: SubscriptionPlanInput; onClose: () => void; onSaved: () => void;
}) {
  const [f, setF] = useState<SubscriptionPlanInput>(initial);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const set = <K extends keyof SubscriptionPlanInput>(k: K, v: SubscriptionPlanInput[K]) => setF((s) => ({ ...s, [k]: v }));

  const save = async () => {
    setBusy(true); setErr(null);
    try {
      const body = { ...f, tier: f.tier || null, description: f.description || null };
      if (id) await platformService.updatePlan(id, body); else await platformService.createPlan(body);
      onSaved();
    } catch (e) { setErr(e instanceof ApiError ? e.message : "Kaydedilemedi."); }
    finally { setBusy(false); }
  };

  return (
    <Modal open onClose={onClose} title={id ? "Planı düzenle" : "Yeni plan"}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        <div className="grid gap-3 sm:grid-cols-2">
          <FormField label="Ad" htmlFor="pname"><Input id="pname" value={f.name} onChange={(e) => set("name", e.target.value)} /></FormField>
          <FormField label="Kademe (tier)" htmlFor="ptier"><Input id="ptier" value={f.tier ?? ""} onChange={(e) => set("tier", e.target.value)} /></FormField>
          <FormField label="Aylık ücret" htmlFor="pprice"><Input id="pprice" type="number" min={0} value={f.monthlyPrice} onChange={(e) => set("monthlyPrice", Number(e.target.value))} /></FormField>
          <FormField label="Para birimi" htmlFor="pcur"><Input id="pcur" maxLength={3} value={f.currency} onChange={(e) => set("currency", e.target.value.toUpperCase())} /></FormField>
          <FormField label="Dönem" htmlFor="pbp">
            <Select id="pbp" value={f.billingPeriod} onChange={(e) => set("billingPeriod", e.target.value as BillingPeriod)}>
              <option value="Monthly">Aylık</option><option value="Yearly">Yıllık</option>
            </Select>
          </FormField>
          <FormField label="Sıra" htmlFor="pord"><Input id="pord" type="number" value={f.displayOrder} onChange={(e) => set("displayOrder", Number(e.target.value))} /></FormField>
          <FormField label="Intro fiyat (ops.)" htmlFor="pintro"><Input id="pintro" type="number" min={0} value={f.introMonthlyPrice ?? ""} onChange={(e) => set("introMonthlyPrice", e.target.value === "" ? null : Number(e.target.value))} /></FormField>
          <FormField label="Intro dönem adedi" htmlFor="pintroc"><Input id="pintroc" type="number" min={1} value={f.introPeriodCount ?? ""} onChange={(e) => set("introPeriodCount", e.target.value === "" ? null : Number(e.target.value))} /></FormField>
          <FormField label="Max hoca (gösterim)" htmlFor="pmi"><Input id="pmi" type="number" min={0} value={f.maxInstructors ?? ""} onChange={(e) => set("maxInstructors", e.target.value === "" ? null : Number(e.target.value))} /></FormField>
          <FormField label="Max öğrenci (gösterim)" htmlFor="pms"><Input id="pms" type="number" min={0} value={f.maxStudents ?? ""} onChange={(e) => set("maxStudents", e.target.value === "" ? null : Number(e.target.value))} /></FormField>
        </div>
        <FormField label="Açıklama" htmlFor="pdesc"><Input id="pdesc" value={f.description ?? ""} onChange={(e) => set("description", e.target.value)} /></FormField>
        <div className="flex flex-wrap gap-4">
          <label className="flex items-center gap-2 text-sm text-foreground"><input type="checkbox" checked={f.isDefault} onChange={(e) => set("isDefault", e.target.checked)} /> Default plan</label>
          <label className="flex items-center gap-2 text-sm text-foreground"><input type="checkbox" checked={f.isActive} onChange={(e) => set("isActive", e.target.checked)} /> Aktif</label>
          <label className="flex items-center gap-2 text-sm text-foreground"><input type="checkbox" checked={f.isCustomQuote} onChange={(e) => set("isCustomQuote", e.target.checked)} /> Özel teklif (Kurumsal)</label>
        </div>
        <div className="flex justify-end gap-2 pt-1">
          <Button variant="ghost" size="sm" onClick={onClose}>Vazgeç</Button>
          <Button size="sm" loading={busy} disabled={!f.name.trim()} onClick={save}>Kaydet</Button>
        </div>
      </div>
    </Modal>
  );
}
