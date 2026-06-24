"use client";

import { useCallback, useEffect, useState } from "react";
import { attendanceService, classTypeService, contractService, memberService, paymentService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useInstructors } from "@/lib/useInstructors";
import {
  type AttendanceSummaryDto,
  type ClassTypeDto,
  type DayOfWeek,
  type GuestInput,
  type MemberDto,
  type PaymentDto,
  type PreferredSlotInput,
  type StudentContractDto,
  type StudentContractLineDto,
  type StudentDebtSummaryDto,
  type StudentDto,
  type StudentLevel,
  type StudentSource,
  daysOfWeek,
  dayLabels,
  paymentStatusLabels,
  studentContractStatusLabels,
  studentLevelLabels,
  studentSourceLabels,
} from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Modal,
  Select,
  Textarea,
  useConfirm,
} from "@/components/ui";

export const emptyGuest: GuestInput = {
  fullName: "", phone: "", email: "", source: "Phone", level: null, notes: "", healthNotes: "",
  emergencyContactName: "", emergencyContactPhone: "", birthDate: "",
};

export const statusTone = (s: StudentDto["status"]) => (s === "Active" ? "success" : s === "Guest" ? "warning" : "neutral");

const today = () => new Date().toISOString().slice(0, 10);
const plus = (days: number) => new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;

