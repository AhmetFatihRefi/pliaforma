"use client";

import { useCallback, useEffect, useState } from "react";
import { classTypeService, studentBookingService, studentMakeupCreditService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { type ClassTypeDto, type StudentSessionDto, sessionKindLabels } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Select } from "@/components/ui";

const todayStr = () => new Date().toISOString().slice(0, 10);
const addDays = (d: string, n: number) => {
  const dt = new Date(d + "T00:00:00");
  dt.setDate(dt.getDate() + n);
  return dt.toISOString().slice(0, 10);
};
const fmtTime = (iso: string) => iso.slice(11, 16);
const fmtDate = (iso: string) => iso.slice(0, 10);

export default function BookClassesPage() {
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [from, setFrom] = useState(todayStr());
  const [to, setTo] = useState(addDays(todayStr(), 14));
  const [classTypeId, setClassTypeId] = useState("");
  const [sessions, setSessions] = useState<StudentSessionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [credit, setCredit] = useState(0);

  useEffect(() => {
    classTypeService.list(true).then(setClassTypes).catch(() => setClassTypes([]));
  }, []);

  const loadCredit = useCallback(() => {
    studentMakeupCreditService.mine().then((c) => setCredit(c.balance)).catch(() => setCredit(0));
  }, []);

  useEffect(() => {
    loadCredit();
  }, [loadCredit]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSessions(await studentBookingService.bookableSessions({ from, to, classTypeId: classTypeId || undefined }));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Dersler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [from, to, classTypeId]);

  useEffect(() => {
    load();
  }, [load]);

  const book = async (s: StudentSessionDto) => {
    setBusyId(s.id);
    setError(null);
    try {
      await studentBookingService.book(s.id);
      await load();
      loadCredit();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Rezervasyon yapılamadı.");
    } finally {
      setBusyId(null);
    }
  };

  const grouped = sessions.reduce<Record<string, StudentSessionDto[]>>((acc, s) => {
    (acc[fmtDate(s.startAt)] ??= []).push(s);
    return acc;
  }, {});

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ders Bul</h1>
          <p className="mt-1 text-sm text-muted">Uygun derslere rezervasyon yapın.</p>
        </div>
        <Badge tone={credit > 0 ? "rose" : "neutral"}>Telafi kredisi: {credit}</Badge>
      </div>

      <Card>
        <div className="grid gap-3 sm:grid-cols-3">
          <FormField label="Başlangıç"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FormField>
          <FormField label="Bitiş"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FormField>
          <FormField label="Grup">
            <Select value={classTypeId} onChange={(e) => setClassTypeId(e.target.value)}>
              <option value="">Tümü</option>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
        </div>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : sessions.length === 0 ? (
        <Card><p className="text-sm text-muted">Bu aralıkta uygun ders yok.</p></Card>
      ) : (
        <div className="space-y-4">
          {Object.entries(grouped).map(([date, list]) => (
            <div key={date}>
              <h3 className="mb-2 text-sm font-semibold text-muted">{date}</h3>
              <div className="grid gap-3 sm:grid-cols-2">
                {list.map((s) => (
                  <Card key={s.id} className="flex items-center justify-between">
                    <div>
                      <p className="flex items-center gap-2 font-medium text-foreground">
                        {s.classTypeName}
                        {s.requiresMakeupCredit && <Badge tone="rose">{sessionKindLabels.Makeup}</Badge>}
                      </p>
                      <p className="text-sm text-muted">{s.instructorName} · {fmtTime(s.startAt)}–{fmtTime(s.endAt)}</p>
                      <p className="mt-1 text-xs text-muted">
                        Kalan {s.remainingCapacity}/{s.capacity}
                        {s.requiresMakeupCredit && <span className="ml-2">· 1 telafi kredisi</span>}
                        {!s.requiresMakeupCredit && s.packageEligible && s.expectedPackageName && (
                          <span className="ml-2">· Paket: {s.expectedPackageName} ({s.availableCredit} hak)</span>
                        )}
                      </p>
                    </div>
                    {s.alreadyBooked ? (
                      <Badge tone="success">Rezerve edildi</Badge>
                    ) : s.isFull ? (
                      <Badge tone="neutral">Dolu</Badge>
                    ) : s.requiresMakeupCredit && credit <= 0 ? (
                      <span className="text-xs text-muted">Telafi krediniz yok</span>
                    ) : !s.requiresMakeupCredit && !s.packageEligible ? (
                      <span className="max-w-[9rem] text-right text-xs text-warning">{s.packageEligibilityMessage ?? "Paketiniz uygun değil"}</span>
                    ) : (
                      <Button size="sm" loading={busyId === s.id} onClick={() => book(s)}>Rezerve et</Button>
                    )}
                  </Card>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
