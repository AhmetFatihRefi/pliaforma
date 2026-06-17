"use client";

import { useCallback, useEffect, useState } from "react";
import { studentBookingService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import type { MyBookingDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;

export default function MyBookingsPage() {
  const [items, setItems] = useState<MyBookingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await studentBookingService.myBookings());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Rezervasyonlar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const cancel = async (b: MyBookingDto) => {
    if (!confirm("Rezervasyon iptal edilsin mi?")) return;
    try {
      await studentBookingService.cancel(b.bookingId);
      await load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "İptal edilemedi.");
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Rezervasyonlarım</h1>
        <p className="mt-1 text-sm text-muted">Yaklaşan derslerin.</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Yaklaşan rezervasyonun yok.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((b) => (
            <Card key={b.bookingId} className="flex items-center justify-between">
              <div>
                <p className="font-medium text-foreground">{b.classTypeName}</p>
                <p className="text-sm text-muted">{b.instructorName} · {fmt(b.startAt)}</p>
              </div>
              <div className="flex items-center gap-2">
                <Badge tone="success">Rezerve</Badge>
                <Button variant="ghost" size="sm" onClick={() => cancel(b)}>İptal</Button>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
