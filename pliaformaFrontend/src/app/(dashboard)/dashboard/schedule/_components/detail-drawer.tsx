"use client";

// Ders detay drawer'ı (sağdan açılır). Ders bilgileri + aksiyonlar (mevcut modalları tetikler).
import { useEffect, useState } from "react";
import { type ClassSessionDto, isUnlimitedCapacity, sessionKindLabels, sessionStatusLabels } from "@/lib/domain-types";
import { Badge, Button, Textarea } from "@/components/ui";

const fmtTime = (iso: string) => iso.slice(11, 16);
const TR_DAYS = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
const fmtDate = (iso: string) => { const d = new Date(iso.slice(0, 10) + "T00:00:00"); return `${TR_DAYS[d.getDay()]}, ${iso.slice(8, 10)}.${iso.slice(5, 7)}.${iso.slice(0, 4)}`; };

export interface DrawerActions {
  book: () => void;
  reschedule: () => void;
  moveStudents: () => void;
  cancel: () => void;
  notDelivered: () => void;
  confirm: () => void;
  unconfirm: () => void;
  bookings: () => void;
  attendance: () => void;
  actual: () => void;
}

export function SessionDetailDrawer({
  session, isManager, canBookStudent, canConfirm, busy, students, onClose, actions, onSaveNotes,
}: {
  session: ClassSessionDto | null;
  isManager: boolean;
  /** Sprint 47: isManager DIŞINDA "Öğrenci ekle" aksiyonuna izinli mi (pilot modda kendi seansına ekleyen hoca). */
  canBookStudent: boolean;
  canConfirm: boolean;
  busy: boolean;
  students?: string[];
  onClose: () => void;
  actions: DrawerActions;
  onSaveNotes: (notes: string) => Promise<void>;
}) {
  const [shown, setShown] = useState(false);
  const [notes, setNotes] = useState("");
  const [savingNotes, setSavingNotes] = useState(false);
  const [notesErr, setNotesErr] = useState<string | null>(null);

  useEffect(() => {
    if (!session) { setShown(false); return; }
    setShown(true);
    setNotes(session.notes ?? "");
    setNotesErr(null);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [session, onClose]);

  const saveNotes = async () => {
    setSavingNotes(true); setNotesErr(null);
    try { await onSaveNotes(notes.trim()); }
    catch (e) { setNotesErr(e instanceof Error ? e.message : "Not kaydedilemedi."); }
    finally { setSavingNotes(false); }
  };

  if (!session) return null;
  const s = session;
  const cap = s.isIndividual
    ? `${s.bookedCount} / 1`
    : isUnlimitedCapacity(s.capacity) ? `${s.bookedCount} öğrenci · limitsiz` : `${s.bookedCount} / ${s.capacity}`;
  const statusTone = s.status === "Scheduled" ? "success" : s.status === "Cancelled" ? "danger" : "neutral";

  return (
    <div className="fixed inset-0 z-50">
      <div className={`absolute inset-0 bg-black/30 backdrop-blur-sm transition-opacity duration-200 ${shown ? "opacity-100" : "opacity-0"}`} onClick={onClose} />
      <aside
        role="dialog"
        aria-modal="true"
        className={`absolute right-0 top-0 flex h-full w-full max-w-sm flex-col border-l border-border bg-surface shadow-2xl transition-transform duration-200 ${shown ? "translate-x-0" : "translate-x-full"}`}
      >
        {/* Başlık */}
        <div className="flex items-start justify-between gap-3 border-b border-border p-5">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-1.5">
              <h3 className="truncate text-lg font-semibold text-foreground">{s.classTypeName}</h3>
              {s.sessionKind === "Makeup" && <Badge tone="rose">{sessionKindLabels.Makeup}</Badge>}
              {(s.sessionKind === "Trial" || s.sessionKind === "Special") && <Badge tone="neutral">{sessionKindLabels[s.sessionKind]}</Badge>}
            </div>
            <p className="mt-0.5 text-sm text-muted">{fmtDate(s.startAt)}</p>
          </div>
          <button onClick={onClose} aria-label="Kapat" className="rounded-lg p-1 text-muted hover:bg-surface-muted hover:text-foreground">✕</button>
        </div>

        {/* Bilgiler */}
        <div className="flex-1 overflow-y-auto p-5">
          <div className="space-y-3">
            <Row label="Saat" value={`${fmtTime(s.startAt)} – ${fmtTime(s.endAt)}`} />
            <Row label="Eğitmen" value={<span className="flex items-center gap-1.5">{s.instructorName}{s.instructorChanged && <Badge tone="rose">Hoca değişti</Badge>}</span>} />
            <Row label="Tür" value={s.isIndividual ? "Bireysel" : "Grup"} />
            <Row label="Doluluk" value={<span className={s.isFull && !isUnlimitedCapacity(s.capacity) ? "text-danger" : ""}>{cap}{s.isFull && !isUnlimitedCapacity(s.capacity) ? " · DOLU" : ""}</span>} />
            <Row label="Durum" value={<Badge tone={statusTone}>{sessionStatusLabels[s.status]}</Badge>} />
            {s.rescheduledAt && <Row label="Durum" value="Bu ders ertelendi" />}
          </div>

          {/* Derse kayıtlı öğrenciler */}
          <div className="mt-4">
            <div className="mb-1.5 text-xs font-semibold text-muted">Öğrenciler ({students?.length ?? 0})</div>
            {students && students.length > 0 ? (
              <div className="flex flex-wrap gap-1.5">
                {students.map((name, i) => (
                  <span key={i} className="rounded-full border border-border bg-surface-muted px-2 py-0.5 text-xs text-foreground">{name}</span>
                ))}
              </div>
            ) : (
              <p className="text-xs text-muted">Bu derse kayıtlı öğrenci yok.{canBookStudent && s.status === "Scheduled" && " “Öğrenci ekle” ile ekleyin."}</p>
            )}
          </div>

          {/* Derse not ekle/düzenle */}
          <div className="mt-4">
            <div className="mb-1.5 flex items-center justify-between">
              <label className="text-xs font-semibold text-muted">Ders notu</label>
              {(notes.trim() !== (s.notes ?? "")) && <span className="text-[11px] text-warning">kaydedilmedi</span>}
            </div>
            <Textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={3}
              placeholder="Bu derse not ekleyin (ör. ekipman, hatırlatma)…"
            />
            {notesErr && <p className="mt-1 text-xs text-danger">{notesErr}</p>}
            <div className="mt-2 flex justify-end">
              <Button size="sm" variant="secondary" loading={savingNotes} disabled={notes.trim() === (s.notes ?? "")} onClick={saveNotes}>
                Notu kaydet
              </Button>
            </div>
          </div>
        </div>

        {/* Aksiyonlar */}
        {s.status !== "Cancelled" && (
          <div className="border-t border-border p-4">
            <div className="grid grid-cols-2 gap-2">
              {s.status === "Scheduled" && (
                <>
                  {canBookStudent && <Button variant="secondary" size="sm" disabled={s.isFull} onClick={actions.book}>{s.isFull ? "Dolu" : "Öğrenci ekle"}</Button>}
                  {canConfirm && <Button size="sm" loading={busy} onClick={actions.confirm}>Onayla</Button>}
                  <Button variant="ghost" size="sm" onClick={actions.reschedule}>Ertele</Button>
                  {isManager && s.bookedCount > 0 && (
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={new Date(s.startAt) < new Date()}
                      title={new Date(s.startAt) < new Date() ? "Geçmiş dersten öğrenci taşınamaz" : undefined}
                      onClick={actions.moveStudents}
                    >
                      Öğrenci Taşı
                    </Button>
                  )}
                  <Button variant="ghost" size="sm" onClick={actions.cancel}>İptal et</Button>
                  {isManager && <Button variant="ghost" size="sm" onClick={actions.notDelivered}>İşlenmedi</Button>}
                </>
              )}
              {s.status === "Completed" && canConfirm && (
                <Button variant="ghost" size="sm" loading={busy} onClick={actions.unconfirm} className="col-span-2">Onayı geri al</Button>
              )}
              <Button variant="ghost" size="sm" onClick={actions.bookings}>Rezervasyonlar</Button>
              <Button variant="ghost" size="sm" onClick={actions.attendance}>Yoklama</Button>
              {isManager && <Button variant="ghost" size="sm" onClick={actions.actual} className="col-span-2">Fiilî hoca</Button>}
            </div>
          </div>
        )}
      </aside>
    </div>
  );
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface-muted/50 px-3 py-2">
      <span className="text-xs font-medium text-muted">{label}</span>
      <span className="text-sm text-foreground">{value}</span>
    </div>
  );
}