// ============================================================================
// Sprint (UX): Yeni sözleşme satırı — ferah, bölümlenmiş modal (eski sıkışık inline form yerine).
// ============================================================================
export function AddContractLineModal({ studentId, onClose, onSaved }: { studentId: string; onClose: () => void; onSaved: () => void }) {
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [instructors, setInstructors] = useState<MemberDto[]>([]);
  const [headers, setHeaders] = useState<StudentContractDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const empty = {
    headerId: "", classTypeId: "", instructorProfileId: "", startDate: today(), endDate: plus(60),
    lessonCount: 10, pricePerLesson: 0, makeupAllowance: 2, dueDate: "", currency: "TRY",
  };
  const [form, setForm] = useState(empty);
  // Sprint 33: kapasite tercihi (Farketmez / Bireysel / En fazla N / Diğer custom).
  const [capMode, setCapMode] = useState<"any" | "1" | "2" | "3" | "4" | "custom">("any");
  const [customCap, setCustomCap] = useState(5);
  const preferredMaxCapacity = capMode === "any" ? null : capMode === "custom" ? customCap : Number(capMode);
  const [prefSlots, setPrefSlots] = useState<PreferredSlotInput[]>([]);
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");

  useEffect(() => {
    (async () => {
      try {
        const [ct, ms, cs] = await Promise.all([
          classTypeService.list(true),
          memberService.list(),
          contractService.forStudent(studentId).catch(() => [] as StudentContractDto[]),
        ]);
        setClassTypes(ct);
        setInstructors(ms.filter((m) => m.instructorProfileId));
        setHeaders(cs.filter((c) => c.status === "Active"));
      } catch (e) {
        setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
      }
    })();
  }, [studentId]);

  const addPrefSlot = () => {
    const t = slotTime ? `${slotTime}:00` : null;
    if (prefSlots.some((s) => s.dayOfWeek === slotDay && (s.preferredStartTime ?? null) === t)) {
      setErr("Aynı gün ve saat tercihi zaten eklendi."); return;
    }
    setErr(null);
    setPrefSlots((xs) => [...xs, { dayOfWeek: slotDay, preferredStartTime: t, notes: null }]);
    setSlotTime("");
  };
  const slotChipLabel = (s: PreferredSlotInput) => `${dayLabels[s.dayOfWeek]}${s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : ""}`;
  const computedTotal = (Number(form.lessonCount) || 0) * (Number(form.pricePerLesson) || 0);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (prefSlots.length === 0) { setErr("En az bir tercih gün/saat ekleyin."); return; }
    setErr(null); setBusy(true);
    try {
      await contractService.addLine(studentId, {
        headerId: form.headerId || null,
        classTypeId: form.classTypeId,
        instructorProfileId: form.instructorProfileId || null,
        startDate: form.startDate,
        endDate: form.endDate,
        lessonCount: Number(form.lessonCount),
        pricePerLesson: Number(form.pricePerLesson),
        makeupAllowance: Number(form.makeupAllowance),
        dueDate: form.dueDate || null,
        preferredMaxCapacity,
        currency: form.currency || "TRY",
        preferredSlots: prefSlots,
      });
      onSaved();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Sözleşme eklenemedi.");
    } finally {
      setBusy(false);
    }
  };

  const Section = ({ title, children }: { title: string; children: React.ReactNode }) => (
    <div className="space-y-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted">{title}</p>
      {children}
    </div>
  );

  return (
    <Modal open onClose={onClose} title="Yeni sözleşme" size="lg">
      <form onSubmit={submit} className="space-y-6">
        {err && <Alert kind="error">{err}</Alert>}

        <Section title="Ders & Hoca">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Ders grubu">
              <Select value={form.classTypeId} onChange={(e) => setForm((f) => ({ ...f, classTypeId: e.target.value }))} required>
                <option value="">Seçin…</option>
                {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </Select>
            </FormField>
            <FormField label="Hoca (opsiyonel)" hint="Seçilmezse program oluştururken seçilir.">
              <Select value={form.instructorProfileId} onChange={(e) => setForm((f) => ({ ...f, instructorProfileId: e.target.value }))}>
                <option value="">Farketmez (herhangi bir hoca)</option>
                {instructors.map((m) => <option key={m.instructorProfileId!} value={m.instructorProfileId!}>{m.fullName}</option>)}
              </Select>
            </FormField>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Kapasite tercihi" hint="Ders en fazla kaç kişilik olsun?">
              <Select value={capMode} onChange={(e) => setCapMode(e.target.value as typeof capMode)}>
                <option value="any">Farketmez</option>
                <option value="1">Bireysel (1 kişi)</option>
                <option value="2">En fazla 2</option>
                <option value="3">En fazla 3</option>
                <option value="4">En fazla 4</option>
                <option value="custom">Diğer (sayı gir)</option>
              </Select>
            </FormField>
            {capMode === "custom" && (
              <FormField label="En fazla kaç kişi?" hint="1–50">
                <Input type="number" min={1} max={50} value={customCap} onChange={(e) => setCustomCap(Number(e.target.value))} />
              </FormField>
            )}
          </div>
          {headers.length > 1 && (
            <FormField label="Sözleşme başlığı (ops.)" hint="Boş = en güncel aktif / yeni">
              <Select value={form.headerId} onChange={(e) => setForm((f) => ({ ...f, headerId: e.target.value }))}>
                <option value="">Otomatik (en güncel aktif / yeni)</option>
                {headers.map((c) => <option key={c.id} value={c.id}>{c.classTypeName} · {c.startDate}</option>)}
              </Select>
            </FormField>
          )}
        </Section>

        <Section title="Tarih & Ders sayısı">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Başlangıç"><Input type="date" value={form.startDate} onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))} required /></FormField>
            <FormField label="Bitiş"><Input type="date" value={form.endDate} onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))} required /></FormField>
            <FormField label="Ders sayısı"><Input type="number" min={1} value={form.lessonCount} onChange={(e) => setForm((f) => ({ ...f, lessonCount: Number(e.target.value) }))} /></FormField>
            <FormField label="Telafi hakkı"><Input type="number" min={0} value={form.makeupAllowance} onChange={(e) => setForm((f) => ({ ...f, makeupAllowance: Number(e.target.value) }))} /></FormField>
          </div>
        </Section>

        <Section title="Ücret">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Ders başı ücret"><Input type="number" min={0} step="0.01" value={form.pricePerLesson} onChange={(e) => setForm((f) => ({ ...f, pricePerLesson: Number(e.target.value) }))} /></FormField>
            <FormField label="Para birimi"><Input value={form.currency} onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value.toUpperCase() }))} maxLength={8} /></FormField>
            <FormField label="Toplam ücret" hint="ders sayısı × ders başı (otomatik)">
              <Input value={`${computedTotal.toLocaleString("tr-TR")} ${form.currency}`} readOnly disabled />
            </FormField>
            <FormField label="Son ödeme tarihi (ops.)"><Input type="date" value={form.dueDate} onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))} /></FormField>
          </div>
        </Section>

        <Section title="Tercih gün/saatleri (en az 1)">
          <div className="flex flex-wrap items-end gap-2 rounded-xl border border-dashed border-border p-3">
            <Select value={slotDay} onChange={(e) => setSlotDay(e.target.value as DayOfWeek)} className="w-36">
              {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
            </Select>
            <Input type="time" value={slotTime} onChange={(e) => setSlotTime(e.target.value)} className="w-32" />
            <Button type="button" variant="secondary" size="sm" onClick={addPrefSlot}>+ Tercih ekle</Button>
            <span className="text-xs text-muted">Saat boş = farketmez</span>
          </div>
          {prefSlots.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {prefSlots.map((s, i) => (
                <span key={i} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-2.5 py-1 text-xs text-foreground">
                  {slotChipLabel(s)}
                  <button type="button" className="text-muted hover:text-danger" onClick={() => setPrefSlots((xs) => xs.filter((_, j) => j !== i))}>×</button>
                </span>
              ))}
            </div>
          )}
        </Section>

        <div className="flex justify-end gap-2 border-t border-border pt-4">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Sözleşme ekle</Button>
        </div>
      </form>
    </Modal>
  );
}

