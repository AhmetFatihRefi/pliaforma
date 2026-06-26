"use client";

import { useCallback, useEffect, useState } from "react";
import { classTypeService, closureService } from "@/lib/studioApi";
import { useInstructors } from "@/lib/useInstructors";
import { ApiError } from "@/lib/api";
import {
  type ClassTypeDto,
  type CreateClosureResult,
  type StudioClosureActionType,
  type StudioClosureDto,
  closureActionLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, useConfirm, useToast } from "@/components/ui";
import { formatDateTime } from "@/lib/format";

const fmt = (iso: string) => formatDateTime(iso);

export default function ClosuresPage() {
  const [items, setItems] = useState<StudioClosureDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await closureService.list());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kayıtlar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Kapalı Günler / Toplu İşlemler</h1>
          <p className="mt-1 text-sm text-muted">Tatil veya toplu iptal/erteleme uygula.</p>
        </div>
        <Button onClick={() => setOpen(true)}>+ Yeni işlem</Button>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Henüz kayıt yok.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((c) => (
            <Card key={c.id}>
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-medium text-foreground">{c.title}</p>
                  <p className="text-sm text-muted">{fmt(c.startAt)} → {fmt(c.endAt)}</p>
                  {c.reason && <p className="mt-1 text-xs text-muted">{c.reason}</p>}
                </div>
                <Badge tone="rose">{closureActionLabels[c.actionType]}</Badge>
              </div>
              <div className="mt-3 flex flex-wrap gap-2 border-t border-border pt-3 text-xs text-muted">
                <span>Etkilenen: {c.affectedCount}</span>
                {c.cancelledCount > 0 && <span>· İptal: {c.cancelledCount}</span>}
                {c.creditedCount > 0 && <span>· Kredi: {c.creditedCount}</span>}
                {c.rescheduledCount > 0 && <span>· Ertelendi: {c.rescheduledCount}</span>}
                {c.skippedCount > 0 && <span>· Atlandı: {c.skippedCount}</span>}
              </div>
            </Card>
          ))}
        </div>
      )}

      {open && <CreateClosureModal onClose={() => setOpen(false)} onDone={() => { setOpen(false); load(); }} />}
    </div>
  );
}

const todayStr = () => new Date().toISOString().slice(0, 16);

function CreateClosureModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const confirm = useConfirm();
  const toast = useToast();
  const { instructors } = useInstructors();
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [form, setForm] = useState({
    title: "",
    reason: "",
    startAt: todayStr(),
    endAt: todayStr(),
    actionType: "CancelSessions" as StudioClosureActionType,
    offsetDays: "7",
    affectedInstructorProfileId: "",
    affectedClassTypeId: "",
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<CreateClosureResult | null>(null);

  useEffect(() => {
    classTypeService.list(true).then(setClassTypes).catch(() => setClassTypes([]));
  }, []);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (form.endAt < form.startAt) { setErr("Bitiş zamanı, başlangıçtan sonra olmalı."); return; }
    // Toplu, geri-alınamaz, kredi etkili işlem → açık uyarı (kesin sayı backend dry-run gerektirir, uydurulmaz).
    if (!(await confirm({
      title: "Toplu işlemi uygula",
      tone: "danger",
      confirmLabel: "Uygula",
      message: "Bu işlem seçili tarih aralığındaki dersleri etkileyebilir (iptal/erteleme; öğrencilere telafi kredisi verilebilir). İşlem geri alınamaz — etkilenen dersleri uyguladıktan sonra sonuç ekranında göreceksiniz.",
    }))) return;
    setBusy(true);
    setErr(null);
    try {
      const r = await closureService.create({
        title: form.title,
        reason: form.reason || undefined,
        startAt: form.startAt,
        endAt: form.endAt,
        actionType: form.actionType,
        offsetDays: form.actionType === "RescheduleByOffset" ? Number(form.offsetDays) : undefined,
        affectedInstructorProfileId: form.affectedInstructorProfileId || undefined,
        affectedClassTypeId: form.affectedClassTypeId || undefined,
      });
      setResult(r);
      toast.success("Toplu işlem uygulandı.");
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "İşlem uygulanamadı.");
    } finally {
      setBusy(false);
    }
  };

  if (result) {
    const c = result.closure;
    return (
      <Modal open onClose={onDone} title="Toplu işlem sonucu">
        <div className="space-y-3">
          <Alert kind="success">
            {c.affectedCount} seans etkilendi — iptal {c.cancelledCount}, kredi {c.creditedCount},
            ertelendi {c.rescheduledCount}, atlandı {c.skippedCount}.
          </Alert>
          <div className="max-h-72 space-y-1 overflow-y-auto">
            {result.sessions.map((s) => (
              <div key={s.sessionId} className="flex items-center justify-between rounded-lg border border-border px-3 py-2 text-sm">
                <span className="text-muted">{fmt(s.originalStartAt)}{s.newStartAt && s.outcome === "rescheduled" ? ` → ${fmt(s.newStartAt)}` : ""}</span>
                <Badge tone={s.outcome === "skipped" ? "warning" : "success"}>
                  {s.outcome}{s.reason ? ` (${s.reason})` : ""}
                </Badge>
              </div>
            ))}
          </div>
          <div className="flex justify-end pt-2">
            <Button onClick={onDone}>Bitti</Button>
          </div>
        </div>
      </Modal>
    );
  }

  return (
    <Modal open onClose={onClose} title="Yeni toplu işlem">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Başlık">
          <Input value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} placeholder="örn. Resmi tatil" required />
        </FormField>
        <FormField label="Gerekçe (ops.)">
          <Input value={form.reason} onChange={(e) => setForm((f) => ({ ...f, reason: e.target.value }))} />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Başlangıç"><Input type="datetime-local" value={form.startAt} onChange={(e) => setForm((f) => ({ ...f, startAt: e.target.value }))} required /></FormField>
          <FormField label="Bitiş"><Input type="datetime-local" value={form.endAt} onChange={(e) => setForm((f) => ({ ...f, endAt: e.target.value }))} required /></FormField>
        </div>
        <FormField label="Aksiyon">
          <Select value={form.actionType} onChange={(e) => setForm((f) => ({ ...f, actionType: e.target.value as StudioClosureActionType }))}>
            <option value="CancelSessions">Seansları iptal et</option>
            <option value="CancelAndGrantMakeupCredit">İptal et + telafi kredisi ver</option>
            <option value="RescheduleByOffset">Gün ofsetiyle ertele</option>
          </Select>
        </FormField>
        {form.actionType === "RescheduleByOffset" && (
          <FormField label="Gün ofseti" hint="Pozitif → ileri al">
            <Input type="number" value={form.offsetDays} onChange={(e) => setForm((f) => ({ ...f, offsetDays: e.target.value }))} required />
          </FormField>
        )}
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Hoca (ops.)">
            <Select value={form.affectedInstructorProfileId} onChange={(e) => setForm((f) => ({ ...f, affectedInstructorProfileId: e.target.value }))}>
              <option value="">Tümü</option>
              {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Grup (ops.)">
            <Select value={form.affectedClassTypeId} onChange={(e) => setForm((f) => ({ ...f, affectedClassTypeId: e.target.value }))}>
              <option value="">Tümü</option>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Uygula</Button>
        </div>
      </form>
    </Modal>
  );
}
