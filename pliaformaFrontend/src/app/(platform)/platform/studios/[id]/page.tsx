"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import {
  type BillingPeriod,
  type PlatformStudioDetailDto,
  type StudioStatus,
  type SubscriptionInput,
  type SubscriptionStatus,
  billingPeriodLabels,
  studioStatusLabels,
  subscriptionStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Textarea } from "@/components/ui";

const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;
const fmt = (iso?: string | null) => (iso ? `${iso.slice(0, 10)} ${iso.slice(11, 16)}` : "—");
const statusTone = (s: StudioStatus) => (s === "Active" ? "success" : s === "Suspended" ? "warning" : "danger");
const today = () => new Date().toISOString().slice(0, 10);

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex justify-between gap-3 py-1 text-sm">
      <span className="text-muted">{label}</span>
      <span className="text-right text-foreground">{value}</span>
    </div>
  );
}

export default function PlatformStudioDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const [d, setD] = useState<PlatformStudioDetailDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const [suspendOpen, setSuspendOpen] = useState(false);
  const [reason, setReason] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setD(await platformService.studioDetail(id));
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Stüdyo yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const doSuspend = async () => {
    setBusy(true); setErr(null); setInfo(null);
    try {
      await platformService.suspend(id, reason);
      setSuspendOpen(false); setReason("");
      setInfo("Stüdyo askıya alındı.");
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Askıya alınamadı.");
    } finally {
      setBusy(false);
    }
  };

  const doReactivate = async () => {
    setBusy(true); setErr(null); setInfo(null);
    try {
      await platformService.reactivate(id);
      setInfo("Stüdyo yeniden aktifleştirildi.");
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Aktifleştirilemedi.");
    } finally {
      setBusy(false);
    }
  };

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;
  if (!d) return <Alert kind="error">{err ?? "Stüdyo bulunamadı."}</Alert>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <Link href="/platform/studios" className="text-sm text-muted hover:text-foreground">← Stüdyolar</Link>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-2xl font-semibold text-foreground">{d.name}</h1>
            <Badge tone={statusTone(d.status)}>{studioStatusLabels[d.status]}</Badge>
          </div>
          <p className="mt-1 text-sm text-muted">{d.slug} · oluşturuldu {d.createdAt.slice(0, 10)}</p>
        </div>
        <div className="flex gap-2">
          {d.status === "Active" ? (
            <Button variant="secondary" size="sm" onClick={() => setSuspendOpen(true)}>Askıya al</Button>
          ) : (
            <Button size="sm" loading={busy} onClick={doReactivate}>Yeniden aktifleştir</Button>
          )}
        </div>
      </div>

      {err && <Alert kind="error">{err}</Alert>}
      {info && <Alert kind="success">{info}</Alert>}

      {d.status !== "Active" && d.suspendedReason && (
        <Alert kind="info">
          Askı gerekçesi: {d.suspendedReason}
          {d.suspendedAt && ` · ${fmt(d.suspendedAt)}`}
        </Alert>
      )}

      <div className="grid gap-5 lg:grid-cols-2">
        <Card>
          <p className="mb-2 text-sm font-semibold text-foreground">Stüdyo</p>
          <Row label="Zaman dilimi" value={d.timeZoneId} />
          <Row label="Para birimi" value={d.currency} />
          <Row label="E-posta" value={d.contactEmail ?? "—"} />
          <Row label="Telefon" value={d.contactPhone ?? "—"} />
          <Row label="Şehir" value={d.city ?? "—"} />
          {d.reactivatedAt && <Row label="Son aktifleştirme" value={fmt(d.reactivatedAt)} />}
        </Card>

        <Card>
          <p className="mb-2 text-sm font-semibold text-foreground">Sahip</p>
          <Row label="Ad" value={d.owner.fullName} />
          <Row label="E-posta" value={d.owner.email} />
          <Row label="Telefon" value={d.owner.phone ?? "—"} />
        </Card>
      </div>

      <Card>
        <p className="mb-2 text-sm font-semibold text-foreground">Sayımlar (bu ay)</p>
        <div className="grid grid-cols-2 gap-x-6 sm:grid-cols-3">
          <Row label="Kullanıcı" value={d.counts.userCount} />
          <Row label="Öğrenci" value={`${d.counts.studentCount} (${d.counts.activeStudentCount} aktif)`} />
          <Row label="Misafir öğrenci" value={d.counts.guestStudentCount} />
          <Row label="Hoca" value={d.counts.instructorCount} />
          <Row label="Seans" value={d.counts.sessionCountThisMonth} />
          <Row label="Rezervasyon" value={d.counts.bookingCountThisMonth} />
          <Row label="Ödeme" value={money(d.counts.paymentAmountThisMonth)} />
          <Row label="Gecikmiş" value={money(d.counts.overduePaymentAmount)} />
        </div>
      </Card>

      <SubscriptionCard studioId={id} initial={d.subscription} onChange={() => load()} />

      <Card>
        <p className="mb-2 text-sm font-semibold text-foreground">Son aktivite</p>
        {d.recentActivity.length === 0 ? (
          <p className="text-sm text-muted">Kayıt yok.</p>
        ) : (
          <div className="space-y-1.5">
            {d.recentActivity.map((a, i) => (
              <div key={i} className="flex items-center justify-between gap-3 text-sm">
                <span className="text-foreground">{a.description}</span>
                <span className="shrink-0 text-xs text-muted">{fmt(a.atUtc)}</span>
              </div>
            ))}
          </div>
        )}
      </Card>

      <Modal open={suspendOpen} onClose={() => setSuspendOpen(false)} title="Stüdyoyu askıya al">
        <div className="space-y-4">
          <p className="text-sm text-muted">
            Askıya alınan stüdyo <strong>yazma yapamaz</strong> (okuma çalışmaya devam eder). Veri silinmez.
          </p>
          <FormField label="Gerekçe" htmlFor="reason">
            <Textarea id="reason" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Örn. ödeme gecikmesi" />
          </FormField>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={() => setSuspendOpen(false)}>Vazgeç</Button>
            <Button size="sm" loading={busy} disabled={!reason.trim()} onClick={doSuspend}>Askıya al</Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}

