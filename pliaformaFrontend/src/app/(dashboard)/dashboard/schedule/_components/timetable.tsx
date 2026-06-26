"use client";

// Ders Programı — haftalık AKIŞ (flow) takvimi.
// Sprint 37: oransal/absolute model TERK EDİLDİ (kısa dersler ufalıyor, çakışanlar dar şeride bölününce sığmıyordu).
// Yeni yapı: her gün bir sütun; öğeler zaman sırasına dizilir; KARTLAR İÇERİĞE GÖRE TAM SIĞAR (kırpılma yok).
// Aynı anda (zamanı çakışan) dersler yan yana DEĞİL, tek bir "eşzamanlı grup" kutusunda altlı üstlü toplanır →
// bir arada oldukları netçe hissedilir. Boşluk (availability window) kartları da aynı gruba katılır.
import { useMemo } from "react";
import { type AvailabilityWindowDto, type ClassSessionDto, type DayOfWeek, type MakeupRequestDto, type WorkingHourDayDto, isUnlimitedCapacity, sessionKindLabels } from "@/lib/domain-types";

const DAY_W = 208; // gün sütunu genişliği (px) — kartlar tam genişlik kullanır
const ADD_HOUR = 9; // gün başlığından "+" ile hızlı ekleme varsayılan saati

const TR_DAYS = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const TR_MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];

