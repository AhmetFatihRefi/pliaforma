"use client";

import { useCallback, useEffect, useState } from "react";
import { notificationService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import { type NotificationDto, notificationTypeLabels } from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;
const tone = (t: NotificationDto["type"]) =>
  t === "PaymentOverdue" ? "danger" : t === "PaymentDueSoon" ? "warning" : t === "ClassReminder" ? "rose" : "neutral";

export default function NotificationsPage() {
  const { canManage } = useRole();
  const [items, setItems] = useState<NotificationDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await notificationService.mine());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Bildirimler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const markRead = async (n: NotificationDto) => {
    if (n.isRead) return;
    try { await notificationService.markRead(n.id); setItems((xs) => xs.map((x) => (x.id === n.id ? { ...x, isRead: true } : x))); }
    catch (e) { setError(e instanceof ApiError ? e.message : "İşlem başarısız."); }
  };
  const markAll = async () => {
    try { await notificationService.markAllRead(); setItems((xs) => xs.map((x) => ({ ...x, isRead: true }))); }
    catch (e) { setError(e instanceof ApiError ? e.message : "İşlem başarısız."); }
  };
  const runReminders = async () => {
    setBusy(true); setInfo(null); setError(null);
    try {
      const r = await notificationService.runReminders();
      setInfo(`${r.total} bildirim üretildi (ders ${r.classReminders}, vade ${r.paymentDueSoon}, gecikmiş ${r.paymentOverdue}).`);
      await load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Çalıştırılamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Bildirimler</h1>
          <p className="mt-1 text-sm text-muted">Ders ve ödeme hatırlatmaları.</p>
        </div>
        <div className="flex gap-2">
          {canManage && <Button variant="secondary" size="sm" loading={busy} onClick={runReminders}>Hatırlatmaları çalıştır</Button>}
          <Button variant="ghost" size="sm" onClick={markAll}>Tümünü okundu yap</Button>
        </div>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {info && <Alert kind="success">{info}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Hiç bildirimin yok.</p></Card>
      ) : (
        <div className="space-y-2">
          {items.map((n) => (
            <Card key={n.id} className={n.isRead ? "" : "border-primary/40"}>
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="flex items-center gap-2">
                    <Badge tone={tone(n.type)}>{notificationTypeLabels[n.type]}</Badge>
                    {!n.isRead && <span className="size-2 rounded-full bg-primary" />}
                  </div>
                  <p className="mt-1 font-medium text-foreground">{n.title}</p>
                  <p className="text-sm text-muted">{n.body}</p>
                  <p className="mt-1 text-xs text-muted">{fmt(n.createdAt)}</p>
                </div>
                {!n.isRead && <Button variant="ghost" size="sm" onClick={() => markRead(n)}>Okundu</Button>}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
