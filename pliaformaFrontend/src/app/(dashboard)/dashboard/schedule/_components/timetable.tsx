"use client";

// Ders Programı — haftalık takvim ızgarası. Sütunlar = günler (yukarıda gün + tarih), satırlar = saatler (solda).
// Bir ders tek kayıttır (öğrenci sayısından bağımsız). Aynı saatte birden çok ders → hücre içinde altlı üstlü.
import { useMemo } from "react";
import { type AvailabilityWindowDto, type ClassSessionDto, type MakeupRequestDto, isUnlimitedCapacity, sessionKindLabels } from "@/lib/domain-types";

// Sprint 36: oransal takvim — sol saat sütunu YOK, bloklar süresine orantılı yükseklikte.
const DAY_W = 172; // her gün sütunu (px)
const HOUR_PX = 52; // bir saatin piksel yüksekliği
const MIN_BLOCK_PX = 26; // çok kısa derste bile tıklanabilir minimum yükseklik
const DEFAULT_FROM = 8;
const DEFAULT_TO = 21;

const TR_DAYS = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const TR_DAYS_SHORT = ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"];
const TR_MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

const hourOf = (iso: string) => Number(iso.slice(11, 13));
const minOfDay = (iso: string) => Number(iso.slice(11, 13)) * 60 + Number(iso.slice(14, 16));
const fmtTime = (iso: string) => iso.slice(11, 16);
export const dateKey = (iso: string) => iso.slice(0, 10);

// Oransal yerleşim: çakışan blokları yan yana şeride (lane) dağıt — standart takvim algoritması.
type Laned<T> = { item: T; lane: number; lanes: number };
function assignLanes<T extends { startMin: number; endMin: number }>(items: T[]): Laned<T>[] {
  const sorted = [...items].sort((a, b) => a.startMin - b.startMin || a.endMin - b.endMin);
  const out: Laned<T>[] = [];
  let cluster: { item: T; lane: number }[] = [];
  let laneEnds: number[] = [];
  let clusterEnd = -Infinity;
  const flush = () => {
    const lanes = laneEnds.length;
    for (const c of cluster) out.push({ item: c.item, lane: c.lane, lanes });
    cluster = [];
    laneEnds = [];
  };
  for (const it of sorted) {
    if (it.startMin >= clusterEnd) { flush(); clusterEnd = it.endMin; }
    let lane = laneEnds.findIndex((e) => e <= it.startMin);
    if (lane === -1) { lane = laneEnds.length; laneEnds.push(it.endMin); }
    else laneEnds[lane] = it.endMin;
    cluster.push({ item: it, lane });
    clusterEnd = Math.max(clusterEnd, it.endMin);
  }
  flush();
  return out;
}

function dayHeading(dateStr: string) {
  const d = new Date(dateStr + "T00:00:00");
  return { full: TR_DAYS[d.getDay()], short: TR_DAYS_SHORT[d.getDay()], dm: `${d.getDate()} ${TR_MONTHS[d.getMonth()]}` };
}

// Sadece renk/kenar tonu (display/genişlik içermez) — hem mobil kart hem oransal blok kullanır.
function blockTone(s: ClassSessionDto) {
  if (s.status === "Cancelled") return "border-border bg-surface-muted text-muted line-through opacity-70";
  if (s.status === "Completed") return "border-success/40 bg-success/12 text-foreground";
  if (s.sessionKind === "Makeup") return "border-primary/50 bg-primary-soft text-foreground hover:border-primary";
  return "border-primary/40 bg-primary-soft/40 text-foreground hover:border-primary";
}
function blockClass(s: ClassSessionDto, _selected: boolean) {
  return `block w-full rounded-md border px-1.5 py-1 text-left transition-colors ${blockTone(s)}`;
}

