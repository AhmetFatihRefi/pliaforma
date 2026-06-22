"use client";

import { useCallback, useEffect, useState } from "react";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import type { PlatformHealthSummaryDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

const fmt = (iso?: string | null) => (iso ? `${iso.slice(0, 10)} ${iso.slice(11, 19)} (TR)` : "—");

// İzleme (Prometheus/Grafana) bağlantıları yalnız build-time NEXT_PUBLIC_* env'leriyle gelir.
// Prod build'de bu değişkenler TANIMSIZ → monitoring kartı GİZLENİR (pilot prod'da izleme yığını çalışmaz).
// Dev'de görmek için pliaformaFrontend/.env.local içine ekleyin (bkz. .env.example).
const GRAFANA_URL = process.env.NEXT_PUBLIC_GRAFANA_URL;
const PROMETHEUS_URL = process.env.NEXT_PUBLIC_PROMETHEUS_URL;
const METRICS_URL = process.env.NEXT_PUBLIC_METRICS_URL;
const MONITORING_ENABLED = Boolean(GRAFANA_URL);

function Stat({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <Card>
      <p className="text-xs font-medium uppercase tracking-wide text-muted">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-foreground">{value}</p>
      {hint && <p className="mt-0.5 text-xs text-muted">{hint}</p>}
    </Card>
  );
}

export default function PlatformHealthPage() {
  const [d, setD] = useState<PlatformHealthSummaryDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setD(await platformService.healthSummary());
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Sağlık özeti yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Sistem Sağlığı</h1>
          <p className="mt-1 text-sm text-muted">Read-only sağlık özeti{MONITORING_ENABLED ? " + Prometheus/Grafana canlı metrikleri" : ""}.</p>
        </div>
        <Button variant="secondary" size="sm" onClick={load}>Yenile</Button>
      </div>

      {err && <Alert kind="error">{err}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : d ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Card>
              <p className="text-xs font-medium uppercase tracking-wide text-muted">Veritabanı</p>
              <div className="mt-2"><Badge tone={d.databaseHealthy ? "success" : "danger"}>{d.databaseHealthy ? "Sağlıklı" : "Sorunlu"}</Badge></div>
            </Card>
            <Card>
              <p className="text-xs font-medium uppercase tracking-wide text-muted">Hatırlatma worker</p>
              <div className="mt-2"><Badge tone={d.reminderWorkerEnabled ? "success" : "neutral"}>{d.reminderWorkerEnabled ? "Açık" : "Kapalı"}</Badge></div>
              <p className="mt-1 text-xs text-muted">Aralık: {d.reminderIntervalSeconds}s</p>
            </Card>
            <Stat label="Sunucu saati" value={fmt(d.serverTimeUtc)} />
          </div>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <Stat label="Toplam stüdyo" value={d.totalStudios} hint={`${d.activeStudios} aktif · ${d.suspendedStudios} askıda`} />
            <Stat label="Bekleyen bildirim" value={d.notificationsPending} />
            <Stat label="Başarısız bildirim" value={d.notificationsFailed} />
            <Stat label="Son 24s gönderilen" value={d.notificationsSentLast24h} />
            <Stat label="Son bildirim" value={fmt(d.lastNotificationActivityUtc)} />
            <Stat label="Aboneliksiz stüdyo" value={d.missingSubscriptionCount} hint="fail-open lisanslı" />
          </div>

        </>
      ) : null}

      {/* İzleme: Prometheus + Grafana (opsiyonel, bağımsız yığın). Yalnız NEXT_PUBLIC_GRAFANA_URL
          tanımlıysa görünür → prod build'de tanımsız olduğundan GİZLİ (güvenli varsayılan). */}
      {MONITORING_ENABLED && (
        <Card>
          <div className="flex flex-wrap items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="text-sm font-semibold text-foreground">İzleme — Prometheus & Grafana</p>
              <p className="mt-0.5 text-xs text-muted">
                Canlı metrikler: istek/sn, yanıt süresi (p95), bellek, CPU. Yığını başlatmak için:{" "}
                <code className="rounded bg-surface-muted px-1 py-0.5">GF_ADMIN_PASSWORD=... docker compose -f docker-compose.monitoring.yml up -d</code>
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <MonitorLink href={GRAFANA_URL!}>Grafana</MonitorLink>
              {PROMETHEUS_URL && <MonitorLink href={PROMETHEUS_URL}>Prometheus</MonitorLink>}
              {METRICS_URL && <MonitorLink href={METRICS_URL}>/metrics</MonitorLink>}
            </div>
          </div>
          <div className="mt-4 overflow-hidden rounded-xl border border-border bg-surface-muted">
            <iframe
              src={GRAFANA_URL!}
              title="Grafana — Pliaforma Genel Bakış"
              className="h-[640px] w-full"
              loading="lazy"
            />
          </div>
          <p className="mt-2 text-xs text-muted">
            Panel boş görünüyorsa izleme yığını çalışmıyordur. Erişim kimlik doğrulamalıdır (Grafana admin parolası env ile verilir).
          </p>
        </Card>
      )}
    </div>
  );
}

function MonitorLink({ href, children }: { href: string; children: React.ReactNode }) {
  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer"
      className="rounded-lg border border-border bg-surface px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:bg-surface-muted"
    >
      {children} ↗
    </a>
  );
}
