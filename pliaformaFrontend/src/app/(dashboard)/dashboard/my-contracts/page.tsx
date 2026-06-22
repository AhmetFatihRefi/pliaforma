"use client";

import { useCallback, useEffect, useState } from "react";
import { studentContractService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type StudentContractLineDto,
  type MyDebtSummaryDto,
  studentContractStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;
const statusTone = (s: StudentContractLineDto["status"]) =>
  s === "Active" ? "success" : s === "Completed" ? "neutral" : s === "Cancelled" ? "danger" : "warning";

// Sprint 28B: öğrenci self-servis LINE-bazlı (Sözleşmelerim = satırlar). Yalnız görüntüleme.
export default function MyContractsPage() {
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [debt, setDebt] = useState<MyDebtSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [ls, d] = await Promise.all([
        studentContractService.myLines(),
        studentContractService.myDebt(),
      ]);
      setLines(ls);
      setDebt(d);
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Sözleşmelerim</h1>
        <p className="mt-1 text-sm text-muted">Paketlerin, ders hakkın ve ödeme durumun. (Yalnız görüntüleme.)</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : lines.length === 0 ? (
        <Card><p className="text-sm text-muted">Henüz paketin yok.</p></Card>
      ) : (
        <div className="space-y-4">
          {lines.map((l) => (
            <Card key={l.id} className="space-y-3">
              <div className="flex items-start justify-between gap-2">
                <div>
                  <p className="flex items-center gap-2 font-medium text-foreground">
                    {l.classTypeName}
                    {l.isIndividual ? <Badge tone="warning">Bireysel</Badge> : <Badge tone="neutral">Grup</Badge>}
                  </p>
                  <p className="text-sm text-muted">{l.instructorName} · {l.startDate} → {l.endDate}</p>
                </div>
                <Badge tone={statusTone(l.status)}>{studentContractStatusLabels[l.status]}</Badge>
              </div>

              {/* Sprint 29C: ders hakkı kalan = işlenmiş bazlı; planlanabilir = planlanmış+işlenmiş düşülen kapasite. */}
              <div
                className="grid grid-cols-3 gap-2 text-center sm:grid-cols-5"
                title="Kalan hak, işlenmiş derslere göre hesaplanır. Planlanabilir sayı, planlanmış ve işlenmiş dersler düşüldükten sonra kalan kapasitedir."
              >
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Toplam ders</p><p className="text-lg font-semibold text-foreground">{l.lessonCount}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Planlanmış</p><p className="text-lg font-semibold text-foreground">{l.plannedLessonCount}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">İşlenmiş</p><p className="text-lg font-semibold text-foreground">{l.deliveredLessonCount}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Kalan hak</p><p className="text-lg font-semibold text-foreground">{l.remainingLessonRight}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Planlanabilir</p><p className="text-lg font-semibold text-foreground">{l.availableLessonCount}</p></div>
              </div>

              <div className="rounded-lg bg-surface p-2 text-sm">
                <p className="mb-1 text-xs font-semibold text-muted">Ödeme durumu</p>
                <div className="grid grid-cols-2 gap-x-4 gap-y-1 sm:grid-cols-4">
                  <div><span className="text-muted">Ders başı</span><div className="font-medium text-foreground">{money(l.pricePerLesson, l.currency)}</div></div>
                  <div><span className="text-muted">Toplam tutar</span><div className="font-medium text-foreground">{money(l.totalPrice, l.currency)}</div></div>
                  <div><span className="text-muted">Ödenen</span><div className="font-medium text-foreground">{money(l.paid, l.currency)}</div></div>
                  <div><span className="text-muted">Kalan ödeme</span><div className={`font-semibold ${l.remaining > 0 ? "text-danger" : "text-success"}`}>{money(l.remaining, l.currency)}</div></div>
                </div>
                <p className="mt-1 text-xs text-muted">Son ödeme tarihi: {l.dueDate ?? "Belirlenmedi"} · Telafi hakkı: {l.makeupAllowance}</p>
              </div>
            </Card>
          ))}
        </div>
      )}

      {/* Para birimi bazlı genel özet — tek cross-currency toplam YOK */}
      {debt && debt.byCurrency.length > 0 && (
        <Card className="space-y-2">
          <h3 className="text-sm font-semibold text-foreground">Genel ödeme durumu</h3>
          {debt.byCurrency.map((g) => (
            <div key={g.currency} className="flex items-center justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
              <span className="font-medium text-foreground">{g.currency}</span>
              <span className="text-muted">
                Toplam {money(g.total, g.currency)} · Ödenen {money(g.paid, g.currency)} ·{" "}
                <span className={g.remaining > 0 ? "font-semibold text-danger" : "font-semibold text-success"}>Kalan {money(g.remaining, g.currency)}</span>
              </span>
            </div>
          ))}
          {debt.unassignedByCurrency.length > 0 && (
            <p className="text-xs text-muted">
              Diğer ödemeler: {debt.unassignedByCurrency.map((u) => money(u.paid, u.currency)).join(" · ")} (belirli bir pakete bağlı değil)
            </p>
          )}
        </Card>
      )}
    </div>
  );
}
