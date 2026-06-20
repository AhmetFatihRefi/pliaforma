"use client";

import { useCallback, useEffect, useState } from "react";
import { studentFinanceService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type PaymentDto,
  type PaymentStatus,
  paymentMethodLabels,
  paymentStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const tone = (s: PaymentStatus) =>
  s === "Paid" ? "success" : s === "Overdue" ? "danger" : s === "Cancelled" || s === "Refunded" ? "neutral" : "warning";

export default function MyPaymentsPage() {
  const [items, setItems] = useState<PaymentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await studentFinanceService.myPayments());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Ödemeler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Ödemelerim</h1>
        <p className="mt-1 text-sm text-muted">Ödeme durumun ve ödeme günlerin.</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Ödeme kaydın yok.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((p) => (
            <Card key={p.id} className="flex items-center justify-between">
              <div>
                <p className="font-medium text-foreground">{p.amount.toLocaleString("tr-TR")} {p.currency}</p>
                <p className="text-sm text-muted">
                  Vade: {p.dueDate}{p.method ? ` · ${paymentMethodLabels[p.method]}` : ""}
                  {p.paidAt ? ` · Ödendi: ${p.paidAt.slice(0, 10)}` : ""}
                </p>
                <p className="mt-0.5 text-xs text-muted">{p.lineLabel ?? "Diğer ödeme"}</p>
                {p.notes && <p className="mt-1 text-xs text-muted">{p.notes}</p>}
              </div>
              <Badge tone={tone(p.status)}>{paymentStatusLabels[p.status]}</Badge>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