// ============================================================================
// Stat — küçük etiket/değer kartı (özet ızgarası için).
// ============================================================================
function Stat({ label, value, hint, tone }: { label: string; value: React.ReactNode; hint?: string; tone?: "danger" | "success" }) {
  return (
    <div className="rounded-xl border border-border bg-surface px-3 py-2">
      <p className="text-[11px] font-medium uppercase tracking-wide text-muted">{label}</p>
      <p className={`mt-0.5 text-lg font-semibold ${tone === "danger" ? "text-danger" : tone === "success" ? "text-success" : "text-foreground"}`}>
        {value}{hint && <span className="ml-0.5 text-xs font-normal text-muted">{hint}</span>}
      </p>
    </div>
  );
}

const SectionTitle = ({ children }: { children: React.ReactNode }) => (
  <p className="text-xs font-semibold uppercase tracking-wide text-muted">{children}</p>
);

// ============================================================================
// ContractDetailModal — bir sözleşmenin TEMİZ detay/yönetim ekranı (eski inline collapse yerine).
// Özet ızgarası + tercih gün/saatleri + program oluştur + son ödeme düzenle + iptal.
// ============================================================================
export function ContractDetailModal({ line: l, onClose, onChanged }: { line: StudentContractLineDto; onClose: () => void; onChanged: () => void }) {
  const confirm = useConfirm();
  const { instructors } = useInstructors();
  const [err, setErr] = useState<string | null>(null);
  const [due, setDue] = useState(l.dueDate ?? "");
  const [busy, setBusy] = useState(false);
  const [days, setDays] = useState<DayOfWeek[]>([]);
  const [time, setTime] = useState("18:00");
  const [reuse, setReuse] = useState(!l.isIndividual);
  const [genMsg, setGenMsg] = useState<string | null>(null);
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");
  const [slotBusy, setSlotBusy] = useState(false);
  const isActive = l.status === "Active";

  // Sprint 33: üretim-anı hoca (line hocası null ise zorunlu) + seans kapasitesi (line tercihi null + ekipman-bağlı ise zorunlu).
  const [genInstructor, setGenInstructor] = useState("");
  const [genCapacity, setGenCapacity] = useState(2);
  const [equipmentBound, setEquipmentBound] = useState(false);
  useEffect(() => {
    classTypeService.list(true)
      .then((cts) => setEquipmentBound(!!cts.find((c) => c.id === l.classTypeId)?.equipmentId))
      .catch(() => {});
  }, [l.classTypeId]);
  const needInstructor = l.instructorProfileId == null;
  const needCapacity = l.preferredMaxCapacity == null && equipmentBound;
  const capLabel = l.preferredMaxCapacity == null ? "Farketmez" : l.preferredMaxCapacity === 1 ? "Bireysel" : `En fazla ${l.preferredMaxCapacity} kişi`;

  const addSlot = async () => {
    setSlotBusy(true); setErr(null);
    try { await contractService.addPreferredSlot(l.id, { dayOfWeek: slotDay, preferredStartTime: slotTime ? `${slotTime}:00` : null, notes: null }); setSlotTime(""); onChanged(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Tercih eklenemedi."); }
    finally { setSlotBusy(false); }
  };
  const removeSlot = async (slotId: string) => {
    setSlotBusy(true); setErr(null);
    try { await contractService.removePreferredSlot(slotId); onChanged(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Tercih kaldırılamadı."); }
    finally { setSlotBusy(false); }
  };
  const saveDue = async () => {
    setBusy(true); setErr(null);
    try { await contractService.updateLine(l.id, { dueDate: due || null, notes: null }); onChanged(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Güncellenemedi."); }
    finally { setBusy(false); }
  };
  const cancelLine = async () => {
    if (!(await confirm({ title: "Sözleşmeyi iptal et", tone: "danger", confirmLabel: "İptal et", message: "Bu sözleşme iptal edilecek. (bağlı ödeme/ders/telafi varsa engellenir)" }))) return;
    setBusy(true); setErr(null);
    try { await contractService.cancelLine(l.id); onChanged(); onClose(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İptal edilemedi."); }
    finally { setBusy(false); }
  };
  const generate = async () => {
    if (days.length === 0) { setGenMsg("En az bir gün seçin."); return; }
    if (needInstructor && !genInstructor) { setGenMsg("Bu sözleşmenin hocası belirtilmemiş; program için bir hoca seçin."); return; }
    if (needCapacity && !genCapacity) { setGenMsg("Ekipman bağlı ders için seans kapasitesi seçin."); return; }
    setBusy(true); setGenMsg(null);
    try {
      const r = await contractService.generateLineSchedule(l.id, {
        slots: days.map((d) => ({ dayOfWeek: d, startTime: `${time}:00` })),
        reuseExistingGroup: l.isIndividual ? false : reuse,
        instructorProfileId: needInstructor ? genInstructor : null,
        capacity: needCapacity ? Number(genCapacity) : null,
      });
      setGenMsg(`✓ ${r.createdCount} ders oluşturuldu (yeni ${r.newSessionCount}, gruba bağlanan ${r.reusedSessionCount}, atlanan ${r.skippedCount}).`);
      onChanged();
    } catch (e) { setGenMsg(e instanceof ApiError ? e.message : "Program oluşturulamadı."); }
    finally { setBusy(false); }
  };

  return (
    <Modal open onClose={onClose} size="lg" title={`Sözleşme — ${l.classTypeName}`}>
      <div className="space-y-5">
        {err && <Alert kind="error">{err}</Alert>}

        <div className="flex flex-wrap items-center gap-2">
          <Badge tone={isActive ? "success" : "neutral"}>{studentContractStatusLabels[l.status]}</Badge>
          <Badge tone="rose">{capLabel}</Badge>
          <span className="text-sm text-muted">{l.instructorName ?? "Herhangi bir hoca"} · {l.startDate} → {l.endDate}</span>
        </div>

        {/* Ders özeti */}
        <div>
          <SectionTitle>Ders hakkı</SectionTitle>
          <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Stat label="Kalan hak" value={l.remainingLessonRight} hint={`/${l.lessonCount}`} />
            <Stat label="Planlanabilir" value={l.availableLessonCount} />
            <Stat label="Planlanmış" value={l.plannedLessonCount} />
            <Stat label="İşlenmiş" value={l.deliveredLessonCount} />
          </div>
        </div>

        {/* Ödeme özeti */}
        <div>
          <SectionTitle>Ücret</SectionTitle>
          <div className="mt-2 grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Stat label="Toplam" value={money(l.totalPrice, l.currency)} />
            <Stat label="Ödenen" value={money(l.paid, l.currency)} />
            <Stat label="Kalan" value={money(l.remaining, l.currency)} tone={l.remaining > 0 ? "danger" : "success"} />
            <Stat label="Telafi hakkı" value={l.makeupAllowance} />
          </div>
        </div>

        {/* Tercih gün/saatleri */}
        <div className="space-y-2">
          <SectionTitle>Tercih gün/saatleri</SectionTitle>
          {l.preferredSlots.length === 0 ? (
            <p className="text-sm text-muted">Henüz tercih yok.</p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {l.preferredSlots.map((s) => (
                <span key={s.id} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-2.5 py-1 text-xs text-foreground">
                  {dayLabels[s.dayOfWeek]}{s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : ""}
                  {isActive && <button type="button" disabled={slotBusy} className="text-muted hover:text-danger" onClick={() => removeSlot(s.id)}>×</button>}
                </span>
              ))}
            </div>
          )}
          {isActive && (
            <div className="flex flex-wrap items-center gap-2">
              <Select value={slotDay} onChange={(e) => setSlotDay(e.target.value as DayOfWeek)} className="w-36">
                {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
              </Select>
              <Input type="time" value={slotTime} onChange={(e) => setSlotTime(e.target.value)} className="w-32" />
              <Button size="sm" variant="secondary" disabled={slotBusy} onClick={addSlot}>+ Tercih ekle</Button>
              <span className="text-xs text-muted">Saat boş = farketmez</span>
            </div>
          )}
        </div>

        {/* Programı oluştur */}
        {isActive && (
          <div className="space-y-2 rounded-xl border border-border bg-surface-muted/40 p-3">
            <SectionTitle>Programı oluştur</SectionTitle>
            <p className="text-xs text-muted">Seçili gün(ler) ve saatte, kalan {l.availableLessonCount} ders kadar seans oluşturur.</p>
            {(needInstructor || needCapacity) && (
              <div className="grid gap-3 sm:grid-cols-2">
                {needInstructor && (
                  <FormField label="Hoca (zorunlu)" hint="Sözleşmede hoca belirtilmemiş.">
                    <Select value={genInstructor} onChange={(e) => setGenInstructor(e.target.value)}>
                      <option value="">Seçin…</option>
                      {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
                    </Select>
                  </FormField>
                )}
                {needCapacity && (
                  <FormField label="Seans kapasitesi (zorunlu)" hint="Ekipman bağlı — sınırsız üretilemez (1–50).">
                    <Input type="number" min={1} max={50} value={genCapacity} onChange={(e) => setGenCapacity(Number(e.target.value))} />
                  </FormField>
                )}
              </div>
            )}
            <div className="flex flex-wrap gap-2">
              {daysOfWeek.map((d) => (
                <label key={d} className={`cursor-pointer rounded-lg border px-2.5 py-1 text-xs ${days.includes(d) ? "border-primary bg-primary-soft text-primary" : "border-border text-muted"}`}>
                  <input type="checkbox" className="hidden" checked={days.includes(d)} onChange={(e) => setDays((xs) => e.target.checked ? [...xs, d] : xs.filter((x) => x !== d))} />
                  {dayLabels[d]}
                </label>
              ))}
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} className="w-32" />
              {!l.isIndividual && (
                <label className="flex items-center gap-2 text-xs text-muted">
                  <input type="checkbox" checked={reuse} onChange={(e) => setReuse(e.target.checked)} /> Mevcut gruba bağlan
                </label>
              )}
              <Button size="sm" loading={busy} onClick={generate}>Oluştur ({l.availableLessonCount} ders)</Button>
            </div>
            {genMsg && <p className="text-xs text-muted">{genMsg}</p>}
          </div>
        )}

        {/* Footer aksiyonlar */}
        {isActive && (
          <div className="flex flex-wrap items-end justify-between gap-3 border-t border-border pt-4">
            <div className="flex items-end gap-2">
              <FormField label="Son ödeme tarihi">
                <Input type="date" value={due} onChange={(e) => setDue(e.target.value)} className="w-44" />
              </FormField>
              <Button size="sm" variant="secondary" loading={busy} onClick={saveDue}>Kaydet</Button>
            </div>
            <Button size="sm" variant="danger" disabled={busy} onClick={cancelLine}>Sözleşmeyi iptal et</Button>
          </div>
        )}
      </div>
    </Modal>
  );
}

// ============================================================================
// Finans (ödemeler + borç) modalı.
// ============================================================================
export function StudentFinanceModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [debt, setDebt] = useState<StudentDebtSummaryDto | null>(null);
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [payAmount, setPayAmount] = useState("");
  const [payDue, setPayDue] = useState(today());
  const [payLineId, setPayLineId] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const [ps, d, ls] = await Promise.all([
        paymentService.forStudent(student.id),
        contractService.debt(student.id),
        contractService.lines(student.id),
      ]);
      setPayments(ps); setDebt(d); setLines(ls); setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    }
  }, [student.id]);
  useEffect(() => { load(); }, [load]);

  const wrap = async (fn: () => Promise<unknown>) => {
    setBusy(true); setErr(null);
    try { await fn(); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İşlem başarısız."); }
    finally { setBusy(false); }
  };
  const createPayment = () => {
    if (!payAmount) { setErr("Tutar girin."); return; }
    if (!payLineId) { setErr("Ödeme için bir sözleşme satırı (paket) seçin."); return; }
    wrap(() => paymentService.create(student.id, { amount: Number(payAmount), dueDate: payDue, studentContractLineId: payLineId }))
      .then(() => { setPayAmount(""); setPayLineId(""); });
  };
  const activeLines = lines.filter((l) => l.status === "Active");

  return (
    <Modal open onClose={onClose} title={`${student.fullName} — Ödemeler`} size="lg">
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        {debt && (
          <div className="rounded-xl border border-border bg-surface p-3">
            <h3 className="mb-2 text-sm font-semibold text-foreground">Borç özeti</h3>
            {debt.byCurrency.length === 0 ? <p className="text-xs text-muted">Henüz satır/borç yok.</p> : (
              <div className="space-y-1 text-sm">
                {debt.byCurrency.map((c) => (
                  <div key={c.currency} className="flex items-center justify-between gap-2">
                    <span className="text-muted">{c.currency}</span>
                    <span className="text-foreground">ödenen {money(c.paid, c.currency)} / {money(c.total, c.currency)} ·
                      <span className={`ml-1 font-semibold ${c.remaining > 0 ? "text-danger" : "text-success"}`}>kalan {money(c.remaining, c.currency)}</span>
                    </span>
                  </div>
                ))}
              </div>
            )}
            {debt.lines.length > 0 && (
              <div className="mt-2 space-y-1 border-t border-border pt-2 text-xs">
                {debt.lines.map((l) => (
                  <div key={l.id} className="flex items-center justify-between gap-2">
                    <span className="text-muted">{l.classTypeName}{l.status !== "Active" && <span> ({studentContractStatusLabels[l.status]})</span>}</span>
                    <span className="text-foreground">borç {money(l.remaining, l.currency)} <span className="text-muted">(toplam {money(l.totalPrice, l.currency)} − ödenen {money(l.paid, l.currency)})</span></span>
                  </div>
                ))}
              </div>
            )}
            {debt.unassignedByCurrency.length > 0 && (
              <p className="mt-2 text-xs text-muted">Atanmamış ödemeler: {debt.unassignedByCurrency.map((u) => money(u.paid, u.currency)).join(" · ")} (hiçbir satıra sayılmaz)</p>
            )}
          </div>
        )}
        <div>
          <h3 className="mb-2 text-sm font-semibold text-foreground">Ödemeler</h3>
          <div className="space-y-2">
            {payments.length === 0 ? <p className="text-sm text-muted">Ödeme yok.</p> : payments.map((p) => (
              <div key={p.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
                <span>{p.amount.toLocaleString("tr-TR")} {p.currency} · <span className="text-muted">vade {p.dueDate}</span> · <span className="text-muted">{p.lineLabel ?? "Diğer ödeme"}</span></span>
                <div className="flex items-center gap-2">
                  <Badge tone={p.status === "Paid" ? "success" : p.status === "Overdue" ? "danger" : "warning"}>{paymentStatusLabels[p.status]}</Badge>
                  {(p.status === "Pending" || p.status === "Overdue") && (
                    <>
                      <Button variant="ghost" size="sm" disabled={busy} onClick={() => wrap(() => paymentService.markPaid(p.id))}>Ödendi</Button>
                      <Button variant="ghost" size="sm" disabled={busy} onClick={() => wrap(() => paymentService.cancel(p.id))}>İptal</Button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
          {activeLines.length === 0 ? (
            <p className="mt-2 text-xs text-muted">Önce bir sözleşme satırı (paket) ekleyin — ödeme bir pakete bağlanmak zorunda.</p>
          ) : (
            <div className="mt-2 flex flex-wrap gap-2">
              <Input type="number" min={0} step="0.01" placeholder="Tutar" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} className="w-28" />
              <Input type="date" value={payDue} onChange={(e) => setPayDue(e.target.value)} className="w-40" />
              <Select value={payLineId} onChange={(e) => setPayLineId(e.target.value)} className="w-56">
                <option value="">Paket seçin…</option>
                {activeLines.map((l) => <option key={l.id} value={l.id}>{l.classTypeName} · {l.startDate} ({l.currency})</option>)}
              </Select>
              <Button size="sm" disabled={busy} onClick={createPayment}>Ödeme ekle</Button>
            </div>
          )}
        </div>
      </div>
    </Modal>
  );
}

// ============================================================================
// Katılım özeti modalı.
// ============================================================================
export function AttendanceSummaryModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [summary, setSummary] = useState<AttendanceSummaryDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setSummary(await attendanceService.studentSummary(student.id, month)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Özet yüklenemedi."); }
    finally { setLoading(false); }
  }, [student.id, month]);
  useEffect(() => { load(); }, [load]);

  return (
    <Modal open onClose={onClose} title={`${student.fullName} — Katılım özeti`} size="lg">
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground" />
        {loading ? <p className="text-sm text-muted">Yükleniyor…</p> : summary ? (
          <div className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-5">
            <SumStat label="Geldi" value={summary.presentCount} />
            <SumStat label="Gelmedi" value={summary.absentCount} />
            <SumStat label="Mazeretli" value={summary.excusedCount} />
            <SumStat label="Geç İptal" value={summary.lateCancelCount} />
            <SumStat label="Katılım" value={summary.attendanceRate != null ? `%${Math.round(summary.attendanceRate * 100)}` : "—"} />
          </div>
        ) : null}
      </div>
    </Modal>
  );
}

function SumStat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-xl border border-border px-3 py-2 text-center">
      <p className="text-lg font-semibold text-foreground">{value}</p>
      <p className="text-xs text-muted">{label}</p>
    </div>
  );
}

// ============================================================================
// Öğrenci ekle/düzenle modalı.
// ============================================================================
export function GuestFormModal({ student, onClose, onSaved }: { student: StudentDto | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<GuestInput>(
    student
      ? {
          fullName: student.fullName, phone: student.phone ?? "", email: student.email ?? "", source: student.source,
          level: student.level ?? null, notes: student.notes ?? "", healthNotes: student.healthNotes ?? "",
          emergencyContactName: student.emergencyContactName ?? "", emergencyContactPhone: student.emergencyContactPhone ?? "",
          birthDate: student.birthDate ?? "",
        }
      : emptyGuest,
  );
  const [err, setErr] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const set = (k: keyof GuestInput) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true); setErr(null);
    try {
      const payload: GuestInput = { ...form, birthDate: form.birthDate || null };
      if (student) await studentService.update(student.id, payload);
      else await studentService.createGuest(payload);
      onSaved();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={student ? "Öğrenciyi düzenle" : "Misafir öğrenci ekle"} size="lg">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Ad soyad"><Input value={form.fullName} onChange={set("fullName")} required /></FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Telefon"><Input value={form.phone ?? ""} onChange={set("phone")} /></FormField>
          <FormField label="E-posta"><Input type="email" value={form.email ?? ""} onChange={set("email")} /></FormField>
          <FormField label="Kaynak">
            <Select value={form.source} onChange={(e) => setForm((f) => ({ ...f, source: e.target.value as StudentSource }))}>
              {Object.entries(studentSourceLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </Select>
          </FormField>
          <FormField label="Seviye">
            <Select value={form.level ?? ""} onChange={(e) => setForm((f) => ({ ...f, level: (e.target.value || null) as StudentLevel | null }))}>
              <option value="">Belirtilmemiş</option>
              {Object.entries(studentLevelLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </Select>
          </FormField>
          <FormField label="Doğum tarihi"><Input type="date" value={form.birthDate ?? ""} onChange={set("birthDate")} /></FormField>
          <FormField label="Acil durum kişisi"><Input value={form.emergencyContactName ?? ""} onChange={set("emergencyContactName")} /></FormField>
          <FormField label="Acil durum telefonu"><Input value={form.emergencyContactPhone ?? ""} onChange={set("emergencyContactPhone")} /></FormField>
        </div>
        <FormField label="Not"><Textarea value={form.notes ?? ""} onChange={set("notes")} /></FormField>
        <FormField label="Sağlık notu" hint="Hassas bilgi — yalnızca yetkili kullanıcılara gösterilir">
          <Textarea value={form.healthNotes ?? ""} onChange={set("healthNotes")} />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>İptal</Button>
          <Button type="submit" loading={saving}>{student ? "Kaydet" : "Oluştur"}</Button>
        </div>
      </form>
    </Modal>
  );
}

// ============================================================================
// Misafir → kullanıcı hesabına bağlama modalı.
// ============================================================================
export function ConvertModal({ student, onClose, onDone }: { student: StudentDto; onClose: () => void; onDone: () => void }) {
  const [email, setEmail] = useState(student.email ?? "");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true); setErr(null);
    try { await studentService.convertToUser(student.id, { email, initialPassword: password || undefined }); onDone(); }
    catch (e2) { setErr(e2 instanceof ApiError ? e2.message : "Dönüştürülemedi."); }
    finally { setBusy(false); }
  };

  return (
    <Modal open onClose={onClose} title={`${student.fullName} → Kullanıcıya bağla`}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-sm text-muted">E-posta sistemde varsa o hesaba bağlanır; yoksa yeni hesap oluşturulur. Öğrenci profili ve geçmişi korunur.</p>
        <FormField label="E-posta"><Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></FormField>
        <FormField label="Başlangıç parolası" hint="Yalnızca yeni hesap oluşturulursa gerekir (min 8 karakter)">
          <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="new-password" />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>İptal</Button>
          <Button type="submit" loading={busy}>Bağla</Button>
        </div>
      </form>
    </Modal>
  );
}
