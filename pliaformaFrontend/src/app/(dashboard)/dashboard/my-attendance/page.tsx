"use client";

import { useCallback, useEffect, useState } from "react";
import { studentAttendanceService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { type AttendanceSummaryDto, type MyAttendanceDto, attendanceStatusLabels } from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;
const thisMonth = () => new Date().toISOString().slice(0, 7);

const tone = (s: MyAttendanceDto["status"]) =>
  s === "Present" ? "success" : s === "Excused" ? "warning" : s === "LateCancel" ? "rose" : "neutral";

export default function MyAttendancePage() {
  const [items, setItems] = useState<MyAttendanceDto[]>([]);
  const [summary, setSummary] = useState<AttendanceSummaryDto | null>(null);
  const [month, setMonth] = useState(thisMonth());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, sum] = await Promise.all([
        studentAttendanceService.mine(),
        studentAttendanceService.summary(month),
      ]);
      setItems(list);
      setSummary(sum);
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Katılım geçmişi yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [month]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Katılım Geçmişim</h1>
        <p className="mt-1 text-sm text-muted">Derslere katılım durumun.</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Card>
        <div className="flex items-center justify-between gap-3">
          <h3 className="text-sm font-semibold text-foreground">Aylık özet</h3>
          <input
            type="month"
            value={month}
            onChange={(e) => setMonth(e.target.value)}
            className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground"
          />
        </div>
        {summary && (
          <div className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-5">
            <Stat label="Geldi" value={summary.presentCount} />
            <Stat label="Gelmedi" value={summary.absentCount} />
            <Stat label="Mazeretli" value={summary.excusedCount} />
            <Stat label="Geç İptal" value={summary.lateCancelCount} />
            <Stat label="Katılım" value={summary.attendanceRate != null ? `%${Math.round(summary.attendanceRate * 100)}` : "—"} />
          </div>
        )}
      </Card>

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Henüz yoklama kaydın yok.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((a) => (
            <Card key={a.id} className="flex items-center justify-between">
              <div>
                <p className="font-medium text-foreground">{a.classTypeName}</p>
                <p className="text-sm text-muted">{a.instructorName} · {fmt(a.startAt)}</p>
                {a.notes && <p className="mt-1 text-xs text-muted">{a.notes}</p>}
              </div>
              <div className="flex items-center gap-2">
                {a.creditGranted && <Badge tone="rose">+1 telafi</Badge>}
                <Badge tone={tone(a.status)}>{attendanceStatusLabels[a.status]}</Badge>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-xl border border-border px-3 py-2 text-center">
      <p className="text-lg font-semibold text-foreground">{value}</p>
      <p className="text-xs text-muted">{label}</p>
    </div>
  );
}
