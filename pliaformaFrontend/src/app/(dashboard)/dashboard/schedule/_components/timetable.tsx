"use client";

// Ders Programı — haftalık takvim ızgarası. Sütunlar = günler (yukarıda gün + tarih), satırlar = saatler (solda).
// Bir ders tek kayıttır (öğrenci sayısından bağımsız). Aynı saatte birden çok ders → hücre içinde altlı üstlü.
import { useMemo } from "react";
import { type ClassSessionDto, type MakeupRequestDto, isUnlimitedCapacity, sessionKindLabels } from "@/lib/domain-types";

const HOUR_W = 60; // sol saat sütunu (px)
const DAY_W = 158; // her gün sütunu (px)
const DEFAULT_FROM = 8;
const DEFAULT_TO = 21;

const TR_DAYS = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const TR_DAYS_SHORT = ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"];
const TR_MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

const hourOf = (iso: string) => Number(iso.slice(11, 13));
const fmtTime = (iso: string) => iso.slice(11, 16);
export const dateKey = (iso: string) => iso.slice(0, 10);

function dayHeading(dateStr: string) {
  const d = new Date(dateStr + "T00:00:00");
  return { full: TR_DAYS[d.getDay()], short: TR_DAYS_SHORT[d.getDay()], dm: `${d.getDate()} ${TR_MONTHS[d.getMonth()]}` };
}

function blockClass(s: ClassSessionDto, selected: boolean) {
  const base = "block w-full rounded-md border px-1.5 py-1 text-left transition-colors";
  if (s.status === "Cancelled") return `${base} border-border bg-surface-muted text-muted line-through opacity-70`;
  if (s.status === "Completed") return `${base} border-success/40 bg-success/12 text-foreground`;
  if (s.sessionKind === "Makeup") return `${base} border-primary/50 bg-primary-soft text-foreground hover:border-primary`;
  return `${base} border-primary/40 bg-primary-soft/40 text-foreground hover:border-primary`;
}

