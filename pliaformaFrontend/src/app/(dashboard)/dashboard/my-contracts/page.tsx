"use client";

import { useCallback, useEffect, useState } from "react";
import { studentContractService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type StudentContractViewDto,
  type StudentAmendmentDto,
  type MyDebtSummaryDto,
  contractAmendmentKindLabels,
  studentContractStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;
const statusTone = (s: StudentContractViewDto["status"]) =>
  s === "Active" ? "success" : s === "Completed" ? "neutral" : s === "Cancelled" ? "danger" : "warning";

// Ek işlemi öğrenciye sade/yumuşak dille özetler (internal alan yok).
function amendmentSummary(a: StudentAmendmentDto): string {
  const parts: string[] = [];
  if (a.additionalLessonCount > 0) parts.push(`+${a.additionalLessonCount} ders eklendi`);
  else if (a.additionalLessonCount < 0) parts.push(`${a.additionalLessonCount} ders düzeltildi`);
  if (a.datesExtended) parts.push(`Bitiş tarihi ${a.newEndDate} olarak uzatıldı`);
  if (a.additionalPrice > 0) parts.push(`Ek ücret: ${money(a.additionalPrice, a.currency)}`);
  else if (a.additionalPrice < 0) parts.push(`İndirim: ${money(a.additionalPrice, a.currency)}`);
  return parts.length ? parts.join(" · ") : contractAmendmentKindLabels[a.kind];
}

export default function MyContractsPage() {
  const [contracts, setContracts] = useState<StudentContractViewDto[]>([]);
  const [debt, setDebt] = useState<MyDebtSummaryDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [cs, d] = await Promise.all([
        studentContractService.myContracts(),
        studentContractService.myDebt(),
      ]);
      setContracts(cs);
      setDebt(d);
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  // contractId → kısa ad (ödeme/borç kartlarında)
  const debtByContract = new Map((debt?.contracts ?? []).map((c) => [c.contractId, c]));

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Sözleşmelerim</h1>
        <p className="mt-1 text-sm text-muted">Sözleşmen, ders hakkın ve ödeme durumun. (Yalnız görüntüleme.)</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : contracts.length === 0 ? (
        <Card><p className="text-sm text-muted">Henüz sözleşmen yok.</p></Card>
      ) : (
        <div className="space-y-4">
          {contracts.map((c) => {
            const cd = debtByContract.get(c.id);
            return (
              <Card key={c.id} className="space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium text-foreground">{c.classTypeName}</p>
                    <p className="text-sm text-muted">{c.instructorName} · {c.startDate} → {c.endDate}</p>
                  </div>
                  <Badge tone={statusTone(c.status)}>{studentContractStatusLabels[c.status]}</Badge>
                </div>

                {/* Ders hakkı */}
                <div className="grid grid-cols-3 gap-2 text-center">
                  <div className="rounded-lg border border-border p-2">
                    <p className="text-xs text-muted">Toplam ders</p>
                    <p className="text-lg font-semibold text-foreground">{c.totalLessonCount}</p>
                  </div>
                  <div className="rounded-lg border border-border p-2">
                    <p className="text-xs text-muted">Planlanmış</p>
                    <p className="text-lg font-semibold text-foreground">{c.scheduledLessonCount}</p>
                  </div>
                  <div className="rounded-lg border border-border p-2">
                    <p className="text-xs text-muted">Kalan planlanabilir</p>
                    <p className="text-lg font-semibold text-foreground">{c.availableLessonCount}</p>
                  </div>
                </div>

                {/* Ödeme durumu (yumuşak dil) */}
                {cd && (
                  <div className="rounded-lg bg-surface p-2 text-sm">
                    <p className="mb-1 text-xs font-semibold text-muted">Ödeme durumu</p>
                    <div className="grid grid-cols-2 gap-x-4 gap-y-1 sm:grid-cols-4">
                      <div><span className="text-muted">Toplam tutar</span><div className="font-medium text-foreground">{money(cd.totalCharges, cd.currency)}</div></div>
                      <div><span className="text-muted">Ek ücretler</span><div className="font-medium text-foreground">{money(cd.additionalCharges, cd.currency)}</div></div>
                      <div><span className="text-muted">Ödenen</span><div className="font-medium text-foreground">{money(cd.paid, cd.currency)}</div></div>
                      <div><span className="text-muted">Kalan ödeme</span><div className={`font-semibold ${cd.outstanding > 0 ? "text-danger" : "text-success"}`}>{money(cd.outstanding, cd.currency)}</div></div>
                    </div>
                  </div>
                )}

                {/* Ek işlem geçmişi (yumuşak; kim yaptığı gösterilmez) */}
                {c.amendments.length > 0 && (
                  <div className="space-y-1 border-t border-border pt-2">
                    <p className="text-xs font-semibold text-muted">Ek işlem geçmişi</p>
                    {c.amendments.map((a, i) => (
                      <div key={i} className="flex items-center justify-between gap-2 text-xs">
                        <span className="text-foreground">
                          {amendmentSummary(a)}
                          {a.description && <span className="text-muted"> — {a.description}</span>}
                        </span>
                        <span className="shrink-0 text-muted">{a.createdAt.slice(0, 10)}</span>
                      </div>
                    ))}
                  </div>
                )}
              </Card>
            );
          })}
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
                Toplam {money(g.totalCharges, g.currency)} · Ödenen {money(g.totalPaid, g.currency)} ·{" "}
                <span className={g.totalOutstanding > 0 ? "font-semibold text-danger" : "font-semibold text-success"}>
                  Kalan {money(g.totalOutstanding, g.currency)}
                </span>
              </span>
            </div>
          ))}
          {debt.untaggedByCurrency.length > 0 && (
            <p className="text-xs text-muted">
              Diğer ödemeler:{" "}
              {debt.untaggedByCurrency.map((u) => money(u.paid, u.currency)).join(" · ")}{" "}
              (belirli bir sözleşmeye bağlı değil)
            </p>
          )}
        </Card>
      )}
    </div>
  );
}