const minOfDay = (iso: string) => Number(iso.slice(11, 13)) * 60 + Number(iso.slice(14, 16));
const fmtTime = (iso: string) => iso.slice(11, 16);
export const dateKey = (iso: string) => iso.slice(0, 10);
// Yerel takvim "bugün" (toISOString UTC'ye kaydırdığı için kullanılmaz; seanslar duvar-saati anahtarlı).
const localToday = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`; };

function dayHeading(dateStr: string) {
  const d = new Date(dateStr + "T00:00:00");
  return { full: TR_DAYS[d.getDay()], dm: `${d.getDate()} ${TR_MONTHS[d.getMonth()]}` };
}

// Sprint 37: tarihten haftanın günü → çalışma saati etiketi ("09:00–22:00" / "Kapalı").
const JS_DOW: DayOfWeek[] = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
type WorkingHoursMap = Map<DayOfWeek, WorkingHourDayDto>;
function toWorkingHoursMap(days?: WorkingHourDayDto[]): WorkingHoursMap | undefined {
  if (!days) return undefined;
  return new Map(days.map((d) => [d.dayOfWeek, d]));
}
function workingHoursLabel(dateStr: string, wh?: WorkingHoursMap): { closed: boolean; label: string } | null {
  if (!wh) return null;
  const d = wh.get(JS_DOW[new Date(dateStr + "T00:00:00").getDay()]);
  if (!d) return null;
  if (d.isClosed) return { closed: true, label: "Kapalı" };
  return { closed: false, label: `${(d.opensAt ?? "").slice(0, 5)}–${(d.closesAt ?? "").slice(0, 5)}` };
}

// ---- Boşluk (availability window) etiketleri ----
// startWindowFrom/To = geçerli BAŞLANGIÇ aralığı (gerçek müsaitlik geçişleri). Etiket bu aralığı gösterir:
// "07:00–15:10" → bu saatler arasında başlayan {süre} dk'lık bir ders bu hoca kümesiyle uygundur.
function availRangeLabel(w: AvailabilityWindowDto) {
  return `${fmtTime(w.startWindowFrom)}–${fmtTime(w.startWindowTo)}`;
}
const availCapacityLabel = (w: AvailabilityWindowDto) => (w.availableEquipmentCapacity == null ? "ekipmansız" : `${w.availableEquipmentCapacity} kişi`);
function availInstructorLabel(w: AvailabilityWindowDto) {
  const n = w.availableInstructors.length;
  if (n === 0) return "uygun hoca yok";
  if (n === 1) return w.availableInstructors[0].name;
  return `${n} hoca uygun`;
}

function capLabel(s: ClassSessionDto) {
  return s.isIndividual ? `${s.bookedCount}/1` : isUnlimitedCapacity(s.capacity) ? `${s.bookedCount} · limitsiz` : `${s.bookedCount}/${s.capacity}`;
}

// Sol aksan çubuğu (border-l) + dolgu ile kategori bir bakışta ayrışır; dark/light uyumlu.
function blockTone(s: ClassSessionDto) {
  if (s.status === "Cancelled") return "border-border border-l-[3px] border-l-muted/50 bg-surface-muted text-muted line-through opacity-70 hover:opacity-90";
  if (s.status === "Completed") return "border-success/25 border-l-[3px] border-l-success bg-success/12 text-foreground hover:bg-success/20";
  if (s.sessionKind === "Makeup") return "border-warning/30 border-l-[3px] border-l-warning bg-warning/12 text-foreground hover:bg-warning/20";
  return "border-primary/25 border-l-[3px] border-l-primary bg-primary-soft text-foreground hover:brightness-[0.98] dark:hover:brightness-110";
}

// En fazla 4 öğrenci adı; fazlası detaya bırakılır.
function StudentList({ names, size }: { names?: string[]; size: string }) {
  if (!names || names.length === 0) return null;
  const head = names.slice(0, 4);
  const rest = names.length - head.length;
  return (
    <div className={`mt-0.5 space-y-px ${size}`}>
      {head.map((n, i) => (
        <div key={i} className="truncate text-foreground/75">• {n}</div>
      ))}
      {rest > 0 && <div className="truncate font-medium text-muted">+{rest} kişi · detayda</div>}
    </div>
  );
}

/* ---------------------------------------------------------------- ortak kartlar */
// Kartlar TAM GENİŞLİK + OTOMATİK YÜKSEKLİK → içerik her zaman tam sığar.

function SessionCard({ s, selected, names, onSelect }: {
  s: ClassSessionDto; selected: boolean; names?: string[]; onSelect: (s: ClassSessionDto) => void;
}) {
  const cap = capLabel(s);
  return (
    <button
      type="button"
      onClick={() => onSelect(s)}
      onContextMenu={(e) => { e.preventDefault(); onSelect(s); }}
      title={`${s.classTypeName} · ${s.instructorName} · ${fmtTime(s.startAt)}–${fmtTime(s.endAt)} · ${cap}`}
      className={`flex w-full flex-col gap-0.5 rounded-lg border px-2 py-1.5 text-left transition-all ${blockTone(s)} ${selected ? "ring-2 ring-primary ring-offset-1 ring-offset-surface" : ""}`}
    >
      <div className="flex items-start justify-between gap-1.5">
        <span className="text-[11px] font-semibold leading-tight text-foreground/65">{fmtTime(s.startAt)}–{fmtTime(s.endAt)}</span>
        <span className="flex shrink-0 items-center gap-1">
          {s.sessionKind === "Makeup" && <span className="rounded bg-warning/20 px-1 text-[9px] font-bold uppercase tracking-wide text-warning">{sessionKindLabels.Makeup}</span>}
          {s.isFull && !isUnlimitedCapacity(s.capacity) && <span className="rounded bg-danger/15 px-1 text-[9px] font-bold uppercase tracking-wide text-danger">Dolu</span>}
        </span>
      </div>
      <div className="text-[13px] font-semibold leading-snug text-foreground line-clamp-2">{s.classTypeName}</div>
      <div className="flex flex-wrap items-center gap-x-1.5 text-[11px] leading-tight text-muted">
        <span className="truncate">{s.instructorName}</span>
        <span className="text-muted/60">·</span>
        <span>{s.isIndividual ? "Bireysel" : "Grup"} {cap}</span>
      </div>
      <StudentList names={names} size="text-[11px]" />
    </button>
  );
}

function RequestCard({ r, onSelect }: { r: MakeupRequestDto; onSelect?: (r: MakeupRequestDto) => void }) {
  return (
    <button
      type="button"
      onClick={() => onSelect?.(r)}
      onContextMenu={(e) => { e.preventDefault(); onSelect?.(r); }}
      title={`${r.classTypeName} · ${r.studentName} · Onay bekliyor`}
      className="flex w-full flex-col gap-0.5 rounded-lg border border-dashed border-l-[3px] border-warning/55 border-l-warning bg-warning/12 px-2 py-1.5 text-left transition-all hover:bg-warning/20"
    >
      <div className="flex items-start justify-between gap-1.5">
        <span className="text-[11px] font-semibold leading-tight text-foreground/65">{fmtTime(r.startAt)}–{fmtTime(r.endAt)}</span>
        <span className="shrink-0 rounded bg-warning/20 px-1 text-[9px] font-bold uppercase tracking-wide text-warning">Bekliyor</span>
      </div>
      <div className="text-[13px] font-semibold leading-snug text-foreground line-clamp-2">{r.classTypeName}</div>
      <div className="truncate text-[11px] leading-tight text-muted">{r.studentName}</div>
      {r.hasConflict && <div className="text-[10px] font-semibold text-danger">⚠ Çakışma var</div>}
    </button>
  );
}

function WindowCard({ w, actionable, onSelect }: {
  w: AvailabilityWindowDto; actionable: boolean; onSelect?: (w: AvailabilityWindowDto) => void;
}) {
  return (
    <button
      type="button"
      onClick={() => onSelect?.(w)}
      disabled={!actionable}
      title={`Boşluk · ${availRangeLabel(w)} · ${w.durationMinutes} dk · ${availCapacityLabel(w)} · ${availInstructorLabel(w)}`}
      className={`flex w-full flex-col gap-0.5 rounded-lg border border-dashed border-l-[3px] border-success/45 border-l-success bg-success/10 px-2 py-1.5 text-left transition-all ${actionable ? "cursor-pointer hover:bg-success/16 hover:border-success/70" : "cursor-default"}`}
    >
      <div className="flex items-center gap-1.5">
        <span className="shrink-0 rounded bg-success/20 px-1 text-[9px] font-bold uppercase tracking-wide text-success">Boşluk</span>
        <span className="text-[12px] font-semibold leading-tight text-foreground">{availRangeLabel(w)}</span>
      </div>
      <div className="text-[11px] leading-tight text-muted">{w.durationMinutes} dk · {availCapacityLabel(w)}</div>
      <div className="truncate text-[11px] leading-tight text-muted">{availInstructorLabel(w)}</div>
      {actionable && <div className="mt-0.5 text-[11px] font-semibold text-success">+ Bu boşluğa ders aç</div>}
    </button>
  );
}

/* ----------------------------------------------------------- zaman gruplama */
type DayItem = { key: string; startMin: number; endMin: number } & (
  | { kind: "session"; s: ClassSessionDto }
  | { kind: "req"; r: MakeupRequestDto }
  | { kind: "window"; w: AvailabilityWindowDto }
);

const kindRank = (it: DayItem) => (it.kind === "session" ? 0 : it.kind === "req" ? 1 : 2);

// Zamanı çakışan öğeleri tek bir kümeye topla (standart takvim "concurrency" kümeleme).
function clusterByOverlap(items: DayItem[]): DayItem[][] {
  const sorted = [...items].sort((a, b) => a.startMin - b.startMin || a.endMin - b.endMin);
  const clusters: DayItem[][] = [];
  let cur: DayItem[] = [];
  let curEnd = -Infinity;
  for (const it of sorted) {
    if (cur.length && it.startMin >= curEnd) { clusters.push(cur); cur = []; curEnd = -Infinity; }
    cur.push(it);
    curEnd = Math.max(curEnd, it.endMin);
  }
  if (cur.length) clusters.push(cur);
  // Küme içi sıralama: önce dersler/talepler, açık boşluklar altta ("dolu + sonra açık kapasite" hissi).
  for (const c of clusters) c.sort((a, b) => kindRank(a) - kindRank(b) || a.startMin - b.startMin);
  return clusters;
}

interface RenderCtx {
  selectedId: string | null;
  students?: Map<string, string[]>;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onSelectWindow?: (w: AvailabilityWindowDto) => void;
}

function ItemCard({ it, ctx }: { it: DayItem; ctx: RenderCtx }) {
  if (it.kind === "session") return <SessionCard s={it.s} selected={it.s.id === ctx.selectedId} names={ctx.students?.get(it.s.id)} onSelect={ctx.onSelectSession} />;
  if (it.kind === "req") return <RequestCard r={it.r} onSelect={ctx.onSelectRequest} />;
  return <WindowCard w={it.w} actionable={!!ctx.onSelectWindow} onSelect={ctx.onSelectWindow} />;
}

// Bir günün tüm zaman gruplarını (tekil kart veya eşzamanlı grup kutusu) render eder.
function DayBody({ items, ctx }: { items: DayItem[]; ctx: RenderCtx }) {
  const clusters = clusterByOverlap(items);
  return (
    <>
      {clusters.map((cluster) => {
        if (cluster.length === 1) {
          const it = cluster[0];
          return <ItemCard key={it.key} it={it} ctx={ctx} />;
        }
        // Eşzamanlı grup: kutu + zaman çıpası + sayaç → bir arada olduğu netçe hissedilir.
        const start = Math.min(...cluster.map((c) => c.startMin));
        const startLabel = `${String(Math.floor(start / 60)).padStart(2, "0")}:${String(start % 60).padStart(2, "0")}`;
        return (
          <div key={cluster[0].key} className="rounded-xl bg-surface-muted/50 p-1.5 ring-1 ring-inset ring-border">
            <div className="mb-1 flex items-center gap-1.5 px-0.5">
              <svg viewBox="0 0 16 16" className="size-3.5 text-muted" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden>
                <rect x="2.5" y="2.5" width="8" height="8" rx="1.5" /><rect x="5.5" y="5.5" width="8" height="8" rx="1.5" />
              </svg>
              <span className="text-[11px] font-semibold text-foreground/80">{startLabel}</span>
              <span className="ml-auto rounded-full bg-foreground/10 px-1.5 py-px text-[10px] font-bold text-foreground/70">{cluster.length} eşzamanlı</span>
            </div>
            <div className="space-y-1.5">
              {cluster.map((it) => <ItemCard key={it.key} it={it} ctx={ctx} />)}
            </div>
          </div>
        );
      })}
    </>
  );
}

function buildItemsByDay(sessions: ClassSessionDto[], requests: MakeupRequestDto[], windows: AvailabilityWindowDto[]) {
  const m = new Map<string, DayItem[]>();
  const push = (d: string, it: DayItem) => (m.get(d) ?? m.set(d, []).get(d)!).push(it);
  for (const s of sessions) push(dateKey(s.startAt), { kind: "session", key: s.id, startMin: minOfDay(s.startAt), endMin: Math.max(minOfDay(s.startAt) + 15, minOfDay(s.endAt)), s });
  for (const r of requests) push(dateKey(r.startAt), { kind: "req", key: r.id, startMin: minOfDay(r.startAt), endMin: Math.max(minOfDay(r.startAt) + 15, minOfDay(r.endAt)), r });
  // Pencerenin "işgal" aralığı = BAŞLANGIÇ aralığı [from,to]. (süre eklenmez; yoksa ardışık pencereler yanlış "eşzamanlı" gruplanır.)
  for (const w of windows) push(w.date, { kind: "window", key: `w|${w.date}|${w.startWindowFrom}`, startMin: minOfDay(w.startWindowFrom), endMin: minOfDay(w.startWindowTo), w });
  return m;
}

/* ------------------------------------------------------------- Mobil ajanda */
export function MobileAgenda({
  days, sessions, requests, availabilityWindows, workingHours, students, onSelectSession, onSelectRequest, onSelectWindow,
}: {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  availabilityWindows?: AvailabilityWindowDto[];
  workingHours?: WorkingHourDayDto[];
  students?: Map<string, string[]>;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onSelectWindow?: (w: AvailabilityWindowDto) => void;
}) {
  const todayStr = localToday();
  const itemsByDay = buildItemsByDay(sessions, requests, availabilityWindows ?? []);
  const whMap = toWorkingHoursMap(workingHours);
  const ctx: RenderCtx = { selectedId: null, students, onSelectSession, onSelectRequest, onSelectWindow };

  return (
    <div className="space-y-4">
      {days.map((date) => {
        const items = itemsByDay.get(date) ?? [];
        if (items.length === 0) return null;
        const h = dayHeading(date);
        const isToday = date === todayStr;
        const wh = workingHoursLabel(date, whMap);
        return (
          <div key={date}>
            <h3 className={`mb-2 text-sm font-semibold ${isToday ? "text-primary" : "text-foreground"}`}>
              {h.full}, {h.dm}{isToday && " · bugün"}
              {wh && <span className={`ml-2 text-xs font-normal ${wh.closed ? "text-muted/70" : "text-muted"}`}>· {wh.label}</span>}
            </h3>
            <div className="space-y-2">
              <DayBody items={items} ctx={ctx} />
            </div>
          </div>
        );
      })}
    </div>
  );
}

/* ----------------------------------------------------------- Haftalık takvim */
export interface TimetableProps {
  days: string[];
  sessions: ClassSessionDto[];
  requests: MakeupRequestDto[];
  /** Sprint 36: "Boşlukları Göster" overlay'i — dashed "Boşluk" kartı olarak render edilir. */
  availabilityWindows?: AvailabilityWindowDto[];
  /** Sprint 37: stüdyo çalışma saatleri — gün başlığında "09:00–22:00" / "Kapalı" etiketi. */
  workingHours?: WorkingHourDayDto[];
  selectedId: string | null;
  students?: Map<string, string[]>;
  /** Dikey kaydırma için kapsayıcı yüksekliği (sticky gün başlığı buna göre sabitlenir). */
  maxHeight?: string;
  onSelectSession: (s: ClassSessionDto) => void;
  onSelectRequest?: (r: MakeupRequestDto) => void;
  onSelectWindow?: (w: AvailabilityWindowDto) => void;
  /** Gün başlığındaki "+" → o güne hızlı ders ekleme (varsayılan saat ile). */
  onEmptyClick?: (dateStr: string, hour: number) => void;
}

export function WeekTimetable({
  days, sessions, requests, availabilityWindows, workingHours, selectedId, students, maxHeight = "70vh",
  onSelectSession, onSelectRequest, onSelectWindow, onEmptyClick,
}: TimetableProps) {
  const itemsByDay = useMemo(
    () => buildItemsByDay(sessions, requests, availabilityWindows ?? []),
    [sessions, requests, availabilityWindows],
  );
  const whMap = useMemo(() => toWorkingHoursMap(workingHours), [workingHours]);
  const ctx: RenderCtx = { selectedId, students, onSelectSession, onSelectRequest, onSelectWindow };
  const todayStr = localToday();
  const cols = `repeat(${days.length}, minmax(${DAY_W}px, 1fr))`;

  return (
    <div className="overflow-auto rounded-2xl border-2 border-border bg-surface" style={{ maxHeight }}>
      <div style={{ minWidth: days.length * DAY_W }}>
        {/* Başlık satırı — dikey kaydırmada sabit */}
        <div className="sticky top-0 z-20 grid border-b-2 border-border bg-surface-muted/95 backdrop-blur" style={{ gridTemplateColumns: cols }}>
          {days.map((date) => {
            const h = dayHeading(date);
            const isToday = date === todayStr;
            const wh = workingHoursLabel(date, whMap);
            return (
              <div key={date} className={`relative border-r border-border px-2 py-2 text-center last:border-r-0 ${isToday ? "bg-primary-soft" : ""}`}>
                <div className={`text-sm font-semibold ${isToday ? "text-primary" : "text-foreground"}`}>{h.full}</div>
                <div className="text-xs text-muted">{h.dm}{isToday && " · bugün"}</div>
                {wh && (
                  <div className={`text-[10px] ${wh.closed ? "font-semibold text-muted/70" : "text-muted"}`}>{wh.label}</div>
                )}
                {onEmptyClick && (
                  <button
                    type="button"
                    onClick={() => onEmptyClick(date, ADD_HOUR)}
                    aria-label={`${h.full} gününe ders ekle`}
                    title="Bu güne ders ekle"
                    className="absolute right-1 top-1 grid size-6 place-items-center rounded-md text-muted transition-colors hover:bg-primary-soft hover:text-primary"
                  >
                    +
                  </button>
                )}
              </div>
            );
          })}
        </div>

        {/* Gövde — her sütun zaman sırasına dizili kartlar/gruplar (tam sığar) */}
        <div className="grid items-stretch" style={{ gridTemplateColumns: cols }}>
          {days.map((date) => {
            const items = itemsByDay.get(date) ?? [];
            const isToday = date === todayStr;
            return (
              <div key={date} className={`flex min-h-[140px] flex-col gap-2 border-r border-border p-1.5 last:border-r-0 ${isToday ? "bg-primary-soft/10" : ""}`}>
                {items.length === 0 ? (
                  onEmptyClick ? (
                    <button
                      type="button"
                      onClick={() => onEmptyClick(date, ADD_HOUR)}
                      className="m-auto flex flex-col items-center gap-1 rounded-lg border border-dashed border-border px-3 py-2 text-xs text-muted transition-colors hover:border-primary/50 hover:text-primary"
                    >
                      <span className="text-base leading-none">+</span>
                      Ders ekle
                    </button>
                  ) : (
                    <span className="m-auto text-xs text-muted/70">—</span>
                  )
                ) : (
                  <DayBody items={items} ctx={ctx} />
                )}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