// Mobil: günlük ajanda (dikey liste). DnD yok; tıkla → detay.
export function MobileAgenda({
  days, sessions, requests, students, onSelectSession, onSelectRequest,
}: {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  students?: Map<string, string[]>;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
}) {
  const todayStr = new Date().toISOString().slice(0, 10);
  const byDay = new Map<string, ClassSessionDto[]>();
  for (const s of sessions) (byDay.get(dateKey(s.startAt)) ?? byDay.set(dateKey(s.startAt), []).get(dateKey(s.startAt))!).push(s);
  const reqByDay = new Map<string, MakeupRequestDto[]>();
  for (const r of requests) (reqByDay.get(dateKey(r.startAt)) ?? reqByDay.set(dateKey(r.startAt), []).get(dateKey(r.startAt))!).push(r);
  for (const v of byDay.values()) v.sort((a, b) => a.startAt.localeCompare(b.startAt));

  return (
    <div className="space-y-4">
      {days.map((date) => {
        const items = byDay.get(date) ?? [];
        const reqs = reqByDay.get(date) ?? [];
        if (items.length === 0 && reqs.length === 0) return null;
        const h = dayHeading(date);
        const isToday = date === todayStr;
        return (
          <div key={date}>
            <h3 className={`mb-2 text-sm font-semibold ${isToday ? "text-primary" : "text-foreground"}`}>{h.full}, {h.dm}{isToday && " · bugün"}</h3>
            <div className="space-y-2">
              {items.map((s) => {
                const cap = s.isIndividual ? `${s.bookedCount}/1` : isUnlimitedCapacity(s.capacity) ? `${s.bookedCount} · limitsiz` : `${s.bookedCount}/${s.capacity}`;
                return (
                  <button key={s.id} type="button" onClick={() => onSelectSession(s)} className={`flex w-full items-center justify-between gap-2 rounded-xl border px-3 py-2 text-left ${blockClass(s, false).replace("px-1.5 py-1", "")}`}>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold">{fmtTime(s.startAt)}–{fmtTime(s.endAt)} · {s.classTypeName}</div>
                      <div className="truncate text-xs text-muted">{s.instructorName} · {s.isIndividual ? "Bireysel" : "Grup"} {cap}</div>
                      {studentList(students?.get(s.id), "text-xs")}
                    </div>
                    {s.sessionKind === "Makeup" && <span className="shrink-0 rounded bg-primary/15 px-1 text-[10px] font-bold uppercase text-primary">{sessionKindLabels.Makeup}</span>}
                  </button>
                );
              })}
              {reqs.map((r) => (
                <button key={r.id} type="button" onClick={() => onSelectRequest?.(r)} className="block w-full rounded-xl border border-dashed border-warning/70 bg-warning/10 px-3 py-2 text-left">
                  <div className="truncate text-sm font-semibold text-foreground">{fmtTime(r.startAt)}–{fmtTime(r.endAt)} · {r.classTypeName}</div>
                  <div className="truncate text-xs text-muted">{r.studentName} · Onay bekliyor</div>
                </button>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// En fazla 4 öğrenci adı (her biri ayrı satır) gösterilir; fazlası detaya bırakılır.
function studentList(names: string[] | undefined, textClass: string) {
  if (!names || names.length === 0) return null;
  const head = names.slice(0, 4);
  const rest = names.length - head.length;
  return (
    <div className={`mt-0.5 space-y-px ${textClass}`}>
      {head.map((n, i) => (
        <div key={i} className="truncate text-foreground/80">• {n}</div>
      ))}
      {rest > 0 && <div className="truncate font-medium text-muted">+{rest} kişi · detayda</div>}
    </div>
  );
}

export interface TimetableProps {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  selectedId: string | null;
  draggable?: boolean;
  movedIds?: Set<string>;
  students?: Map<string, string[]>;
  /** Dikey kaydırma için kapsayıcı yüksekliği (sticky gün başlığı bu kapsayıcıya göre sabitlenir). */
  maxHeight?: string;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onEmptyClick?: (dateStr: string, hour: number) => void;
  onDropSession?: (sessionId: string, dateStr: string, hour: number) => void;
}

export function WeekTimetable({
  days, sessions, requests, selectedId, draggable, movedIds, students, maxHeight = "70vh",
  onSelectSession, onSelectRequest, onEmptyClick, onDropSession,
}: TimetableProps) {
  const { fromH, toH } = useMemo(() => {
    let lo = DEFAULT_FROM, hi = DEFAULT_TO;
    for (const s of sessions) { lo = Math.min(lo, hourOf(s.startAt)); hi = Math.max(hi, hourOf(s.endAt) + (s.endAt.slice(14, 16) === "00" ? 0 : 1)); }
    for (const r of requests) { lo = Math.min(lo, hourOf(r.startAt)); hi = Math.max(hi, hourOf(r.endAt) + 1); }
    return { fromH: Math.max(0, Math.min(lo, DEFAULT_FROM)), toH: Math.min(24, Math.max(hi, DEFAULT_TO)) };
  }, [sessions, requests]);

  const hours = Array.from({ length: toH - fromH }, (_, i) => fromH + i);

  // (gün, saat) → o saatte BAŞLAYAN dersler / talepler
  const cell = useMemo(() => {
    const m = new Map<string, { sessions: ClassSessionDto[]; reqs: MakeupRequestDto[] }>();
    const key = (d: string, h: number) => `${d}|${h}`;
    const ensure = (k: string) => m.get(k) ?? m.set(k, { sessions: [], reqs: [] }).get(k)!;
    for (const s of sessions) ensure(key(dateKey(s.startAt), hourOf(s.startAt))).sessions.push(s);
    for (const r of requests) ensure(key(dateKey(r.startAt), hourOf(r.startAt))).reqs.push(r);
    for (const v of m.values()) v.sessions.sort((a, b) => a.startAt.localeCompare(b.startAt));
    return m;
  }, [sessions, requests]);

  const todayStr = new Date().toISOString().slice(0, 10);
  const gridCols = `${HOUR_W}px repeat(${days.length}, ${DAY_W}px)`;

  return (
    <div className="overflow-auto rounded-2xl border border-border bg-surface" style={{ maxHeight }}>
      <div style={{ minWidth: HOUR_W + days.length * DAY_W }}>
        {/* Başlık: köşe + gün sütunları (dikey kaydırmada sabit) */}
        <div className="sticky top-0 z-20 grid border-b border-border bg-surface-muted" style={{ gridTemplateColumns: gridCols }}>
          {/* Köşe: hem üstte hem solda sabit (her iki eksende kaydırmada yerinde kalır) */}
          <div className="sticky left-0 z-30 border-r border-border bg-surface-muted" />
          {days.map((date) => {
            const h = dayHeading(date);
            const isToday = date === todayStr;
            return (
              <div key={date} className={`border-r border-border px-2 py-2 text-center last:border-r-0 ${isToday ? "bg-primary-soft" : ""}`}>
                <div className={`text-sm font-semibold ${isToday ? "text-primary" : "text-foreground"}`}>{h.full}</div>
                <div className="text-xs text-muted">{h.dm}{isToday && " · bugün"}</div>
              </div>
            );
          })}
        </div>

        {/* Saat satırları */}
        {hours.map((hr) => (
          <div key={hr} className="grid border-b border-border last:border-b-0" style={{ gridTemplateColumns: gridCols }}>
            {/* Saat etiketi (yatay kaydırmada solda sabit) */}
            <div className="sticky left-0 z-10 flex items-start justify-end border-r border-border bg-surface-muted px-2 py-1 text-xs font-medium text-muted">
              {String(hr).padStart(2, "0")}:00
            </div>
            {/* Gün hücreleri */}
            {days.map((date) => {
              const c = cell.get(`${date}|${hr}`);
              const empty = !c || (c.sessions.length === 0 && c.reqs.length === 0);
              const isToday = date === todayStr;
              return (
                <div
                  key={date}
                  className={`min-h-14 space-y-1 border-r border-border p-1 last:border-r-0 ${isToday ? "bg-primary-soft/15" : ""}`}
                  onClick={(e) => { if (onEmptyClick && empty && e.target === e.currentTarget) onEmptyClick(date, hr); }}
                  onDragOver={(e) => { if (onDropSession) { e.preventDefault(); e.currentTarget.classList.add("ring-2", "ring-primary", "ring-inset"); } }}
                  onDragLeave={(e) => e.currentTarget.classList.remove("ring-2", "ring-primary", "ring-inset")}
                  onDrop={(e) => {
                    e.currentTarget.classList.remove("ring-2", "ring-primary", "ring-inset");
                    const id = e.dataTransfer.getData("text/session");
                    if (id && onDropSession) onDropSession(id, date, hr);
                  }}
                >
                  {c?.sessions.map((s) => {
                    const selected = s.id === selectedId;
                    const moved = movedIds?.has(s.id);
                    const cap = s.isIndividual ? `${s.bookedCount}/1` : isUnlimitedCapacity(s.capacity) ? `${s.bookedCount} · limitsiz` : `${s.bookedCount}/${s.capacity}`;
                    return (
                      <button
                        key={s.id}
                        type="button"
                        draggable={draggable && s.status === "Scheduled"}
                        onDragStart={(e) => { e.dataTransfer.setData("text/session", s.id); e.dataTransfer.effectAllowed = "move"; }}
                        onClick={() => onSelectSession(s)}
                        title={`${s.classTypeName} · ${s.instructorName} · ${fmtTime(s.startAt)}–${fmtTime(s.endAt)} · ${cap}`}
                        className={`${blockClass(s, selected)} ${selected ? "ring-2 ring-primary ring-offset-1 ring-offset-surface" : ""} ${moved ? "ring-2 ring-warning" : ""} ${draggable && s.status === "Scheduled" ? "cursor-grab active:cursor-grabbing" : "cursor-pointer"}`}
                      >
                        <div className="truncate text-[11px] font-semibold leading-tight">{fmtTime(s.startAt)} {s.classTypeName}</div>
                        <div className="truncate text-[10px] text-muted">{s.instructorName}</div>
                        {studentList(students?.get(s.id), "text-[10px]")}
                        <div className="flex flex-wrap items-center gap-1 text-[10px] text-muted">
                          <span>{s.isIndividual ? "Bireysel" : "Grup"} · {cap}</span>
                          {s.sessionKind === "Makeup" && <span className="rounded bg-primary/15 px-1 font-bold uppercase text-primary">{sessionKindLabels.Makeup}</span>}
                          {s.isFull && !isUnlimitedCapacity(s.capacity) && <span className="font-semibold text-danger">DOLU</span>}
                          {moved && <span className="rounded bg-warning/20 px-1 font-bold uppercase text-warning">taşındı</span>}
                        </div>
                      </button>
                    );
                  })}
                  {c?.reqs.map((r) => (
                    <button
                      key={r.id}
                      type="button"
                      onClick={() => onSelectRequest?.(r)}
                      title={`${r.classTypeName} · ${r.studentName}`}
                      className="block w-full rounded-md border border-dashed border-warning/70 bg-warning/10 px-1.5 py-1 text-left"
                    >
                      <span className="block truncate text-[11px] font-semibold leading-tight">{fmtTime(r.startAt)} {r.classTypeName}</span>
                      <span className="block truncate text-[10px] text-muted">{r.studentName} · Onay bekliyor</span>
                    </button>
                  ))}
                </div>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
}