function SubscriptionCard({
  studioId,
  initial,
  onChange,
}: {
  studioId: string;
  initial?: PlatformStudioDetailDto["subscription"];
  onChange: () => void;
}) {
  const [form, setForm] = useState<SubscriptionInput>(() => ({
    planName: initial?.planName ?? "Free",
    tier: initial?.tier ?? "",
    status: (initial?.status ?? "Trialing") as SubscriptionStatus,
    monthlyPrice: initial?.monthlyPrice ?? 0,
    currency: initial?.currency ?? "TRY",
    billingPeriod: (initial?.billingPeriod ?? "Monthly") as BillingPeriod,
    currentPeriodStart: initial?.currentPeriodStart ?? today(),
    currentPeriodEnd: initial?.currentPeriodEnd ?? today(),
    trialEndsAt: initial?.trialEndsAt ?? null,
    notes: initial?.notes ?? "",
  }));
  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const set = <K extends keyof SubscriptionInput>(k: K, v: SubscriptionInput[K]) =>
    setForm((f) => ({ ...f, [k]: v }));

  const save = async () => {
    setBusy(true); setErr(null); setInfo(null);
    try {
      await platformService.updateSubscription(studioId, { ...form, tier: form.tier || null, notes: form.notes || null });
      setInfo("Abonelik kaydedildi.");
      onChange();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  const cancel = async () => {
    setBusy(true); setErr(null); setInfo(null);
    try {
      const updated = await platformService.cancelSubscription(studioId);
      set("status", updated.status);
      setInfo("Abonelik iptal edildi.");
      onChange();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "İptal edilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <div className="mb-3 flex items-center justify-between">
        <p className="text-sm font-semibold text-foreground">Abonelik</p>
        <Badge tone={initial ? "rose" : "neutral"}>{initial ? subscriptionStatusLabels[initial.status] : "Yok"}</Badge>
      </div>
      <p className="mb-4 text-xs text-muted">
        Takip amaçlı iskelet — gerçek tahsilat/online ödeme yoktur.
      </p>

      {err && <Alert kind="error">{err}</Alert>}
      {info && <Alert kind="success">{info}</Alert>}

      <div className="grid gap-3 sm:grid-cols-2">
        <FormField label="Plan adı" htmlFor="planName">
          <Input id="planName" value={form.planName} onChange={(e) => set("planName", e.target.value)} />
        </FormField>
        <FormField label="Kademe" htmlFor="tier">
          <Input id="tier" value={form.tier ?? ""} onChange={(e) => set("tier", e.target.value)} placeholder="Örn. Pro" />
        </FormField>
        <FormField label="Durum" htmlFor="status">
          <Select id="status" value={form.status} onChange={(e) => set("status", e.target.value as SubscriptionStatus)}>
            {(["Trialing", "Active", "PastDue", "Suspended", "Cancelled"] as SubscriptionStatus[]).map((s) => (
              <option key={s} value={s}>{subscriptionStatusLabels[s]}</option>
            ))}
          </Select>
        </FormField>
        <FormField label="Dönem" htmlFor="billing">
          <Select id="billing" value={form.billingPeriod} onChange={(e) => set("billingPeriod", e.target.value as BillingPeriod)}>
            {(["Monthly", "Yearly"] as BillingPeriod[]).map((p) => (
              <option key={p} value={p}>{billingPeriodLabels[p]}</option>
            ))}
          </Select>
        </FormField>
        <FormField label="Aylık ücret" htmlFor="price">
          <Input id="price" type="number" min={0} step="0.01" value={form.monthlyPrice}
            onChange={(e) => set("monthlyPrice", Number(e.target.value))} />
        </FormField>
        <FormField label="Para birimi" htmlFor="currency">
          <Input id="currency" maxLength={3} value={form.currency} onChange={(e) => set("currency", e.target.value.toUpperCase())} />
        </FormField>
        <FormField label="Dönem başı" htmlFor="start">
          <Input id="start" type="date" value={form.currentPeriodStart} onChange={(e) => set("currentPeriodStart", e.target.value)} />
        </FormField>
        <FormField label="Dönem sonu" htmlFor="end">
          <Input id="end" type="date" value={form.currentPeriodEnd} onChange={(e) => set("currentPeriodEnd", e.target.value)} />
        </FormField>
        <FormField label="Deneme bitişi" htmlFor="trial">
          <Input id="trial" type="date" value={form.trialEndsAt ?? ""} onChange={(e) => set("trialEndsAt", e.target.value || null)} />
        </FormField>
        <FormField label="Not" htmlFor="notes">
          <Input id="notes" value={form.notes ?? ""} onChange={(e) => set("notes", e.target.value)} />
        </FormField>
      </div>

      <div className="mt-4 flex justify-end gap-2">
        {initial && initial.status !== "Cancelled" && (
          <Button variant="ghost" size="sm" loading={busy} onClick={cancel}>Aboneliği iptal et</Button>
        )}
        <Button size="sm" loading={busy} onClick={save}>{initial ? "Kaydet" : "Abonelik oluştur"}</Button>
      </div>
    </Card>
  );
}
