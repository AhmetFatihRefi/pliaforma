"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ApiError } from "@/lib/api";
import { attendanceService, bookingCandidateService, bookingService, classSessionService, classTypeService, groupScheduleChangeService, makeupCreditService, makeupRequestService } from "@/lib/studioApi";
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
  type MakeupEligibleLineDto,
  type MakeupRequestDto,
  type RegularCandidateDto,
  type SessionAttendanceDto,
  type StudentDto,
  type StudentLevel,
  attendanceStatusLabels,
  bookingStatusLabels,
  dayLabels,
  daysOfWeek,
  studentLevelLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Table, TableEmpty, TD, TH, THead, TR, useConfirm, useToast } from "@/components/ui";
import { errorMessage } from "@/lib/errors";
import { WeekTimetable, MobileAgenda } from "./_components/timetable";
import { SessionDetailDrawer } from "./_components/detail-drawer";

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
  // Sprint 27: onay bekleyen telafi talepleri (Instructor → kendi; Owner/StudioAdmin → tümü) — programda gösterilir.
  const [pendingReqs, setPendingReqs] = useState<MakeupRequestDto[]>([]);
  const [bookings, setBookings] = useState<ClassBookingDto[]>([]);
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
  const [detailFor, setDetailFor] = useState<ClassSessionDto | null>(null);
  const [fullscreen, setFullscreen] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const confirm = useConfirm();
  const toast = useToast();
  // Sprint 26: seçili hoca filtresi adı (boş/dolu programdan anlaşılır mesajı için).
  const selectedInstructorName = instructorFilter ? instructors.find((i) => i.id === instructorFilter)?.name ?? null : null;

  useEffect(() => {
    classTypeService.list(true).then(setClassTypes).catch(() => setClassTypes([]));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [s, reqs, bks] = await Promise.all([
        classSessionService.list({
          from, to,
          instructorProfileId: instructorFilter || undefined,
          classTypeId: classTypeFilter || undefined,
        }),
        // Sprint 27: onay bekleyen telafi talepleri (rol-scope backend'de). Hata olursa programı bozma.
        makeupRequestService.list("Pending").catch(() => [] as MakeupRequestDto[]),
        // Programda öğrencileri göstermek için aktif (Booked) rezervasyonlar.
        bookingService.list({ status: "Booked" }).catch(() => [] as ClassBookingDto[]),
      ]);
      setSessions(s);
      setPendingReqs(reqs);
      setBookings(bks);
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

  // Sprint 27: telafi talepleri seçili tarih aralığında + (varsa) hoca/ders tipi filtresine göre süzülür.
  const fromD = from, toD = to;
  const visibleReqs = pendingReqs.filter((r) => {
    const d = fmtDate(r.startAt);
    if (d < fromD || d > toD) return false;
    if (instructorFilter && r.requestedInstructorProfileId !== instructorFilter) return false;
    if (classTypeFilter && r.classTypeId !== classTypeFilter) return false;
    return true;
  });

  // Timetable: seçili aralıktaki TÜM günler (boş günler de satır olarak görünür).
  const days = useMemo(() => {
    const out: string[] = [];
    const start = new Date(from + "T00:00:00");
    const end = new Date(to + "T00:00:00");
    for (let d = start; d <= end; d.setDate(d.getDate() + 1)) out.push(new Date(d).toISOString().slice(0, 10));
    return out.length > 0 && out.length <= 62 ? out : [from];
  }, [from, to]);

  const shiftWeek = (deltaDays: number) => {
    setFrom((f) => addDays(f, deltaDays));
    setTo((t) => addDays(t, deltaDays));
  };
  const goThisWeek = () => { setFrom(todayStr()); setTo(addDays(todayStr(), 7)); };

  // ---- Sürükle-bırak (taşıma) + kaydedilmemiş değişiklikler ----
  const [moves, setMoves] = useState<Record<string, { date: string; hour: number }>>({});
  const [quickAt, setQuickAt] = useState<string | null>(null);
  const movedIds = useMemo(() => new Set(Object.keys(moves)), [moves]);

  const isoDate = (iso: string) => iso.slice(0, 10);
  const isoHour = (iso: string) => Number(iso.slice(11, 13));
  const durationMin = (s: ClassSessionDto) => Math.max(0, Math.round((new Date(s.endAt).getTime() - new Date(s.startAt).getTime()) / 60000));
  const movedStart = (s: ClassSessionDto, date: string, hour: number) => `${date}T${String(hour).padStart(2, "0")}:${s.startAt.slice(14, 16)}:00`;
  const addMinutes = (iso: string, min: number) => {
    const d = new Date(iso); d.setMinutes(d.getMinutes() + min);
    const p = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:00`;
  };

  // Optimistik: taşınan dersleri yeni konumlarıyla göster.
  const displaySessions = useMemo(() => sessions.map((s) => {
    const mv = moves[s.id];
    if (!mv) return s;
    const start = movedStart(s, mv.date, mv.hour);
    return { ...s, startAt: start, endAt: addMinutes(start, durationMin(s)) };
  }), [sessions, moves]);

  // Seans → kayıtlı öğrenci adları (programda + drawer'da göstermek için).
  const studentsBySession = useMemo(() => {
    const m = new Map<string, string[]>();
    for (const b of bookings) {
      const arr = m.get(b.classSessionId) ?? m.set(b.classSessionId, []).get(b.classSessionId)!;
      arr.push(b.studentName);
    }
    return m;
  }, [bookings]);

  const onDropSession = (id: string, date: string, hour: number) => {
    const s = sessions.find((x) => x.id === id);
    if (!s || s.status !== "Scheduled") return;
    if (isoDate(s.startAt) === date && isoHour(s.startAt) === hour) {
      setMoves((m) => { const { [id]: _drop, ...rest } = m; return rest; }); // orijinal hücreye geri → değişikliği kaldır
    } else {
      setMoves((m) => ({ ...m, [id]: { date, hour } }));
    }
  };

  const discardMoves = () => setMoves({});
  const saveMoves = async () => {
    const entries = Object.entries(moves);
    if (entries.length === 0) return;
    setBusyId("save-moves");
    const fails: string[] = [];
    for (const [id, mv] of entries) {
      const s = sessions.find((x) => x.id === id);
      if (!s) continue;
      try { await classSessionService.reschedule(id, { newStartAt: movedStart(s, mv.date, mv.hour), keepBookings: true }); }
      catch (e) { fails.push(`${s.classTypeName} (${fmtTime(s.startAt)}): ${errorMessage(e, "taşınamadı")}`); }
    }
    setMoves({}); setBusyId(null);
    await load();
    if (fails.length) toast.error(`Bazı dersler taşınamadı — ${fails.join(" · ")}`);
    else toast.success(`${entries.length} ders taşındı.`);
  };

  const withdrawRequest = async (r: MakeupRequestDto) => {
    if (!(await confirm({ title: "Telafi talebini geri çek", tone: "danger", confirmLabel: "Geri çek", message: "Bu telafi talebini geri çekmek istiyor musunuz? Saat boşalır, rezerve kredi serbest kalır." }))) return;
    setBusyId(r.id);
    try { await makeupRequestService.withdraw(r.id); await load(); toast.success("Telafi talebi geri çekildi."); }
    catch (e) { toast.error(errorMessage(e, "Geri çekilemedi.")); }
    finally { setBusyId(null); }
  };

  const confirmSession = async (s: ClassSessionDto) => {
    if (!(await confirm({ title: "Dersi onayla", message: "Bu ders onaylanacak ve tamamlandı işaretlenecek. İşaretlenmemiş öğrenciler Present (Geldi) kabul edilecek.", confirmLabel: "Onayla" }))) return;
    setBusyId(s.id);
    try { await classSessionService.confirm(s.id); await load(); toast.success("Ders onaylandı."); }
    catch (e) { toast.error(errorMessage(e, "Ders onaylanamadı.")); }
    finally { setBusyId(null); }
  };

  // Sprint 29C: ders onayını geri al (Completed → Scheduled). İşlenmiş ders sayımı düşer, kalan hak geri gelir.
  const unconfirmSession = async (s: ClassSessionDto) => {
    if (!(await confirm({ title: "Onayı geri al", message: "Bu dersin onayı geri alınacak (Planlı durumuna döner). İşlenmiş ders sayımı düşer; yoklama kayıtları korunur.", confirmLabel: "Geri al" }))) return;
    setBusyId(s.id);
    try { await classSessionService.unconfirm(s.id); await load(); toast.success("Ders onayı geri alındı."); }
    catch (e) { toast.error(errorMessage(e, "Onay geri alınamadı.")); }
    finally { setBusyId(null); }
  };

  const confirmToday = async () => {
    if (!(await confirm({ title: "Bugünü onayla", confirmLabel: "Onayla", message: "Bugünün başlamış/geçmiş dersleri onaylanacak (gelecek saatli ve iptal dersler atlanır). İşaretlenmemiş öğrenciler Present (Geldi) kabul edilecek." }))) return;
    setBusyId("today");
    try {
      const r = await classSessionService.confirmToday();
      await load();
      toast.success(`${r.confirmedCount} ders onaylandı · ${r.presentMarkedCount} öğrenci Present · ${r.skippedFutureCount} gelecek ders atlandı.`);
    } catch (e) { toast.error(errorMessage(e, "Dersler onaylanamadı.")); }
    finally { setBusyId(null); }
  };

  const markNotDelivered = async (s: ClassSessionDto) => {
    if (!(await confirm({ title: "Ders işlenmedi", tone: "danger", confirmLabel: "İşlenmedi olarak işaretle", message: "Bu ders 'işlenmedi' olarak işaretlenecek: ders İPTAL edilecek ve öğrencilere telafi hakkı verilecek. (Ertelenecekse 'Ertele', başka hoca verdiyse 'Fiilî hoca' kullanın — onlar telafi üretmez.)" }))) return;
    setBusyId(s.id);
    try { await classSessionService.notDelivered(s.id); await load(); toast.success("Ders 'işlenmedi' olarak işaretlendi; öğrencilere telafi hakkı verildi."); }
    catch (e) { toast.error(errorMessage(e, "İşlem yapılamadı.")); }
    finally { setBusyId(null); }
  };

  return (
    <div className="mx-auto max-w-7xl space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ders Programı</h1>
          <p className="mt-1 text-sm text-muted">Haftalık ders planı — derse tıklayıp yönetin, sürükleyip taşıyın.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {/* Sprint 30: "+ Telafi talebi" Telafi Talepleri sayfasına taşındı (hoca + yönetici oradan oluşturur). */}
          {isManager && <Button variant="secondary" loading={busyId === "today"} onClick={confirmToday}>Bugünü onayla</Button>}
          {isManager && <Button variant="secondary" onClick={() => setGroupChangeOpen(true)}>Grup düzeni</Button>}
          {/* Sprint 26: ders atama yalnız stüdyo yetkilisi (Owner/StudioAdmin). */}
          {isManager && <Button variant="secondary" onClick={() => setBulkOpen(true)}>Toplu ders</Button>}
          {isManager && <Button onClick={() => setCreateOpen(true)}>+ Yeni seans</Button>}
        </div>
      </div>

      {/* Araç çubuğu: hafta navigasyonu + tarih aralığı + filtreler + tam ekran */}
      <Card>
        <div className="flex flex-wrap items-center gap-3">
          <div className="flex items-center gap-1">
            <Button variant="ghost" size="sm" onClick={() => shiftWeek(-7)} aria-label="Önceki hafta">‹</Button>
            <Button variant="secondary" size="sm" onClick={goThisWeek}>Bu hafta</Button>
            <Button variant="ghost" size="sm" onClick={() => shiftWeek(7)} aria-label="Sonraki hafta">›</Button>
          </div>
          <div className="flex items-center gap-2 text-sm">
            <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-40" />
            <span className="text-muted">–</span>
            <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-40" />
          </div>
          {isManager && (
            <Select value={instructorFilter} onChange={(e) => setInstructorFilter(e.target.value)} className="w-44">
              <option value="">Tüm hocalar</option>
              {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
            </Select>
          )}
          <Select value={classTypeFilter} onChange={(e) => setClassTypeFilter(e.target.value)} className="w-44">
            <option value="">Tüm ders tipleri</option>
            {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
          <Button variant="ghost" size="sm" className="ml-auto" onClick={() => setFullscreen(true)}>⛶ Tam ekran</Button>
        </div>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}

      {/* Sprint 26: hoca filtresi seçiliyse o hocanın programı gösterilir (boş/dolu saatler buradan anlaşılır). */}
      {selectedInstructorName && (
        <p className="text-sm text-muted">
          <strong className="text-foreground">{selectedInstructorName}</strong> hocanın programı gösteriliyor — dolu saatler aşağıdaki dersler, boş saatler ders olmayan saatlerdir.
        </p>
      )}

      {loading ? (
        <Card><p className="text-sm text-muted">Yükleniyor…</p></Card>
      ) : (
        <>
          {/* Kaydedilmemiş taşıma çubuğu */}
          {movedIds.size > 0 && (
            <div className="sticky top-2 z-20 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-warning/60 bg-warning/10 px-3 py-2 shadow-sm">
              <span className="text-sm font-medium text-foreground">{movedIds.size} ders taşındı — değişiklikler kaydedilmedi.</span>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" onClick={discardMoves}>Vazgeç</Button>
                <Button size="sm" loading={busyId === "save-moves"} onClick={saveMoves}>Kaydet ({movedIds.size})</Button>
              </div>
            </div>
          )}
          {/* Masaüstü/tablet: takvim ızgarası (sürükle-bırak) */}
          <div className="hidden md:block">
            <WeekTimetable
              days={days}
              sessions={displaySessions}
              requests={visibleReqs}
              selectedId={detailFor?.id ?? null}
              draggable={isManager}
              movedIds={movedIds}
              students={studentsBySession}
              onSelectSession={setDetailFor}
              onSelectRequest={(r) => { if (!isManager) withdrawRequest(r); }}
              onDropSession={isManager ? onDropSession : undefined}
              onEmptyClick={isManager ? (date, hour) => setQuickAt(`${date}T${String(hour).padStart(2, "0")}:00`) : undefined}
            />
          </div>
          {/* Mobil: günlük ajanda */}
          <div className="md:hidden">
            <MobileAgenda
              days={days}
              sessions={displaySessions}
              requests={visibleReqs}
              students={studentsBySession}
              onSelectSession={setDetailFor}
              onSelectRequest={(r) => { if (!isManager) withdrawRequest(r); }}
            />
          </div>
          {sessions.length === 0 && visibleReqs.length === 0 && (
            <p className="text-center text-sm text-muted">
              {selectedInstructorName ? `${selectedInstructorName} hocanın bu aralıkta dersi yok.` : "Bu aralıkta ders seansı yok."}
              {isManager && " Üstten “+ Yeni seans” ile veya boş bir hücreye tıklayarak ekleyebilirsiniz."}
            </p>
          )}
        </>
      )}

      {/* Tam ekran timetable overlay */}
      {fullscreen && (
        <div className="fixed inset-0 z-40 flex flex-col bg-background p-4">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-1">
              <Button variant="ghost" size="sm" onClick={() => shiftWeek(-7)} aria-label="Önceki hafta">‹</Button>
              <Button variant="secondary" size="sm" onClick={goThisWeek}>Bu hafta</Button>
              <Button variant="ghost" size="sm" onClick={() => shiftWeek(7)} aria-label="Sonraki hafta">›</Button>
              <span className="ml-2 text-sm text-muted">{from} – {to}</span>
            </div>
            <Button variant="ghost" size="sm" onClick={() => setFullscreen(false)}>✕ Kapat</Button>
          </div>
          {movedIds.size > 0 && (
            <div className="mb-2 flex items-center justify-between gap-2 rounded-xl border border-warning/60 bg-warning/10 px-3 py-2">
              <span className="text-sm font-medium text-foreground">{movedIds.size} ders taşındı — kaydedilmedi.</span>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" onClick={discardMoves}>Vazgeç</Button>
                <Button size="sm" loading={busyId === "save-moves"} onClick={saveMoves}>Kaydet ({movedIds.size})</Button>
              </div>
            </div>
          )}
          <div className="min-h-0 flex-1">
            <WeekTimetable
              days={days}
              sessions={displaySessions}
              requests={visibleReqs}
              selectedId={detailFor?.id ?? null}
              draggable={isManager}
              movedIds={movedIds}
              students={studentsBySession}
              maxHeight="100%"
              onSelectSession={setDetailFor}
              onSelectRequest={(r) => { if (!isManager) withdrawRequest(r); }}
              onDropSession={isManager ? onDropSession : undefined}
              onEmptyClick={isManager ? (date, hour) => setQuickAt(`${date}T${String(hour).padStart(2, "0")}:00`) : undefined}
            />
          </div>
        </div>
      )}

      {/* Ders detay drawer'ı (tıklayınca açılır) */}
      <SessionDetailDrawer
        session={detailFor}
        isManager={isManager}
        canConfirm={detailFor ? canConfirm(detailFor) : false}
        busy={busyId === detailFor?.id}
        students={detailFor ? studentsBySession.get(detailFor.id) : undefined}
        onClose={() => setDetailFor(null)}
        actions={{
          book: () => { const s = detailFor!; setDetailFor(null); setBookFor(s); },
          reschedule: () => { const s = detailFor!; setDetailFor(null); setRescheduleFor(s); },
          cancel: () => { const s = detailFor!; setDetailFor(null); setCancelFor(s); },
          notDelivered: () => { const s = detailFor!; setDetailFor(null); markNotDelivered(s); },
          confirm: () => { const s = detailFor!; setDetailFor(null); confirmSession(s); },
          unconfirm: () => { const s = detailFor!; setDetailFor(null); unconfirmSession(s); },
          bookings: () => { const s = detailFor!; setDetailFor(null); setBookingsFor(s); },
          attendance: () => { const s = detailFor!; setDetailFor(null); setAttendanceFor(s); },
          actual: () => { const s = detailFor!; setDetailFor(null); setActualFor(s); },
        }}
        onSaveNotes={async (notes) => {
          const s = detailFor!;
          const updated = await classSessionService.update(s.id, { notes: notes || null });
          setDetailFor(updated);
          toast.success("Ders notu kaydedildi.");
          await load();
        }}
      />

      {createOpen && (
        <CreateSessionModal
          instructors={instructors}
          classTypes={classTypes}
          onClose={() => setCreateOpen(false)}
          onDone={() => { setCreateOpen(false); load(); }}
        />
      )}
      {/* Boş hücreye tıklayınca hızlı oluşturma (gün+saat ön-dolu) */}
      {quickAt && (
        <CreateSessionModal
          instructors={instructors}
          classTypes={classTypes}
          initialStartAt={quickAt}
          onClose={() => setQuickAt(null)}
          onDone={() => { setQuickAt(null); load(); }}
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
  const [level, setLevel] = useState<StudentLevel | "">(""); // Sprint 28C-1: opsiyonel seviye filtresi
  const [candidates, setCandidates] = useState<GroupChangeCandidateDto[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [days, setDays] = useState<Set<DayOfWeek>>(new Set());
  const [time, setTime] = useState("19:00");
  const [preview, setPreview] = useState<GroupChangePreviewDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const slots = () => Array.from(days).map((d) => ({ dayOfWeek: d, startTime: `${time}:00` }));
  // Sprint 28C-1: seçim birimi lineId.
  const input = () => ({ instructorProfileId: instructorId, classTypeId, effectiveDate, lineIds: Array.from(selected), newSlots: slots() });
  const reset = () => { setPreview(null); };

  const loadCandidates = async () => {
    setBusy(true); setErr(null); setPreview(null);
    try { setCandidates(await groupScheduleChangeService.candidates(instructorId, classTypeId, effectiveDate, level || null)); setSelected(new Set()); }
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
          <FormField label="Ders grubu">
            <Select value={classTypeId} onChange={(e) => { setClassTypeId(e.target.value); setCandidates(null); }}>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Geçerlilik tarihi">
            <Input type="date" value={effectiveDate} onChange={(e) => { setEffectiveDate(e.target.value); setCandidates(null); }} />
          </FormField>
          <FormField label="Seviye (opsiyonel)">
            <Select value={level} onChange={(e) => { setLevel(e.target.value as StudentLevel | ""); setCandidates(null); }}>
              <option value="">Tüm seviyeler</option>
              {(Object.keys(studentLevelLabels) as StudentLevel[]).map((lv) => <option key={lv} value={lv}>{studentLevelLabels[lv]}</option>)}
            </Select>
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
                  <label key={c.lineId} className="flex items-center justify-between gap-2 rounded-lg border border-border px-3 py-2 text-sm">
                    <span className="flex items-center gap-2">
                      <input type="checkbox" checked={selected.has(c.lineId)} onChange={() => toggleSel(c.lineId)} />
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
                    <div key={s.lineId} className="flex items-center justify-between gap-2">
                      <span className="text-foreground">{s.studentName}</span>
                      <span className="text-muted">−{s.removedCount} kaldır · +{s.plannedCount} üret (yeni {s.newSessionCount}, paylaşılan {s.reusedCount})</span>
                      <span className="flex gap-1">
                        {s.conflictCount > 0 && <Badge tone="danger">{s.conflictCount} çakışma</Badge>}
                        {s.note && <Badge tone="warning">{s.note}</Badge>}
                      </span>
                    </div>
                  ))}
                  {!preview.canApply && <p className="text-danger">Çakışma var — uygulanamaz. Gün/saat veya tarihi değiştirin.</p>}
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
  // Sprint 29B: aday öğrenciler backend candidate uçlarından gelir (Regular: tercih gün/saat sıralı; Makeup: telafi bazlı).
  const isMakeup = session.sessionKind === "Makeup";
  const [search, setSearch] = useState("");
  const [level, setLevel] = useState<StudentLevel | "">("");
  const [regular, setRegular] = useState<RegularCandidateDto[]>([]);
  const [makeup, setMakeup] = useState<MakeupEligibleLineDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true); setErr(null);
    const lv = level || null;
    const p = isMakeup
      ? bookingCandidateService.makeup(session.id, lv).then((r) => { if (!cancelled) { setRegular([]); setMakeup(r); } })
      // Regular: uygun adaylar (tercih gün/saat) + AYRICA bu ders tipinde telafi hakkı olanlar (renkli gösterilir).
      : Promise.all([
          bookingCandidateService.regular(session.id, lv),
          makeupCreditService.eligibleStudents(lv).catch(() => [] as MakeupEligibleLineDto[]),
        ]).then(([reg, elig]) => {
          if (cancelled) return;
          setRegular(reg);
          const regIds = new Set(reg.map((r) => r.studentProfileId));
          setMakeup(elig.filter((e) => e.classTypeId === session.classTypeId && !regIds.has(e.studentProfileId)));
        });
    p.catch((e) => { if (!cancelled) setErr(e instanceof ApiError ? e.message : "Aday listesi yüklenemedi."); })
     .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [session.id, level, isMakeup]);

  // Her satır o satırın LineId'iyle booking yapar (line seçtirme modalı yok — her uygun line ayrı satır).
  const book = async (studentProfileId: string, lineId: string) => {
    setBusyId(lineId); setErr(null);
    try { await bookingService.create(session.id, studentProfileId, lineId); onDone(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Rezervasyon yapılamadı."); setBusyId(null); }
  };

  const q = search.trim().toLowerCase();
  const fmtTime = (t?: string | null) => (t ? t.slice(0, 5) : null);
  const filteredRegular = regular.filter((c) => c.studentName.toLowerCase().includes(q));
  const filteredMakeup = makeup.filter((c) => c.studentName.toLowerCase().includes(q));

  return (
    <Modal open onClose={onClose} title={`${session.classTypeName} — ${isMakeup ? "Telafi dersine öğrenci ekle" : "Öğrenci ekle"}`}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        {isMakeup
          ? <Alert kind="info">Telafi: kullanılabilir telafi hakkı olan öğrenciler (tercih gün/saatten bağımsız).</Alert>
          : <Alert kind="info">Üstte uygun adaylar (tercih gün/saatine göre sıralı); altta <span className="font-medium text-primary">telafi hakkıyla eklenebilecekler</span> renkli gösterilir.</Alert>}
        <div className="flex items-center gap-2">
          <Input placeholder="Öğrenci ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
          <Select value={level} onChange={(e) => setLevel(e.target.value as StudentLevel | "")} className="w-40">
            <option value="">Tüm seviyeler</option>
            {(Object.keys(studentLevelLabels) as StudentLevel[]).map((lv) => <option key={lv} value={lv}>{studentLevelLabels[lv]}</option>)}
          </Select>
        </div>
        <div className="max-h-72 space-y-2 overflow-y-auto">
          {loading ? (
            <p className="text-sm text-muted">Yükleniyor…</p>
          ) : isMakeup ? (
            filteredMakeup.length === 0 ? (
              <p className="text-sm text-muted">Bu derse uygun telafi hakkı olan öğrenci yok.</p>
            ) : (
              filteredMakeup.map((c) => (
                <div key={c.lineId} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-sm font-medium text-foreground">{c.studentName}</span>
                      <Badge tone="rose">{c.availableCredits} telafi</Badge>
                      {c.level && <Badge tone="neutral">{studentLevelLabels[c.level]}</Badge>}
                      <Badge tone={c.isIndividual ? "warning" : "neutral"}>{c.isIndividual ? "Bireysel" : "Grup"}</Badge>
                    </div>
                    <p className="mt-0.5 text-xs text-muted">{c.lineLabel}</p>
                  </div>
                  <Button size="sm" disabled={busyId !== null} onClick={() => book(c.studentProfileId, c.lineId)}>Ekle</Button>
                </div>
              ))
            )
          ) : (filteredRegular.length === 0 && filteredMakeup.length === 0) ? (
            <p className="text-sm text-muted">Bu derse uygun aday öğrenci yok.</p>
          ) : (
            <>
              {filteredRegular.map((c) => (
                <div key={c.lineId} className="flex items-center justify-between gap-2 rounded-xl border border-border px-3 py-2">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-sm font-medium text-foreground">{c.studentName}</span>
                      <Badge tone="neutral">kalan {c.availableForPlanning}</Badge>
                      {c.studentLevel && <Badge tone="neutral">{studentLevelLabels[c.studentLevel]}</Badge>}
                      <Badge tone={c.isIndividual ? "warning" : "neutral"}>{c.isIndividual ? "Bireysel" : "Grup"}</Badge>
                      {!c.hasPreferredSlots ? (
                        <Badge tone="warning">tercih yok</Badge>
                      ) : c.nearestPreferredTime ? (
                        <Badge tone="success">tercih {fmtTime(c.nearestPreferredTime)} · ±{c.minutesFromSession} dk</Badge>
                      ) : (
                        <Badge tone="success">tercih: saat farketmez</Badge>
                      )}
                    </div>
                    <p className="mt-0.5 text-xs text-muted">{c.lineLabel}</p>
                  </div>
                  <Button size="sm" disabled={busyId !== null} onClick={() => book(c.studentProfileId, c.lineId)}>Ekle</Button>
                </div>
              ))}
              {/* Telafi hakkıyla eklenebilecekler — renkli (rose) ayrı bölüm. */}
              {filteredMakeup.length > 0 && (
                <>
                  <p className="px-1 pt-2 text-xs font-semibold text-primary">Telafi hakkıyla eklenebilecekler</p>
                  {filteredMakeup.map((c) => (
                    <div key={c.lineId} className="flex items-center justify-between gap-2 rounded-xl border border-primary/50 bg-primary-soft/50 px-3 py-2">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-1.5">
                          <span className="text-sm font-medium text-foreground">{c.studentName}</span>
                          <Badge tone="rose">{c.availableCredits} telafi hakkı</Badge>
                          {c.level && <Badge tone="neutral">{studentLevelLabels[c.level]}</Badge>}
                          <Badge tone={c.isIndividual ? "warning" : "neutral"}>{c.isIndividual ? "Bireysel" : "Grup"}</Badge>
                        </div>
                        <p className="mt-0.5 text-xs text-muted">{c.lineLabel}</p>
                      </div>
                      <Button size="sm" variant="secondary" disabled={busyId !== null} onClick={() => book(c.studentProfileId, c.lineId)}>Ekle</Button>
                    </div>
                  ))}
                </>
              )}
            </>
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

function CreateSessionModal({ instructors, classTypes, onClose, onDone, initialStartAt }: ModalProps & { initialStartAt?: string }) {
  const [form, setForm] = useState({
    instructorProfileId: instructors[0]?.id ?? "",
    classTypeId: classTypes[0]?.id ?? "",
    startAt: initialStartAt ?? "",
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
      setErr(e2 instanceof ApiError ? e2.message : "Dersler oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Toplu ders oluştur">
      <form onSubmit={submit} className="space-y-4">
        <p className="text-sm text-muted">Seçilen gün ve saat aralığında, mola süresine göre art arda dersler oluşturur.</p>
        {err && <Alert kind="error">{err}</Alert>}
        {result && (
          <Alert kind="success">{result.created} ders oluşturuldu, {result.skipped} atlandı.</Alert>
        )}
        <FormField label="Hoca">
          <Select value={form.instructorProfileId} onChange={(e) => setForm((f) => ({ ...f, instructorProfileId: e.target.value }))}>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
        <FormField label="Ders grubu">
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
          <Button type="submit" loading={busy}>{result ? "Tekrar oluştur" : "Oluştur"}</Button>
          {result && <Button type="button" onClick={onDone}>Bitti</Button>}
        </div>
      </form>
    </Modal>
  );
}