// Mobil: günlük ajanda (dikey liste). DnD yok; tıkla → detay.
export function MobileAgenda({
  days, sessions, requests, availabilityWindows, students, onSelectSession, onSelectRequest, onSelectWindow,
}: {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  availabilityWindows?: AvailabilityWindowDto[];
  students?: Map<string, string[]>;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onSelectWindow?: (w: AvailabilityWindowDto) => void;
}) {
  const todayStr = new Date().toISOString().slice(0, 10);
  const byDay = new Map<string, ClassSessionDto[]>();
  for (const s of sessions) (byDay.get(dateKey(s.startAt)) ?? byDay.set(dateKey(s.startAt), []).get(dateKey(s.startAt))!).push(s);
  const reqByDay = new Map<string, MakeupRequestDto[]>();
  for (const r of requests) (reqByDay.get(dateKey(r.startAt)) ?? reqByDay.set(dateKey(r.startAt), []).get(dateKey(r.startAt))!).push(r);
  // Sprint 36: boşluk pencereleri günlük listede de gösterilir (mobil).
  const winByDay = new Map<string, AvailabilityWindowDto[]>();
  for (const w of availabilityWindows ?? []) (winByDay.get(w.date) ?? winByDay.set(w.date, []).get(w.date)!).push(w);
  for (const v of byDay.values()) v.sort((a, b) => a.startAt.localeCompare(b.startAt));
  for (const v of winByDay.values()) v.sort((a, b) => a.startWindowFrom.localeCompare(b.startWindowFrom));

  return (
    <div className="space-y-4">
      {days.map((date) => {
        const items = byDay.get(date) ?? [];
        const reqs = reqByDay.get(date) ?? [];
        const wins = winByDay.get(date) ?? [];
        if (items.length === 0 && reqs.length === 0 && wins.length === 0) return null;
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
              {wins.map((w, wi) => (
                <button
                  key={`w|${w.date}|${w.startWindowFrom}|${wi}`}
                  type="button"
                  onClick={() => onSelectWindow?.(w)}
                  disabled={!onSelectWindow}
                  className={`block w-full rounded-xl border border-dashed border-success/60 bg-success/10 px-3 py-2 text-left ${onSelectWindow ? "" : "cursor-default"}`}
                >
                  <div className="flex items-center gap-2 truncate text-sm font-semibold text-foreground">
                    <span className="rounded bg-success/20 px-1 text-[10px] font-bold uppercase tracking-wide text-success">Boşluk</span>
                    {availRangeLabel(w)} · {w.durationMinutes} dk
                  </div>
                  <div className="truncate text-xs text-muted">{availCapacityLabel(w)} · {availInstructorLabel(w)}</div>
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

// Sprint 36: boşluk (availability window) kartı etiketleri — ders/telafi kartlarından AYRIŞIR (dashed yeşil + "BOŞLUK").
// Boş aralık = startWindowFrom .. (startWindowTo + süre). Adım = ders süresi olduğundan saatler temiz çıkar.
function availRangeLabel(w: AvailabilityWindowDto) {
  const endMin = Number(w.startWindowTo.slice(11, 13)) * 60 + Number(w.startWindowTo.slice(14, 16)) + w.durationMinutes;
  const end = `${String(Math.floor(endMin / 60)).padStart(2, "0")}:${String(endMin % 60).padStart(2, "0")}`;
  return `${fmtTime(w.startWindowFrom)}–${end}`;
}
function availCapacityLabel(w: AvailabilityWindowDto) {
  return w.availableEquipmentCapacity == null ? "ekipmansız" : `${w.availableEquipmentCapacity} kişi`;
}
function availInstructorLabel(w: AvailabilityWindowDto) {
  const n = w.availableInstructors.length;
  if (n === 0) return "hoca yok";
  if (n === 1) return w.availableInstructors[0].name;
  return `${n} hoca uygun`;
}

export interface TimetableProps {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  /** Sprint 36: "Boşlukları Göster" overlay'i — dashed "Boşluk" bloğu olarak render edilir. */
  availabilityWindows?: AvailabilityWindowDto[];
  selectedId: string | null;
  students?: Map<string, string[]>;
  /** Dikey kaydırma için kapsayıcı yüksekliği (sticky gün başlığı bu kapsayıcıya göre sabitlenir). */
  maxHeight?: string;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onSelectWindow?: (w: AvailabilityWindowDto) => void;
  onEmptyClick?: (dateStr: string, hour: number) => void;
}

// Oransal yerleşim için tek tip blok (ders / telafi / boşluk).
type DayItem = { key: string; startMin: number; endMin: number } & (
  | { kind: "session"; s: ClassSessionDto }
  | { kind: "req"; r: MakeupRequestDto }
  | { kind: "window"; w: AvailabilityWindowDto }
);

export function WeekTimetable({
  days, sessions, requests, availabilityWindows, selectedId, students, maxHeight = "70vh",
  onSelectSession, onSelectRequest, onSelectWindow, onEmptyClick,
}: TimetableProps) {
  const windows = availabilityWindows ?? [];
  const { fromH, toH } = useMemo(() => {
    let lo = DEFAULT_FROM, hi = DEFAULT_TO;
    for (const s of sessions) { lo = Math.min(lo, hourOf(s.startAt)); hi = Math.max(hi, hourOf(s.endAt) + (s.endAt.slice(14, 16) === "00" ? 0 : 1)); }
    for (const r of requests) { lo = Math.min(lo, hourOf(r.startAt)); hi = Math.max(hi, hourOf(r.endAt) + 1); }
    // Sprint 36: erken/geç boşluklar da görünsün; boşluk bitişi = startWindowTo + süre.
    for (const w of windows) { lo = Math.min(lo, hourOf(w.startWindowFrom)); hi = Math.max(hi, hourOf(w.startWindowTo) + Math.ceil(w.durationMinutes / 60) + 1); }
    return { fromH: Math.max(0, Math.min(lo, DEFAULT_FROM)), toH: Math.min(24, Math.max(hi, DEFAULT_TO)) };
  }, [sessions, requests, windows]);

  const hours = Array.from({ length: toH - fromH }, (_, i) => fromH + i);
  const fromMin = fromH * 60;
  const pxPerMin = HOUR_PX / 60;
  const totalHeight = (toH - fromH) * HOUR_PX;

  // Gün → o güne ait tüm bloklar (ders + telafi + boşluk), çakışmalar yan yana şeritlenir.
  const itemsByDay = useMemo(() => {
    const m = new Map<string, DayItem[]>();
    const push = (d: string, it: DayItem) => (m.get(d) ?? m.set(d, []).get(d)!).push(it);
    for (const s of sessions) push(dateKey(s.startAt), { kind: "session", key: s.id, startMin: minOfDay(s.startAt), endMin: Math.max(minOfDay(s.startAt) + 15, minOfDay(s.endAt)), s });
    for (const r of requests) push(dateKey(r.startAt), { kind: "req", key: r.id, startMin: minOfDay(r.startAt), endMin: Math.max(minOfDay(r.startAt) + 15, minOfDay(r.endAt)), r });
    for (const w of windows) push(w.date, { kind: "window", key: `w|${w.date}|${w.startWindowFrom}`, startMin: minOfDay(w.startWindowFrom), endMin: minOfDay(w.startWindowTo) + w.durationMinutes, w });
    return m;
  }, [sessions, requests, windows]);

  const todayStr = new Date().toISOString().slice(0, 10);
  const cols = `repeat(${days.length}, ${DAY_W}px)`;

  return (
    <div className="overflow-auto rounded-2xl border-2 border-border bg-surface" style={{ maxHeight }}>
      <div style={{ minWidth: days.length * DAY_W }}>
        {/* Başlık: gün sütunları (dikey kaydırmada sabit) */}
        <div className="sticky top-0 z-20 grid border-b-2 border-border bg-surface-muted" style={{ gridTemplateColumns: cols }}>
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

        {/* Gövde: gün sütunları — bloklar süresine orantılı, mutlak konumlu */}
        <div className="grid" style={{ gridTemplateColumns: cols }}>
          {days.map((date) => {
            const laid = assignLanes(itemsByDay.get(date) ?? []);
            const isToday = date === todayStr;
            return (
              <div
                key={date}
                className={`relative border-r border-border last:border-r-0 ${isToday ? "bg-primary-soft/10" : ""}`}
                style={{ height: totalHeight }}
                onClick={(e) => {
                  if (!onEmptyClick || e.target !== e.currentTarget) return;
                  const y = e.clientY - e.currentTarget.getBoundingClientRect().top;
                  const hr = Math.min(toH - 1, Math.max(fromH, fromH + Math.floor(y / HOUR_PX)));
                  onEmptyClick(date, hr);
                }}
              >
                {/* Saat çizgileri — etiketsiz, sadece hizalama için */}
                {hours.map((h, i) => (
                  <div key={h} className="pointer-events-none absolute inset-x-0 border-t border-border/50" style={{ top: i * HOUR_PX }} />
                ))}
                {/* Bloklar */}
                {laid.map(({ item, lane, lanes }) => {
                  const top = (item.startMin - fromMin) * pxPerMin;
                  const height = Math.max(MIN_BLOCK_PX, (item.endMin - item.startMin) * pxPerMin);
                  const widthPct = 100 / lanes;
                  const style = { top, height, left: `${lane * widthPct}%`, width: `${widthPct}%` };
                  if (item.kind === "session") {
                    const s = item.s;
                    const selected = s.id === selectedId;
                    const cap = s.isIndividual ? `${s.bookedCount}/1` : isUnlimitedCapacity(s.capacity) ? `${s.bookedCount} · limitsiz` : `${s.bookedCount}/${s.capacity}`;
                    return (
                      <div key={item.key} className="absolute p-0.5" style={style}>
                        <button
                          type="button"
                          onClick={() => onSelectSession(s)}
                          onContextMenu={(e) => { e.preventDefault(); onSelectSession(s); }}
                          title={`${s.classTypeName} · ${s.instructorName} · ${fmtTime(s.startAt)}–${fmtTime(s.endAt)} · ${cap}`}
                          className={`flex h-full w-full cursor-pointer flex-col overflow-hidden rounded-md border px-1.5 py-1 text-left transition-colors ${blockTone(s)} ${selected ? "ring-2 ring-primary ring-offset-1 ring-offset-surface" : ""}`}
                        >
                          <div className="truncate text-[11px] font-semibold leading-tight">{fmtTime(s.startAt)} {s.classTypeName}</div>
                          <div className="truncate text-[10px] text-muted">{s.instructorName}</div>
                          {studentList(students?.get(s.id), "text-[10px]")}
                          <div className="mt-auto flex flex-wrap items-center gap-1 text-[10px] text-muted">
                            <span>{s.isIndividual ? "Bireysel" : "Grup"} · {cap}</span>
                            {s.sessionKind === "Makeup" && <span className="rounded bg-primary/15 px-1 font-bold uppercase text-primary">{sessionKindLabels.Makeup}</span>}
                            {s.isFull && !isUnlimitedCapacity(s.capacity) && <span className="font-semibold text-danger">DOLU</span>}
                          </div>
                        </button>
                      </div>
                    );
                  }
                  if (item.kind === "req") {
                    const r = item.r;
                    return (
                      <div key={item.key} className="absolute p-0.5" style={style}>
                        <button
                          type="button"
                          onClick={() => onSelectRequest?.(r)}
                          onContextMenu={(e) => { e.preventDefault(); onSelectRequest?.(r); }}
                          title={`${r.classTypeName} · ${r.studentName}`}
                          className="flex h-full w-full flex-col overflow-hidden rounded-md border border-dashed border-warning/70 bg-warning/10 px-1.5 py-1 text-left"
                        >
                          <span className="truncate text-[11px] font-semibold leading-tight">{fmtTime(r.startAt)} {r.classTypeName}</span>
                          <span className="truncate text-[10px] text-muted">{r.studentName} · Onay bekliyor</span>
                        </button>
                      </div>
                    );
                  }
                  const w = item.w;
                  return (
                    <div key={item.key} className="absolute p-0.5" style={style}>
                      <button
                        type="button"
                        onClick={() => onSelectWindow?.(w)}
                        disabled={!onSelectWindow}
                        title={`Boşluk · ${availRangeLabel(w)} · ${w.durationMinutes} dk · ${availCapacityLabel(w)} · ${availInstructorLabel(w)}`}
                        className={`flex h-full w-full flex-col overflow-hidden rounded-md border border-dashed border-success/60 bg-success/10 px-1.5 py-1 text-left ${onSelectWindow ? "cursor-pointer hover:border-success" : "cursor-default"}`}
                      >
                        <span className="flex items-center gap-1 truncate text-[11px] font-semibold leading-tight text-foreground">
                          <span className="rounded bg-success/20 px-1 text-[9px] font-bold uppercase tracking-wide text-success">Boşluk</span>
                          {availRangeLabel(w)}
                        </span>
                        <span className="truncate text-[10px] text-muted">{w.durationMinutes} dk · {availCapacityLabel(w)}</span>
                        <span className="truncate text-[10px] text-muted">{availInstructorLabel(w)}</span>
                      </button>
                    </div>
                  );
                })}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
