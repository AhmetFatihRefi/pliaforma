"use client";

import { useCallback, useEffect, useState } from "react";
import { ApiError } from "@/lib/api";
import { attendanceService, bookingService, classSessionService, classTypeService, groupScheduleChangeService, makeupCreditService, studentService } from "@/lib/studioApi";
import { useInstructors } from "@/lib/useInstructors";
import {
  type AttendanceItemDto,
  type AttendanceMarkInput,
  type AttendanceStatus,
  type ClassBookingDto,
  type ClassSessionDto,
  type ClassSessionKind,
  type ChainShiftPreviewDto,
  type ClassTypeDto,
  type DayOfWeek,
  type GroupChangeCandidateDto,
  type GroupChangePreviewDto,
  type MakeupEligibleStudentDto,
  type SessionAttendanceDto,
  type StudentDto,
  attendanceStatusLabels,
  bookingStatusLabels,
  dayLabels,
  daysOfWeek,
  isUnlimitedCapacity,
  sessionKindLabels,
  sessionStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";

const todayStr = () => new Date().toISOString().slice(0, 10);
const addDays = (d: string, n: number) => {
  const dt = new Date(d + "T00:00:00");
  dt.setDate(dt.getDate() + n);
  return dt.toISOString().slice(0, 10);
};
const fmtTime = (iso: string) => iso.slice(11, 16);
const fmtDate = (iso: string) => iso.slice(0, 10);

export default function SchedulePage() {
  const { instructors, isManager } = useInstructors();
  // Sprint 25: Instructor için useInstructors yalnız kendi profilini döndürür → kendi dersini ayırt et.
  const myInstructorId = isManager ? null : instructors[0]?.id ?? null;
  const canConfirm = (s: ClassSessionDto) =>
    isManager || (myInstructorId != null && (s.instructorProfileId === myInstructorId || s.actualInstructorProfileId === myInstructorId));
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [from, setFrom] = useState(todayStr());
  const [to, setTo] = useState(addDays(todayStr(), 7));
  const [instructorFilter, setInstructorFilter] = useState("");
  const [classTypeFilter, setClassTypeFilter] = useState("");
  const [sessions, setSessions] = useState<ClassSessionDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [bulkOpen, setBulkOpen] = useState(false);
  const [groupChangeOpen, setGroupChangeOpen] = useState(false);
  const [bookFor, setBookFor] = useState<ClassSessionDto | null>(null);
  const [bookingsFor, setBookingsFor] = useState<ClassSessionDto | null>(null);
  const [cancelFor, setCancelFor] = useState<ClassSessionDto | null>(null);
  const [rescheduleFor, setRescheduleFor] = useState<ClassSessionDto | null>(null);
  const [attendanceFor, setAttendanceFor] = useState<ClassSessionDto | null>(null);
  const [actualFor, setActualFor] = useState<ClassSessionDto | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);

  useEffect(() => {
    classTypeService.list(true).then(setClassTypes).catch(() => setClassTypes([]));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSessions(await classSessionService.list({
        from, to,
        instructorProfileId: instructorFilter || undefined,
        classTypeId: classTypeFilter || undefined,
      }));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Program yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [from, to, instructorFilter, classTypeFilter]);

  useEffect(() => {
    load();
  }, [load]);

  const grouped = sessions.reduce<Record<string, ClassSessionDto[]>>((acc, s) => {
    (acc[fmtDate(s.startAt)] ??= []).push(s);
    return acc;
  }, {});

  const confirmSession = async (s: ClassSessionDto) => {
    if (!window.confirm("Bu ders onaylanacak ve tamamlandı işaretlenecek.\nİşaretlenmemiş öğrenciler Present (Geldi) kabul edilecek.")) return;
    setBusyId(s.id);
    setError(null);
    try { await classSessionService.confirm(s.id); await load(); }
    catch (e) { setError(e instanceof ApiError ? e.message : "Ders onaylanamadı."); }
    finally { setBusyId(null); }
  };

  const confirmToday = async () => {
    if (!window.confirm("Bugünün başlamış/geçmiş dersleri onaylanacak (gelecek saatli ve iptal dersler atlanır).\nİşaretlenmemiş öğrenciler Present (Geldi) kabul edilecek.")) return;
    setBusyId("today");
    setError(null);
    try {
      const r = await classSessionService.confirmToday();
      setBanner(`${r.confirmedCount} ders onaylandı · ${r.presentMarkedCount} öğrenci Present · ${r.skippedFutureCount} gelecek ders atlandı.`);
      await load();
    } catch (e) { setError(e instanceof ApiError ? e.message : "Dersler onaylanamadı."); }
    finally { setBusyId(null); }
  };

  const markNotDelivered = async (s: ClassSessionDto) => {
    if (!window.confirm("Bu ders 'işlenmedi' olarak işaretlenecek: ders İPTAL edilecek ve öğrencilere telafi hakkı verilecek.\n(Ertelenecekse 'Ertele', başka hoca verdiyse 'Fiilî hoca' kullanın — onlar telafi üretmez.)")) return;
    setBusyId(s.id);
    setError(null);
    try { await classSessionService.notDelivered(s.id); await load(); }
    catch (e) { setError(e instanceof ApiError ? e.message : "İşlem yapılamadı."); }
    finally { setBusyId(null); }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ders Programı</h1>
          <p className="mt-1 text-sm text-muted">Ders seanslarını görüntüleyin ve oluşturun.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {isManager && <Button variant="secondary" loading={busyId === "today"} onClick={confirmToday}>Bugünü onayla</Button>}
          {isManager && <Button variant="secondary" onClick={() => setGroupChangeOpen(true)}>Grup düzeni</Button>}
          <Button variant="secondary" onClick={() => setBulkOpen(true)}>Toplu slot</Button>
          <Button onClick={() => setCreateOpen(true)}>+ Yeni seans</Button>
        </div>
      </div>

      <Card>
        <div className="grid gap-3 sm:grid-cols-4">
          <FormField label="Başlangıç"><Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} /></FormField>
          <FormField label="Bitiş"><Input type="date" value={to} onChange={(e) => setTo(e.target.value)} /></FormField>
          {isManager && (
            <FormField label="Hoca">
              <Select value={instructorFilter} onChange={(e) => setInstructorFilter(e.target.value)}>
                <option value="">Tümü</option>
                {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
              </Select>
            </FormField>
          )}
          <FormField label="Grup">
            <Select value={classTypeFilter} onChange={(e) => setClassTypeFilter(e.target.value)}>
              <option value="">Tümü</option>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
        </div>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}
      {banner && <Alert kind="success">{banner}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : sessions.length === 0 ? (
        <Card><p className="text-sm text-muted">Bu aralıkta ders seansı yok.</p></Card>
      ) : (
        <div className="space-y-4">
          {Object.entries(grouped).map(([date, list]) => (
            <div key={date}>
              <h3 className="mb-2 text-sm font-semibold text-muted">{date}</h3>
              <div className="grid gap-3 sm:grid-cols-2">
                {list.map((s) => (
                  <Card key={s.id}>
                    <div className="flex items-start justify-between">
                      <div>
                        <p className="flex items-center gap-2 font-medium text-foreground">
                          {s.classTypeName}
                          {s.sessionKind === "Makeup" && <Badge tone="rose">{sessionKindLabels.Makeup}</Badge>}
                          {(s.sessionKind === "Trial" || s.sessionKind === "Special") && (
                            <Badge tone="neutral">{sessionKindLabels[s.sessionKind]}</Badge>
                          )}
                          <Badge tone={s.isIndividual ? "warning" : "neutral"}>{s.isIndividual ? "Bireysel" : "Grup"}</Badge>
                          {s.instructorChanged && <Badge tone="rose">Hoca değişti</Badge>}
                        </p>
                        <p className="text-sm text-muted">{s.instructorName} · {fmtTime(s.startAt)}–{fmtTime(s.endAt)}</p>
                        <p className="mt-1 text-xs text-muted">
                          {s.isIndividual
                            ? `Bireysel · Doluluk ${s.bookedCount}/1`
                            : isUnlimitedCapacity(s.capacity)
                              ? `Grup · ${s.bookedCount} öğrenci · limitsiz`
                              : `Grup · Doluluk ${s.bookedCount}/${s.capacity}`}
                          {s.isFull && !isUnlimitedCapacity(s.capacity) && <span className="ml-2 text-danger">· DOLU</span>}
                          {s.rescheduledAt && <span className="ml-2">· ertelendi</span>}
                        </p>
                      </div>
                      <Badge tone={s.status === "Scheduled" ? "success" : s.status === "Cancelled" ? "danger" : "neutral"}>
                        {sessionStatusLabels[s.status]}
                      </Badge>
                    </div>
                    {s.status !== "Cancelled" && (
                      <div className="mt-3 flex flex-wrap gap-2 border-t border-border pt-3">
                        {s.status === "Scheduled" && (
                          <>
                            <Button variant="secondary" size="sm" disabled={s.isFull} onClick={() => setBookFor(s)}>
                              {s.isFull ? "Dolu" : "Öğrenci ekle"}
                            </Button>
                            <Button variant="ghost" size="sm" onClick={() => setRescheduleFor(s)}>Ertele</Button>
                            <Button variant="ghost" size="sm" onClick={() => setCancelFor(s)}>İptal et</Button>
                            {isManager && <Button variant="ghost" size="sm" onClick={() => markNotDelivered(s)}>İşlenmedi</Button>}
                            {canConfirm(s) && <Button size="sm" loading={busyId === s.id} onClick={() => confirmSession(s)}>Onayla</Button>}
                          </>
                        )}
                        <Button variant="ghost" size="sm" onClick={() => setBookingsFor(s)}>Rezervasyonlar</Button>
                        <Button variant="ghost" size="sm" onClick={() => setAttendanceFor(s)}>Yoklama</Button>
                        {isManager && <Button variant="ghost" size="sm" onClick={() => setActualFor(s)}>Fiilî hoca</Button>}
                      </div>
                    )}
                  </Card>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}

      {createOpen && (
        <CreateSessionModal
          instructors={instructors}
          classTypes={classTypes}
          onClose={() => setCreateOpen(false)}
          onDone={() => { setCreateOpen(false); load(); }}
        />
      )}
      {bulkOpen && (
        <BulkModal
          instructors={instructors}
          classTypes={classTypes}
          onClose={() => setBulkOpen(false)}
          onDone={() => { setBulkOpen(false); load(); }}
        />
      )}
      {bookFor && (
        <StaffBookingModal session={bookFor} onClose={() => setBookFor(null)} onDone={() => { setBookFor(null); load(); }} />
      )}
      {bookingsFor && (
        <SessionBookingsModal session={bookingsFor} onClose={() => setBookingsFor(null)} onChanged={() => load()} />
      )}
      {cancelFor && (
        <CancelSessionModal session={cancelFor} onClose={() => setCancelFor(null)} onDone={() => { setCancelFor(null); load(); }} />
      )}
      {rescheduleFor && (
        <RescheduleModal session={rescheduleFor} isManager={isManager} onClose={() => setRescheduleFor(null)} onDone={() => { setRescheduleFor(null); load(); }} />
      )}
      {attendanceFor && (
        <AttendanceModal session={attendanceFor} onClose={() => setAttendanceFor(null)} />
      )}
      {actualFor && (
        <ActualInstructorModal
          session={actualFor}
          instructors={instructors}
          onClose={() => setActualFor(null)}
          onDone={() => { setActualFor(null); load(); }}
        />
      )}
      {groupChangeOpen && (
        <GroupChangeModal
          instructors={instructors}
          classTypes={classTypes}
          onClose={() => setGroupChangeOpen(false)}
          onDone={() => { setGroupChangeOpen(false); load(); }}
        />
      )}
    </div>
  );
}

// Sprint 22: köklü grup düzeni değişikliği — hoca+ders tipi+effectiveDate → öğrenci seç → yeni slot → önizle → uygula.
function GroupChangeModal({ instructors, classTypes, onClose, onDone }: {
  instructors: { id: string; name: string }[]; classTypes: ClassTypeDto[]; onClose: () => void; onDone: () => void;
}) {
  const [instructorId, setInstructorId] = useState(instructors[0]?.id ?? "");
  const [classTypeId, setClassTypeId] = useState(classTypes[0]?.id ?? "");
  const [effectiveDate, setEffectiveDate] = useState(todayStr());
  const [candidates, setCandidates] = useState<GroupChangeCandidateDto[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [days, setDays] = useState<Set<DayOfWeek>>(new Set());
  const [time, setTime] = useState("19:00");
  const [preview, setPreview] = useState<GroupChangePreviewDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const slots = () => Array.from(days).map((d) => ({ dayOfWeek: d, startTime: `${time}:00` }));
  const input = () => ({ instructorProfileId: instructorId, classTypeId, effectiveDate, contractIds: Array.from(selected), newSlots: slots() });
  const reset = () => { setPreview(null); };

  const loadCandidates = async () => {
    setBusy(true); setErr(null); setPreview(null);
    try { setCandidates(await groupScheduleChangeService.candidates(instructorId, classTypeId, effectiveDate)); setSelected(new Set()); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Adaylar getirilemedi."); }
    finally { setBusy(false); }
  };

  const doPreview = async () => {
    setBusy(true); setErr(null);
    try { setPreview(await groupScheduleChangeService.preview(input())); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Önizleme alınamadı."); }
    finally { setBusy(false); }
  };

  const apply = async () => {
    setBusy(true); setErr(null);
    try { await groupScheduleChangeService.apply(input()); onDone(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Uygulanamadı."); }
    finally { setBusy(false); }
  };

  const toggleSel = (id: string) => setSelected((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); reset(); return n; });
  const toggleDay = (d: DayOfWeek) => setDays((s) => { const n = new Set(s); n.has(d) ? n.delete(d) : n.add(d); reset(); return n; });

  const canPreview = selected.size > 0 && days.size > 0;

  return (
    <Modal open onClose={onClose} title="Grup düzeni değiştir">
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-xs text-muted">Seçili öğrencilerin <strong>{effectiveDate} ve sonrası</strong> gelecek dersleri yeni düzene taşınır (telafi üretmez). Seçilmeyen öğrenciler etkilenmez.</p>

        <div className="grid gap-3 sm:grid-cols-3">
          <FormField label="Hoca">
            <Select value={instructorId} onChange={(e) => { setInstructorId(e.target.value); setCandidates(null); }}>
              {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Grup (ClassType)">
            <Select value={classTypeId} onChange={(e) => { setClassTypeId(e.target.value); setCandidates(null); }}>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Geçerlilik tarihi">
            <Input type="date" value={effectiveDate} onChange={(e) => { setEffectiveDate(e.target.value); setCandidates(null); }} />
          </FormField>
        </div>
        <Button size="sm" variant="secondary" loading={busy} onClick={loadCandidates}>Öğrencileri getir</Button>

        {candidates && (
          candidates.length === 0 ? (
            <p className="text-sm text-muted">Bu hoca/ders tipinde gelecek dersi olan öğrenci yok.</p>
          ) : (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-muted">Taşınacak öğrenciler (tek tek seçin):</p>
              <div className="max-h-44 space-y-1 overflow-y-auto">
                {candidates.map((c) => (
                  <label key={c.contractId} className="flex items-center justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                    <span className="flex items-center gap-2">
                      <input type="checkbox" checked={selected.has(c.contractId)} onChange={() => toggleSel(c.contractId)} />
                      <span className="font-medium text-foreground">{c.studentName}</span>
                      {c.isIndividual && <Badge tone="warning">Bireysel</Badge>}
                    </span>
                    <span className="text-xs text-muted">{c.currentSlotSummary} · kalan {c.availableLessonCount} · {c.futureRegularCount} gelecek</span>
                  </label>
                ))}
              </div>

              <p className="text-xs font-semibold text-muted">Yeni gün/saat:</p>
              <div className="flex flex-wrap gap-1">
                {daysOfWeek.map((d) => (
                  <label key={d} className={`cursor-pointer rounded-lg border px-2 py-1 text-xs ${days.has(d) ? "border-primary bg-primary-soft text-primary-hover" : "border-border text-foreground"}`}>
                    <input type="checkbox" className="hidden" checked={days.has(d)} onChange={() => toggleDay(d)} />
                    {dayLabels[d]}
                  </label>
                ))}
                <Input type="time" value={time} onChange={(e) => { setTime(e.target.value); reset(); }} className="w-28" />
              </div>

              {preview && (
                <div className="max-h-52 space-y-1 overflow-y-auto rounded-xl border border-border p-2 text-xs">
                  <p className="font-semibold text-muted">Önizleme · {preview.emptiedSessionsCancelled} boş ders iptal olacak</p>
                  {preview.students.map((s) => (
                    <div key={s.contractId} className="flex items-center justify-between gap-2">
                      <span className="text-foreground">{s.studentName}</span>
                      <span className="text-muted">−{s.removedCount} kaldır · +{s.plannedCount} üret (yeni {s.newSessionCount}, paylaşılan {s.reusedCount})</span>
                      <span className="flex gap-1">
                        {s.conflictCount > 0 && <Badge tone="danger">{s.conflictCount} çakışma</Badge>}
                        {s.note && <Badge tone="warning">{s.note}</Badge>}
                      </span>
                    </div>
                  ))}
                  {!preview.canApply && <p className="text-danger">Çakışma var — uygulanamaz. Slot/tarih değiştirin.</p>}
                </div>
              )}

              <div className="flex justify-end gap-2 pt-2">
                <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
                {!preview ? (
                  <Button loading={busy} disabled={!canPreview} onClick={doPreview}>Önizle</Button>
                ) : (
                  <Button loading={busy} disabled={!preview.canApply} onClick={apply}>Uygula ({selected.size} öğrenci)</Button>
                )}
              </div>
            </div>
          )
        )}
      </div>
    </Modal>
  );
}

const ATT_OPTIONS: { value: AttendanceStatus; label: string }[] = [
  { value: "Present", label: "Geldi" },
  { value: "Absent", label: "Gelmedi" },
  { value: "Excused", label: "Mazeretli" },
  { value: "LateCancel", label: "Geç İptal" },
];

type AttDraft = { status: AttendanceStatus; notes: string; grant: boolean };

function AttendanceModal({ session, onClose }: { session: ClassSessionDto; onClose: () => void }) {
  const [data, setData] = useState<SessionAttendanceDto | null>(null);
  const [draft, setDraft] = useState<Record<string, AttDraft>>({});
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const d = await attendanceService.forSession(session.id);
      setData(d);
      const init: Record<string, AttDraft> = {};
      d.items.forEach((it) => {
        init[it.bookingId] = { status: it.status ?? "Present", notes: it.notes ?? "", grant: false };
      });
      setDraft(init);
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yoklama yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [session.id]);

  useEffect(() => {
    load();
  }, [load]);

  const setItem = (bookingId: string, patch: Partial<AttDraft>) =>
    setDraft((d) => ({ ...d, [bookingId]: { ...d[bookingId], ...patch } }));

  const save = async () => {
    if (!data) return;
    setBusy(true);
    setErr(null);
    try {
      const items: AttendanceMarkInput[] = data.items.map((it) => ({
        bookingId: it.bookingId,
        status: draft[it.bookingId].status,
        notes: draft[it.bookingId].notes || undefined,
        grantMakeupCredit:
          (draft[it.bookingId].status === "Excused" || draft[it.bookingId].status === "LateCancel") && draft[it.bookingId].grant,
      }));
      const updated = await attendanceService.markBatch(session.id, items);
      setData(updated);
      const init: Record<string, AttDraft> = {};
      updated.items.forEach((it) => { init[it.bookingId] = { status: it.status ?? "Present", notes: it.notes ?? "", grant: false }; });
      setDraft(init);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yoklama kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  const canGrant = (it: AttendanceItemDto) => {
    const st = draft[it.bookingId]?.status;
    return st === "Excused" || st === "LateCancel";
  };

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — Yoklama`}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        {loading ? (
          <p className="text-sm text-muted">Yükleniyor…</p>
        ) : !data ? null : !data.canMark ? (
          <Alert kind="info">Yoklama zamanı gelmedi (seans henüz başlamadı veya iptal edildi).</Alert>
        ) : data.items.length === 0 ? (
          <p className="text-sm text-muted">Bu seansta aktif rezervasyon yok.</p>
        ) : (
          <>
            <div className="max-h-[28rem] space-y-2 overflow-y-auto">
              {data.items.map((it) => (
                <div key={it.bookingId} className="rounded-xl border border-border p-3">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-medium text-foreground">{it.studentName}</span>
                    <div className="flex items-center gap-2">
                      {it.creditGranted && <Badge tone="rose">+1 telafi</Badge>}
                      {it.status && <Badge tone="neutral">{attendanceStatusLabels[it.status]}</Badge>}
                    </div>
                  </div>
                  <div className="mt-2 grid gap-2 sm:grid-cols-2">
                    <Select value={draft[it.bookingId]?.status} onChange={(e) => setItem(it.bookingId, { status: e.target.value as AttendanceStatus })}>
                      {ATT_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                    </Select>
                    <Input placeholder="Not (ops.)" value={draft[it.bookingId]?.notes ?? ""} onChange={(e) => setItem(it.bookingId, { notes: e.target.value })} />
                  </div>
                  {canGrant(it) && !it.creditGranted && (
                    <label className="mt-2 flex items-center gap-2 text-xs text-foreground">
                      <input type="checkbox" checked={draft[it.bookingId]?.grant ?? false} onChange={(e) => setItem(it.bookingId, { grant: e.target.checked })} />
                      Telafi kredisi ver
                    </label>
                  )}
                </div>
              ))}
            </div>
            <div className="flex justify-end gap-2 pt-2">
              <Button variant="secondary" onClick={onClose}>Kapat</Button>
              <Button loading={busy} onClick={save}>Yoklamayı kaydet</Button>
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

function ActualInstructorModal({ session, instructors, onClose, onDone }: {
  session: ClassSessionDto; instructors: { id: string; name: string }[]; onClose: () => void; onDone: () => void;
}) {
  const [value, setValue] = useState(session.actualInstructorProfileId ?? "");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    setErr(null);
    try {
      await classSessionService.setActualInstructor(session.id, value || null);
      onDone();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Güncellenemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — Fiilî hoca`}>
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-sm text-muted">
          Planlanan hoca: <strong>{session.instructorName}</strong>. Dersi fiilen başka bir hoca verdiyse seçin —
          ders iptal olmaz, telafi doğmaz; yalnızca rapora ve "hoca değişti" rozetine yansır.
        </p>
        <FormField label="Fiilî hoca">
          <Select value={value} onChange={(e) => setValue(e.target.value)}>
            <option value="">Planlanan hoca verdi ({session.instructorName})</option>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button loading={busy} onClick={submit}>Kaydet</Button>
        </div>
      </div>
    </Modal>
  );
}

function CancelSessionModal({ session, onClose, onDone }: { session: ClassSessionDto; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState("");
  const [grant, setGrant] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await classSessionService.cancel(session.id, { reason: reason || undefined, grantMakeupCredit: grant });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "İptal edilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — Dersi iptal et`}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-sm text-muted">
          Bu seans iptal edilecek; {session.bookedCount} aktif rezervasyon stüdyo iptaliyle kapatılacak.
        </p>
        <FormField label="Gerekçe (ops.)">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} placeholder="örn. hoca rahatsız" />
        </FormField>
        <label className="flex items-center gap-2 text-sm text-foreground">
          <input type="checkbox" checked={grant} onChange={(e) => setGrant(e.target.checked)} />
          Rezervasyon sahiplerine telafi kredisi ver
        </label>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Dersi iptal et</Button>
        </div>
      </form>
    </Modal>
  );
}

const conflictLabel = (r?: string | null) =>
  r === "past" ? "geçmiş zaman" : r === "instructor" ? "hoca çakışması" : r === "student" ? "öğrenci çakışması" : null;

// Sprint 21: tek ders taşıma — mod A (sadece bu ders) / mod B (zincirleme kaydır + önizleme).
function RescheduleModal({ session, onClose, onDone, isManager }: {
  session: ClassSessionDto; onClose: () => void; onDone: () => void; isManager: boolean;
}) {
  const [mode, setMode] = useState<"single" | "chain">("single");
  const [newStartAt, setNewStartAt] = useState(session.startAt.slice(0, 16));
  const [reason, setReason] = useState("");
  const [keepBookings, setKeepBookings] = useState(true);
  const [grant, setGrant] = useState(false);
  const [preview, setPreview] = useState<ChainShiftPreviewDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const setStart = (v: string) => { setNewStartAt(v); setPreview(null); };

  const submitSingle = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setErr(null);
    try {
      await classSessionService.reschedule(session.id, {
        newStartAt, reason: reason || undefined, keepBookings, grantMakeupCredit: !keepBookings && grant,
      });
      onDone();
    } catch (e2) { setErr(e2 instanceof ApiError ? e2.message : "Taşınamadı."); }
    finally { setBusy(false); }
  };

  const doPreview = async () => {
    setBusy(true); setErr(null);
    try { setPreview(await classSessionService.chainShiftPreview(session.id, newStartAt)); }
    catch (e2) { setErr(e2 instanceof ApiError ? e2.message : "Önizleme alınamadı."); }
    finally { setBusy(false); }
  };

  const applyChain = async () => {
    setBusy(true); setErr(null);
    try { await classSessionService.chainShift(session.id, { newStartAt, reason: reason || undefined }); onDone(); }
    catch (e2) { setErr(e2 instanceof ApiError ? e2.message : "Uygulanamadı."); }
    finally { setBusy(false); }
  };

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — Tarih/Saat değiştir`}>
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}

        {isManager && (
          <div className="flex flex-wrap gap-2">
            <Button size="sm" variant={mode === "single" ? "primary" : "secondary"} onClick={() => setMode("single")}>Sadece bu dersi taşı</Button>
            <Button size="sm" variant={mode === "chain" ? "primary" : "secondary"} onClick={() => { setMode("chain"); setPreview(null); }}>Zincirleme kaydır</Button>
          </div>
        )}

        <FormField label="Yeni başlangıç" hint="Bitiş, grup süresinden otomatik hesaplanır">
          <Input type="datetime-local" value={newStartAt} onChange={(e) => setStart(e.target.value)} required />
        </FormField>
        <FormField label="Gerekçe (ops.)">
          <Input value={reason} onChange={(e) => setReason(e.target.value)} />
        </FormField>

        {mode === "single" ? (
          <form onSubmit={submitSingle} className="space-y-3">
            <label className="flex items-center gap-2 text-sm text-foreground">
              <input type="checkbox" checked={keepBookings} onChange={(e) => setKeepBookings(e.target.checked)} />
              Mevcut rezervasyonlar korunsun (seansla taşınır — telafi üretmez)
            </label>
            {!keepBookings && (
              <label className="flex items-center gap-2 text-sm text-foreground">
                <input type="checkbox" checked={grant} onChange={(e) => setGrant(e.target.checked)} />
                Rezervasyonlar iptal edilince telafi kredisi ver
              </label>
            )}
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
              <Button type="submit" loading={busy}>Taşı</Button>
            </div>
          </form>
        ) : (
          <div className="space-y-3">
            <p className="text-xs text-muted">Zincirleme kaydırma telafi üretmez. Sonraki dersler aynı miktarda kayar; boşlukta zincir durur. Çakışma varsa hiçbir ders kaymaz.</p>
            {preview && (
              <div className="max-h-60 space-y-1 overflow-y-auto rounded-xl border border-border p-2">
                <p className="text-xs font-semibold text-muted">{preview.affected.length} ders kayacak:</p>
                {preview.affected.map((it) => (
                  <div key={it.sessionId} className="flex items-center justify-between gap-2 text-xs">
                    <span className="text-foreground">{it.classTypeName}</span>
                    <span className="text-muted">{fmtDate(it.oldStartAt)} {fmtTime(it.oldStartAt)} → {fmtDate(it.newStartAt)} {fmtTime(it.newStartAt)}</span>
                    <span className="flex gap-1">
                      {conflictLabel(it.conflictReason) && <Badge tone="danger">{conflictLabel(it.conflictReason)}</Badge>}
                      {it.outsideAvailability && <Badge tone="warning">müsaitlik dışı</Badge>}
                    </span>
                  </div>
                ))}
                {!preview.canApply && <p className="text-xs text-danger">Çakışma var — zincir uygulanamaz. Saati değiştirin veya tek ders taşıyın.</p>}
              </div>
            )}
            <div className="flex justify-end gap-2 pt-2">
              <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
              {!preview ? (
                <Button loading={busy} onClick={doPreview}>Önizle</Button>
              ) : (
                <Button loading={busy} disabled={!preview.canApply} onClick={applyChain}>Uygula ({preview.affected.length})</Button>
              )}
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
}

function StaffBookingModal({ session, onClose, onDone }: { session: ClassSessionDto; onClose: () => void; onDone: () => void }) {
  // Sprint 20: Telafi (Makeup) seansına YALNIZCA kullanılabilir telafi hakkı olan öğrenciler eklenebilir.
  const isMakeup = session.sessionKind === "Makeup";
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<StudentDto[]>([]);
  const [eligible, setEligible] = useState<MakeupEligibleStudentDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    if (isMakeup) {
      makeupCreditService.eligibleStudents().then(setEligible).catch(() => setEligible([]));
      return;
    }
    const t = setTimeout(async () => {
      try {
        setResults(search.trim() ? await studentService.search(search.trim()) : await studentService.list());
      } catch {
        setResults([]);
      }
    }, 300);
    return () => clearTimeout(t);
  }, [search, isMakeup]);

  const book = async (studentProfileId: string) => {
    setBusyId(studentProfileId);
    setErr(null);
    try {
      await bookingService.create(session.id, studentProfileId);
      onDone();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Rezervasyon yapılamadı.");
    } finally {
      setBusyId(null);
    }
  };

  const q = search.trim().toLowerCase();
  const filteredEligible = eligible.filter((e) => e.studentName.toLowerCase().includes(q));

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — ${isMakeup ? "Telafi dersine öğrenci ekle" : "Öğrenci ekle"}`}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        {isMakeup && <Alert kind="info">Telafi dersine yalnızca kullanılabilir telafi hakkı olan öğrenciler eklenebilir.</Alert>}
        <Input placeholder="Öğrenci ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="max-h-72 space-y-2 overflow-y-auto">
          {isMakeup ? (
            filteredEligible.length === 0 ? (
              <p className="text-sm text-muted">Telafi hakkı olan uygun öğrenci yok.</p>
            ) : (
              filteredEligible.map((e) => (
                <div key={e.studentProfileId} className="flex items-center justify-between rounded-xl border border-border px-3 py-2">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-foreground">{e.studentName}</span>
                    <Badge tone="rose">{e.availableCredits} telafi</Badge>
                  </div>
                  <Button size="sm" disabled={busyId !== null} onClick={() => book(e.studentProfileId)}>Ekle</Button>
                </div>
              ))
            )
          ) : results.length === 0 ? (
            <p className="text-sm text-muted">Öğrenci bulunamadı.</p>
          ) : (
            results.map((s) => (
              <div key={s.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2">
                <div>
                  <span className="text-sm font-medium text-foreground">{s.fullName}</span>
                  {s.status === "Guest" && <Badge tone="warning">Misafir</Badge>}
                  {s.status === "Inactive" && <Badge tone="neutral">Pasif</Badge>}
                </div>
                <Button size="sm" disabled={busyId !== null || s.status === "Inactive"} onClick={() => book(s.id)}>Ekle</Button>
              </div>
            ))
          )}
        </div>
      </div>
    </Modal>
  );
}

function SessionBookingsModal({ session, onClose, onChanged }: { session: ClassSessionDto; onClose: () => void; onChanged: () => void }) {
  const [items, setItems] = useState<ClassBookingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await bookingService.forSession(session.id));
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [session.id]);

  useEffect(() => {
    load();
  }, [load]);

  const cancel = async (b: ClassBookingDto) => {
    try {
      await bookingService.cancel(b.id);
      await load();
      onChanged();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "İptal edilemedi.");
    }
  };

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — Rezervasyonlar`}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        <Table>
          <THead><TR><TH>Öğrenci</TH><TH>Durum</TH><TH className="text-right">İşlem</TH></TR></THead>
          <tbody>
            {loading ? (
              <TableEmpty colSpan={3}>Yükleniyor…</TableEmpty>
            ) : items.length === 0 ? (
              <TableEmpty colSpan={3}>Rezervasyon yok.</TableEmpty>
            ) : (
              items.map((b) => (
                <TR key={b.id}>
                  <TD className="font-medium">{b.studentName}</TD>
                  <TD>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <Badge tone={b.status === "Booked" ? "success" : "neutral"}>{bookingStatusLabels[b.status]}</Badge>
                      {b.requiresPayment && <Badge tone="warning">Ödeme gerekli</Badge>}
                    </div>
                  </TD>
                  <TD className="text-right">
                    {b.status === "Booked" && <Button variant="ghost" size="sm" onClick={() => cancel(b)}>İptal</Button>}
                  </TD>
                </TR>
              ))
            )}
          </tbody>
        </Table>
      </div>
    </Modal>
  );
}

interface ModalProps {
  instructors: { id: string; name: string }[];
  classTypes: ClassTypeDto[];
  onClose: () => void;
  onDone: () => void;
}

function CreateSessionModal({ instructors, classTypes, onClose, onDone }: ModalProps) {
  const [form, setForm] = useState({
    instructorProfileId: instructors[0]?.id ?? "",
    classTypeId: classTypes[0]?.id ?? "",
    startAt: "",
    capacity: "",
    notes: "",
    sessionKind: "Regular" as ClassSessionKind,
  });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await classSessionService.create({
        instructorProfileId: form.instructorProfileId,
        classTypeId: form.classTypeId,
        startAt: form.startAt,
        capacity: form.capacity ? Number(form.capacity) : undefined,
        notes: form.notes || undefined,
        sessionKind: form.sessionKind,
      });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Yeni ders seansı">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Hoca">
          <Select value={form.instructorProfileId} onChange={(e) => setForm((f) => ({ ...f, instructorProfileId: e.target.value }))}>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
        <FormField label="Grup (ClassType)" hint="Süre ve kapasite buradan gelir">
          <Select value={form.classTypeId} onChange={(e) => setForm((f) => ({ ...f, classTypeId: e.target.value }))}>
            {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.defaultDurationMinutes}dk</option>)}
          </Select>
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Başlangıç">
            <Input type="datetime-local" value={form.startAt} onChange={(e) => setForm((f) => ({ ...f, startAt: e.target.value }))} required />
          </FormField>
          <FormField label="Kapasite (ops.)">
            <Input type="number" min={1} value={form.capacity} onChange={(e) => setForm((f) => ({ ...f, capacity: e.target.value }))} placeholder="grup varsayılanı" />
          </FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Tür" hint="Telafi seansı kredi tüketir">
            <Select value={form.sessionKind} onChange={(e) => setForm((f) => ({ ...f, sessionKind: e.target.value as ClassSessionKind }))}>
              <option value="Regular">Normal</option>
              <option value="Makeup">Telafi</option>
              <option value="Trial">Deneme</option>
              <option value="Special">Özel</option>
            </Select>
          </FormField>
          <FormField label="Not (ops.)">
            <Input value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} />
          </FormField>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>İptal</Button>
          <Button type="submit" loading={busy}>Oluştur</Button>
        </div>
      </form>
    </Modal>
  );
}

function BulkModal({ instructors, classTypes, onClose, onDone }: ModalProps) {
  const [form, setForm] = useState({
    instructorProfileId: instructors[0]?.id ?? "",
    classTypeId: classTypes[0]?.id ?? "",
    date: todayStr(),
    rangeStart: "09:00",
    rangeEnd: "12:00",
    breakMinutes: "0",
  });
  const [err, setErr] = useState<string | null>(null);
  const [result, setResult] = useState<{ created: number; skipped: number } | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      const r = await classSessionService.bulkGenerate({
        instructorProfileId: form.instructorProfileId,
        classTypeId: form.classTypeId,
        date: form.date,
        rangeStart: `${form.rangeStart}:00`,
        rangeEnd: `${form.rangeEnd}:00`,
        breakMinutes: form.breakMinutes ? Number(form.breakMinutes) : undefined,
      });
      setResult({ created: r.created.length, skipped: r.skipped.length });
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Slot üretilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Toplu slot oluştur">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        {result && (
          <Alert kind="success">{result.created} seans oluşturuldu, {result.skipped} atlandı.</Alert>
        )}
        <FormField label="Hoca">
          <Select value={form.instructorProfileId} onChange={(e) => setForm((f) => ({ ...f, instructorProfileId: e.target.value }))}>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
        <FormField label="Grup (ClassType)">
          <Select value={form.classTypeId} onChange={(e) => setForm((f) => ({ ...f, classTypeId: e.target.value }))}>
            {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.defaultDurationMinutes}dk</option>)}
          </Select>
        </FormField>
        <div className="grid grid-cols-3 gap-3">
          <FormField label="Tarih"><Input type="date" value={form.date} onChange={(e) => setForm((f) => ({ ...f, date: e.target.value }))} required /></FormField>
          <FormField label="Aralık başı"><Input type="time" value={form.rangeStart} onChange={(e) => setForm((f) => ({ ...f, rangeStart: e.target.value }))} required /></FormField>
          <FormField label="Aralık sonu"><Input type="time" value={form.rangeEnd} onChange={(e) => setForm((f) => ({ ...f, rangeEnd: e.target.value }))} required /></FormField>
        </div>
        <FormField label="Aralarda mola (dk)">
          <Input type="number" min={0} max={240} value={form.breakMinutes} onChange={(e) => setForm((f) => ({ ...f, breakMinutes: e.target.value }))} />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Kapat</Button>
          <Button type="submit" loading={busy}>{result ? "Tekrar üret" : "Üret"}</Button>
          {result && <Button type="button" onClick={onDone}>Bitti</Button>}
        </div>
      </form>
    </Modal>
  );
}
