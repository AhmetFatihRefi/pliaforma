"use client";

import { useCallback, useEffect, useState } from "react";
import { studioSubscriptionService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type StudioSubscriptionViewDto,
  billingPeriodLabels,
  effectiveStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

const money = (n: number, c = "TRY") => `${n.toLocaleString("tr-TR")} ${c === "TRY" ? "₺" : c}`;
const tone = (s: StudioSubscriptionViewDto["effectiveStatus"]) =>
  s === "Active" || s === "Trialing" ? "success" : s === "Expired" ? "danger" : s === "FailOpenLicensed" ? "neutral" : "warning";

export default function SubscriptionPage() {
  const [d, setD] = useState<StudioSubscriptionViewDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await studioSubscriptionService.mine()); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Abonelik yüklenemedi."); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);

  const cancel = async () => {
    if (!confirm("Aboneliğiniz dönem sonunda iptal edilecek; o tarihe kadar erişiminiz açık kalır. Onaylıyor musunuz?")) return;
    setBusy(true); setErr(null); setInfo(null);
    try { setD(await studioSubscriptionService.cancel()); setInfo("İptal talebiniz alındı (dönem sonunda)."); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İptal edilemedi."); }
    finally { setBusy(false); }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Aboneliğim</h1>
        <p className="mt-1 text-sm text-muted">Plan, lisans durumu ve ödeme geçmişi.</p>
      </div>

      {err && <Alert kind="error">{err}</Alert>}
      {info && <Alert kind="success">{info}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : d ? (
        <>
          <Card>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-lg font-semibold text-foreground">{d.planName ?? "Abonelik yok"}</span>
                  <Badge tone={tone(d.effectiveStatus)}>{effectiveStatusLabels[d.effectiveStatus]}</Badge>
                </div>
                {d.hasSubscription && d.monthlyPrice != null && (
                  <p className="mt-1 text-sm text-muted">
                    {money(d.monthlyPrice, d.currency ?? "TRY")} / {d.billingPeriod ? billingPeriodLabels[d.billingPeriod] : "—"}
                    {d.currentPeriodEnd && ` · dönem sonu ${d.currentPeriodEnd}`}
                  </p>
                )}
              </div>
              {d.hasSubscription && !d.cancelAtPeriodEnd && d.effectiveStatus !== "Expired" && (
                <Button variant="secondary" size="sm" loading={busy} onClick={cancel}>İptal et</Button>
              )}
            </div>

            <div className={`mt-3 rounded-xl border px-4 py-3 text-sm ${d.isLicensed ? "border-border text-muted" : "border-danger/30 bg-danger/5 text-danger"}`}>
              {d.licenseMessage}
            </div>

            {d.cancelAtPeriodEnd && (
              <p className="mt-2 text-xs text-warning">İptal talebi alındı — dönem sonunda ({d.currentPeriodEnd}) sona erecek.</p>
            )}
          </Card>

          <Card>
            <p className="mb-3 text-sm font-semibold text-foreground">Ödeme geçmişi</p>
            {d.payments.length === 0 ? (
              <p className="text-sm text-muted">Henüz ödeme kaydı yok.</p>
            ) : (
              <div className="space-y-1.5">
                {d.payments.map((p) => (
                  <div key={p.id} className="flex items-center justify-between gap-3 text-sm">
                    <span className="text-foreground">{money(p.amount, p.currency)}</span>
                    <span className="text-xs text-muted">{p.periodStart} → {p.periodEnd} · {p.paidAt.slice(0, 10)}</span>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </>
      ) : null}
    </div>
  );
}
