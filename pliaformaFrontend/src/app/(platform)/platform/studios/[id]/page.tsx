"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import {
  type PlatformStudioDetailDto,
  type StudioStatus,
  type SubscriptionPaymentDto,
  type SubscriptionPlanDto,
  billingPeriodLabels,
  effectiveStatusLabels,
  studioStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Textarea } from "@/components/ui";

const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;
const fmt = (iso?: string | null) => (iso ? `${iso.slice(0, 10)} ${iso.slice(11, 16)}` : "—");
const statusTone = (s: StudioStatus) => (s === "Active" ? "success" : s === "Suspended" ? "warning" : "danger");

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
  const [plans, setPlans] = useState<SubscriptionPlanDto[]>([]);
  const [payments, setPayments] = useState<SubscriptionPaymentDto[]>([]);
  const [planId, setPlanId] = useState("");
  const [amount, setAmount] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    platformService.plans(true).then(setPlans).catch(() => {});
    platformService.subscriptionPayments(studioId).then(setPayments).catch(() => {});
  }, [studioId]);

  const after = async (msg: string) => {
    setInfo(msg);
    try { setPayments(await platformService.subscriptionPayments(studioId)); } catch { /* yut */ }
    onChange();
  };
  const act = async (fn: () => Promise<unknown>, msg: string, failMsg: string) => {
    setBusy(true); setErr(null); setInfo(null);
    try { await fn(); await after(msg); }
    catch (e) { setErr(e instanceof ApiError ? e.message : failMsg); }
    finally { setBusy(false); }
  };

  const effTone = !initial ? "neutral"
    : initial.effectiveStatus === "Active" || initial.effectiveStatus === "Trialing" ? "success"
    : initial.effectiveStatus === "Expired" ? "danger" : "warning";

  return (
    <Card>
      <div className="mb-2 flex items-center justify-between">
        <p className="text-sm font-semibold text-foreground">Abonelik & Lisans</p>
        <Badge tone={effTone}>{initial ? effectiveStatusLabels[initial.effectiveStatus] : "Abonelik yok"}</Badge>
      </div>

      {err && <Alert kind="error">{err}</Alert>}
      {info && <Alert kind="success">{info}</Alert>}

      {!initial ? (
        <Alert kind="info">Abonelik kaydı yok — <strong>fail-open lisanslı</strong> (yazma açık). Aşağıdan bir plan atayın.</Alert>
      ) : (
        <div className="rounded-xl border border-border p-3">
          <Row label="Plan" value={`${initial.planName}${initial.tier ? ` · ${initial.tier}` : ""}`} />
          <Row label="Fiyat" value={`${money(initial.monthlyPrice)} / ${billingPeriodLabels[initial.billingPeriod]}`} />
          <Row label="Dönem sonu" value={`${initial.currentPeriodEnd} (${initial.daysRemaining} gün)`} />
          {initial.introPeriodsRemaining != null && initial.introPeriodsRemaining > 0 && (
            <Row label="Intro kalan" value={`${initial.introPeriodsRemaining} dönem`} />
          )}
          <Row label="Lisans" value={initial.isLicensed ? "Lisanslı" : "Lisanssız (yazma kısıtlı)"} />
          {initial.cancelAtPeriodEnd && <Row label="İptal" value="Dönem sonunda iptal edilecek" />}
          <p className="mt-2 text-xs text-muted">{initial.licenseMessage}</p>
        </div>
      )}

      {/* Plan ata */}
      <div className="mt-3 flex flex-wrap items-end gap-2">
        <div className="min-w-[12rem] flex-1">
          <FormField label="Plan ata" htmlFor="assignPlan">
            <Select id="assignPlan" value={planId} onChange={(e) => setPlanId(e.target.value)}>
              <option value="">Plan seç…</option>
              {plans.map((p) => <option key={p.id} value={p.id}>{p.name} · {p.isCustomQuote ? "teklif" : money(p.monthlyPrice)}</option>)}
            </Select>
          </FormField>
        </div>
        <Button variant="secondary" size="sm" disabled={!planId} loading={busy}
          onClick={() => act(() => platformService.assignPlan(studioId, planId), "Plan atandı.", "Atanamadı.")}>Ata</Button>
      </div>

      {/* Ödeme kaydet + iptal */}
      <div className="mt-3 flex flex-wrap items-end gap-2">
        <div className="w-32">
          <FormField label="Tutar (ops.)" htmlFor="payAmt">
            <Input id="payAmt" type="number" min={0} value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="oto" />
          </FormField>
        </div>
        <Button size="sm" loading={busy} disabled={!initial}
          onClick={() => act(() => platformService.recordPayment(studioId, { amount: amount ? Number(amount) : null }), "Ödeme kaydedildi.", "Kaydedilemedi.")}>
          Ödeme kaydet
        </Button>
        {initial && !initial.cancelAtPeriodEnd && (
          <Button variant="ghost" size="sm" loading={busy}
            onClick={() => act(() => platformService.cancelSubscription(studioId), "İptal (dönem sonu) işlendi.", "İptal edilemedi.")}>
            Dönem sonunda iptal
          </Button>
        )}
      </div>

      {/* Tahsilat geçmişi */}
      <div className="mt-4">
        <p className="mb-2 text-xs font-medium uppercase tracking-wide text-muted">Tahsilat geçmişi</p>
        {payments.length === 0 ? (
          <p className="text-sm text-muted">Henüz tahsilat yok.</p>
        ) : (
          <div className="space-y-1">
            {payments.map((p) => (
              <div key={p.id} className="flex items-center justify-between gap-3 text-sm">
                <span className="text-foreground">{money(p.amount)} <span className="text-xs text-muted">({p.source})</span></span>
                <span className="text-xs text-muted">{p.periodStart} → {p.periodEnd} · {p.paidAt.slice(0, 10)}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </Card>
  );
}
