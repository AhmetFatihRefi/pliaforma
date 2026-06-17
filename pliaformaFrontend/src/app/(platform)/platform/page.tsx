"use client";

import { useCallback, useEffect, useState } from "react";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import type { PlatformActivityItemDto, PlatformOverviewDto } from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;
const fmt = (iso?: string | null) => (iso ? `${iso.slice(0, 10)} ${iso.slice(11, 16)}` : "—");

function Stat({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <Card>
      <p className="text-xs font-medium uppercase tracking-wide text-muted">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-foreground">{value}</p>
      {hint && <p className="mt-0.5 text-xs text-muted">{hint}</p>}
    </Card>
  );
}

export default function PlatformOverviewPage() {
  const [d, setD] = useState<PlatformOverviewDto | null>(null);
  const [activity, setActivity] = useState<PlatformActivityItemDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [o, a] = await Promise.all([platformService.overview(), platformService.recentActivity()]);
      setD(o);
      setActivity(a);
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Genel bakış yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Genel Bakış</h1>
        <p className="mt-1 text-sm text-muted">Tüm stüdyolar · {d?.periodLabel ?? "—"}</p>
      </div>

      {err && <Alert kind="error">{err}</Alert>}
      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : d ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Toplam stüdyo" value={d.totalStudios} hint={`${d.activeStudios} aktif · ${d.suspendedStudios} askıda`} />
            <Stat label="Toplam kullanıcı" value={d.totalUsers} />
            <Stat label="Öğrenci" value={d.totalStudents} />
            <Stat label="Hoca" value={d.totalInstructors} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Bu ay abonelik tahsilatı" value={money(d.manualSubscriptionRevenueThisMonth)} hint="manuel (ledger)" />
            <Stat label="Aboneliksiz stüdyo" value={d.missingSubscriptionCount} hint="fail-open lisanslı" />
            <Stat label="Gecikmiş ödeme (öğrenci)" value={money(d.overduePaymentAmount)} />
            <Stat label="İptal stüdyo" value={d.cancelledStudios} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Bu ay rezervasyon" value={d.totalBookingsThisMonth} />
            <Stat label="Bu ay ödeme kaydı" value={d.totalPaymentsThisMonth} />
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Bekleyen bildirim" value={d.notificationsPending} />
            <Stat label="Başarısız bildirim" value={d.notificationsFailed} />
            <Stat label="Son bildirim" value={fmt(d.lastNotificationActivityUtc)} />
            <Card>
              <p className="text-xs font-medium uppercase tracking-wide text-muted">Veritabanı</p>
              <div className="mt-2">
                <Badge tone={d.databaseHealthy ? "success" : "danger"}>{d.databaseHealthy ? "Sağlıklı" : "Sorunlu"}</Badge>
              </div>
            </Card>
          </div>

          <Card>
            <p className="mb-3 text-sm font-semibold text-foreground">Son aktivite</p>
            {activity.length === 0 ? (
              <p className="text-sm text-muted">Kayıt yok.</p>
            ) : (
              <div className="space-y-1.5">
                {activity.map((a, i) => (
                  <div key={i} className="flex items-center justify-between gap-3 text-sm">
                    <span className="text-foreground">{a.description}</span>
                    <span className="shrink-0 text-xs text-muted">{fmt(a.atUtc)}</span>
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
