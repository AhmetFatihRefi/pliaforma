"use client";

import { useCallback, useEffect, useState } from "react";
import { attendanceService, classTypeService, contractService, memberService, paymentService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useInstructors } from "@/lib/useInstructors";
import {
  type AttendanceSummaryDto,
  type ClassTypeDto,
  type ContractLinePaymentPlanDto,
  type DayOfWeek,
  type GuestInput,
  type InstallmentInput,
  type InstallmentStatus,
  type MemberDto,
  type PaymentDto,
  type PreferredSlotInput,
  type ReplanLinePreviewDto,
  type ReplanLineResultDto,
  type StudentContractDto,
  type StudentContractLineDto,
  type StudentDebtSummaryDto,
  type StudentDto,
  type StudentLevel,
  type StudentSource,
  daysOfWeek,
  dayLabels,
  installmentStatusLabels,
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
import { PaymentPlanEditor, planMatches, planMoney } from "./_payment-plan";

const installmentTone = (s: InstallmentStatus): "success" | "warning" | "danger" | "neutral" =>
  s === "Paid" ? "success" : s === "PartiallyPaid" ? "warning" : s === "Overdue" ? "danger" : "neutral";

export const emptyGuest: GuestInput = {
  fullName: "", phone: "", email: "", source: "Phone", level: null, notes: "", healthNotes: "",
  emergencyContactName: "", emergencyContactPhone: "", birthDate: "",
};

export const statusTone = (s: StudentDto["status"]) => (s === "Active" ? "success" : s === "Guest" ? "warning" : "neutral");

const today = () => new Date().toISOString().slice(0, 10);
const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;

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
  const [genMsg, setGenMsg] = useState<string | null>(null);
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");
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
  // Sprint 35: program üretimi line'ın KAYITLI haftalık slot düzenini (saatli preferred slots) kullanır — tek kaynak.
  const weeklySlots = l.preferredSlots.filter((s) => s.preferredStartTime);
  const weeklyCount = weeklySlots.length;
  const estWeeks = weeklyCount > 0 ? Math.ceil(l.availableLessonCount / weeklyCount) : 0;

  // Sprint 34: TERCİH DÜZENLE (full-replace) — hoca/kapasite/slot. Mevcut programa dokunmaz.
  const capModeOf = (v: number | null | undefined): "any" | "1" | "2" | "3" | "4" | "custom" =>
    v == null ? "any" : v === 1 ? "1" : v === 2 ? "2" : v === 3 ? "3" : v === 4 ? "4" : "custom";
  const [editInstr, setEditInstr] = useState(l.instructorProfileId ?? "");
  const [editCapMode, setEditCapMode] = useState(capModeOf(l.preferredMaxCapacity));
  const [editCustomCap, setEditCustomCap] = useState(l.preferredMaxCapacity && l.preferredMaxCapacity > 4 ? l.preferredMaxCapacity : 5);
  const [editSlots, setEditSlots] = useState<PreferredSlotInput[]>(
    l.preferredSlots.map((s) => ({ dayOfWeek: s.dayOfWeek, preferredStartTime: s.preferredStartTime ?? null, notes: null })),
  );
  const [prefMsg, setPrefMsg] = useState<string | null>(null);
  const editPreferredMaxCapacity = editCapMode === "any" ? null : editCapMode === "custom" ? editCustomCap : Number(editCapMode);

  // Sprint 34: REPLAN (kalan dersleri yeniden planla) — preview + apply.
  const [replanInstr, setReplanInstr] = useState("");
  const [replanCap, setReplanCap] = useState(2);
  const [replanPrev, setReplanPrev] = useState<ReplanLinePreviewDto | null>(null);
  const [replanResult, setReplanResult] = useState<ReplanLineResultDto | null>(null);
  const [replanMsg, setReplanMsg] = useState<string | null>(null);

  // Sprint 38: ödeme planı (taksit/vade) — yükle/düzenle.
  const [plan, setPlan] = useState<ContractLinePaymentPlanDto | null>(null);
  const [planEditing, setPlanEditing] = useState(false);
  const [planEdit, setPlanEdit] = useState<InstallmentInput[]>([]);
  const [planMsg, setPlanMsg] = useState<string | null>(null);
  const loadPlan = useCallback(() => {
    contractService.getPaymentPlan(l.id).then(setPlan).catch(() => setPlan(null));
  }, [l.id]);
  // Ödeme değişince (l.paid) yeniden hesapla.
  useEffect(() => { loadPlan(); }, [loadPlan, l.paid]);
  const savePlan = async () => {
    if (!planMatches(planEdit, l.totalPrice)) { setPlanMsg("Taksit toplamı sözleşme tutarına eşit olmalı."); return; }
    setBusy(true); setPlanMsg(null);
    try {
      const res = await contractService.updatePaymentPlan(l.id, planEdit);
      setPlan(res); setPlanEditing(false); onChanged();
    } catch (e) { setPlanMsg(e instanceof ApiError ? e.message : "Ödeme planı kaydedilemedi."); }
    finally { setBusy(false); }
  };

  const addEditSlot = () => {
    const t = slotTime ? `${slotTime}:00` : null;
    if (editSlots.some((s) => s.dayOfWeek === slotDay && (s.preferredStartTime ?? null) === t)) { setPrefMsg("Aynı gün/saat zaten ekli."); return; }
    setEditSlots((xs) => [...xs, { dayOfWeek: slotDay, preferredStartTime: t, notes: null }]); setSlotTime(""); setPrefMsg(null);
  };
  const savePreferences = async () => {
    if (editSlots.length === 0) { setPrefMsg("En az bir tercih gün/saat girilmeli."); return; }
    setBusy(true); setPrefMsg(null);
    try {
      await contractService.updatePreferences(l.id, { instructorProfileId: editInstr || null, preferredMaxCapacity: editPreferredMaxCapacity, preferredSlots: editSlots });
      setPrefMsg("✓ Tercihler kaydedildi. Mevcut programı etkilemez — kalan dersleri yeni tercihlere göre yeniden planlamak ister misin? (aşağıdaki bölüm)");
      onChanged();
    } catch (e) { setPrefMsg(e instanceof ApiError ? e.message : "Tercihler kaydedilemedi."); }
    finally { setBusy(false); }
  };
  const doReplanPreview = async () => {
    setBusy(true); setReplanMsg(null); setReplanResult(null);
    try {
      setReplanPrev(await contractService.replanPreview(l.id, { instructorProfileId: needInstructor ? replanInstr || null : null, capacity: needCapacity ? Number(replanCap) : null }));
    } catch (e) { setReplanMsg(e instanceof ApiError ? e.message : "Önizleme alınamadı."); }
    finally { setBusy(false); }
  };
  const doReplan = async () => {
    if (needInstructor && !replanInstr) { setReplanMsg("Bu sözleşmenin hocası belirtilmemiş; yeniden planlama için bir hoca seçin."); return; }
    if (!(await confirm({ title: "Kalan dersleri yeniden planla", confirmLabel: "Yeniden planla", message: "Tamamlanan derslere dokunulmaz; yalnız gelecekteki planlı dersler etkilenir. Grup derslerinde başka öğrenciler etkilenmez. Uygun ders yoksa eksik planlanır." }))) return;
    setBusy(true); setReplanMsg(null);
    try {
      const r = await contractService.replan(l.id, { instructorProfileId: needInstructor ? replanInstr || null : null, capacity: needCapacity ? Number(replanCap) : null });
      setReplanResult(r); setReplanPrev(null); onChanged();
    } catch (e) { setReplanMsg(e instanceof ApiError ? e.message : "Yeniden planlanamadı."); }
    finally { setBusy(false); }
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
    if (weeklyCount === 0) { setGenMsg("Önce “Tercihleri düzenle”den saatli gün/saat ekleyin (haftalık düzen)."); return; }
    if (needInstructor && !genInstructor) { setGenMsg("Bu sözleşmenin hocası belirtilmemiş; program için bir hoca seçin."); return; }
    if (needCapacity && !genCapacity) { setGenMsg("Ekipman bağlı ders için seans kapasitesi seçin."); return; }
    setBusy(true); setGenMsg(null);
    try {
      // Sprint 35: slot kaynağı = line'ın kayıtlı haftalık düzeni (saatli preferred slots). Manuel gün/saat picker yok.
      const r = await contractService.generateLineSchedule(l.id, {
        slots: weeklySlots.map((s) => ({ dayOfWeek: s.dayOfWeek, startTime: s.preferredStartTime! })),
        reuseExistingGroup: !l.isIndividual,
        instructorProfileId: needInstructor ? genInstructor : null,
        capacity: needCapacity ? Number(genCapacity) : null,
      });
      const whNote = r.skippedOutsideWorkingHoursCount > 0
        ? ` ${r.skippedOutsideWorkingHoursCount} ders çalışma saatleri dışında kaldığı için planlanamadı.`
        : "";
      setGenMsg(`✓ ${r.createdCount} ders oluşturuldu (yeni ${r.newSessionCount}, gruba bağlanan ${r.reusedSessionCount}, atlanan ${r.skippedCount}).${whNote}`);
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

        {/* Ödeme planı (Sprint 38) — taksit/vade takibi. Plan ≠ Payment (plan beklenen takvim). */}
        <div>
          <div className="flex items-center justify-between gap-2">
            <SectionTitle>Ödeme planı</SectionTitle>
            {isActive && !planEditing && (
              <Button size="sm" variant="secondary" onClick={() => { setPlanEdit(plan?.installments.map((i) => ({ dueDate: i.dueDate, amount: i.amount, notes: i.notes ?? null })) ?? []); setPlanMsg(null); setPlanEditing(true); }}>
                {plan?.isConfigured ? "Planı düzenle" : "Plan oluştur"}
              </Button>
            )}
          </div>

          {planMsg && <p className="mt-1 text-xs text-danger">{planMsg}</p>}

          {planEditing ? (
            <div className="mt-2 space-y-3">
              {(plan?.totalPaidAmount ?? 0) > 0 && (
                <Alert kind="warning">Bu sözleşme satırında ödeme var. Plan değişirse ödeme dağılımı yeniden hesaplanır.</Alert>
              )}
              <PaymentPlanEditor total={l.totalPrice} currency={l.currency} value={planEdit} onChange={setPlanEdit} />
              <div className="flex justify-end gap-2">
                <Button size="sm" variant="secondary" onClick={() => setPlanEditing(false)}>Vazgeç</Button>
                <Button size="sm" loading={busy} disabled={!planMatches(planEdit, l.totalPrice)} onClick={savePlan}>Planı kaydet</Button>
              </div>
            </div>
          ) : !plan?.isConfigured ? (
            <p className="mt-2 text-sm text-muted">Ödeme planı yok. {isActive ? "“Plan oluştur” ile peşin/taksitli plan tanımlayabilirsiniz." : ""}</p>
          ) : (
            <div className="mt-2 space-y-2">
              <div className="flex flex-wrap items-center gap-2 text-xs">
                <Badge tone="neutral">{plan.planType === "Cash" ? "Peşin" : `Taksitli (${plan.installments.length})`}</Badge>
                {plan.overdueAmount > 0 && <Badge tone="danger">Geciken {planMoney(plan.overdueAmount, plan.currency)}</Badge>}
                {plan.nextDueDate && <span className="text-muted">Sonraki ödeme: {plan.nextDueDate} · {planMoney(plan.nextDueAmount ?? 0, plan.currency)}</span>}
                {plan.overpaidAmount > 0 && <Badge tone="success">Fazla ödeme {planMoney(plan.overpaidAmount, plan.currency)}</Badge>}
                <span className="text-muted">{plan.paidCount}/{plan.installments.length} ödendi</span>
              </div>
              <ul className="divide-y divide-border rounded-xl border border-border">
                {plan.installments.map((i) => (
                  <li key={i.id} className="flex items-center justify-between gap-2 px-3 py-1.5 text-sm">
                    <div className="flex items-center gap-2">
                      <Badge tone={installmentTone(i.status)}>{installmentStatusLabels[i.status]}</Badge>
                      <span className="text-muted">{i.sequenceNo}. Vade {i.dueDate}</span>
                    </div>
                    <span className="text-foreground">
                      {planMoney(i.paidAmount, plan.currency)} / {planMoney(i.amount, plan.currency)}
                    </span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        {/* Tercihleri düzenle (Sprint 34) — hoca/kapasite/gün-saat full-replace; MEVCUT programa dokunmaz */}
        {!isActive ? (
          <div className="space-y-2">
            <SectionTitle>Tercih gün/saatleri</SectionTitle>
            <div className="flex flex-wrap gap-2">
              {l.preferredSlots.length === 0 ? <span className="text-sm text-muted">Tercih yok.</span> :
                l.preferredSlots.map((s) => (
                  <span key={s.id} className="rounded-full border border-border bg-surface px-2.5 py-1 text-xs text-foreground">
                    {dayLabels[s.dayOfWeek]}{s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : ""}
                  </span>
                ))}
            </div>
          </div>
        ) : (
          <div className="space-y-3 rounded-xl border border-border bg-surface-muted/40 p-3">
            <SectionTitle>Tercihleri düzenle</SectionTitle>
            <p className="text-xs text-muted">Hoca, kapasite ve gün/saat tercihleri. Kaydetmek <strong>mevcut programı değiştirmez</strong>; kalan dersleri güncellemek için aşağıdaki “Kalan dersleri yeniden planla”yı kullan.</p>
            <div className="grid gap-3 sm:grid-cols-2">
              <FormField label="Hoca (opsiyonel)" hint="Boş = herhangi bir hoca">
                <Select value={editInstr} onChange={(e) => setEditInstr(e.target.value)}>
                  <option value="">Farketmez (herhangi bir hoca)</option>
                  {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
                </Select>
              </FormField>
              <FormField label="Kapasite tercihi">
                <Select value={editCapMode} onChange={(e) => setEditCapMode(e.target.value as typeof editCapMode)}>
                  <option value="any">Farketmez</option>
                  <option value="1">Bireysel (1 kişi)</option>
                  <option value="2">En fazla 2</option>
                  <option value="3">En fazla 3</option>
                  <option value="4">En fazla 4</option>
                  <option value="custom">Diğer (sayı gir)</option>
                </Select>
              </FormField>
              {editCapMode === "custom" && (
                <FormField label="En fazla kaç kişi?" hint="1–50">
                  <Input type="number" min={1} max={50} value={editCustomCap} onChange={(e) => setEditCustomCap(Number(e.target.value))} />
                </FormField>
              )}
            </div>
            <div>
              <div className="mb-1 flex items-center gap-2">
                <p className="text-xs text-muted">Haftalık ders gün/saatleri (en az 1) — haftada 2 ders için 2 farklı gün/saat ekleyin.</p>
                {editSlots.filter((s) => s.preferredStartTime).length > 0 && (
                  <Badge tone="rose">Haftada {editSlots.filter((s) => s.preferredStartTime).length} ders</Badge>
                )}
              </div>
              <div className="mb-2 flex flex-wrap gap-2">
                {editSlots.length === 0 ? <span className="text-xs text-muted">Henüz tercih yok.</span> :
                  editSlots.map((s, i) => (
                    <span key={i} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-2.5 py-1 text-xs text-foreground">
                      {dayLabels[s.dayOfWeek]}{s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : " (saat farketmez)"}
                      <button type="button" className="text-muted hover:text-danger" onClick={() => setEditSlots((xs) => xs.filter((_, j) => j !== i))}>×</button>
                    </span>
                  ))}
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Select value={slotDay} onChange={(e) => setSlotDay(e.target.value as DayOfWeek)} className="w-36">
                  {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
                </Select>
                <Input type="time" value={slotTime} onChange={(e) => setSlotTime(e.target.value)} className="w-32" />
                <Button size="sm" variant="secondary" onClick={addEditSlot}>+ Ekle</Button>
                <span className="text-xs text-muted">Saat boş = farketmez (üretimde kullanılmaz)</span>
              </div>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <Button size="sm" loading={busy} onClick={savePreferences}>Tercihleri kaydet</Button>
              {prefMsg && <p className="text-xs text-muted">{prefMsg}</p>}
            </div>
          </div>
        )}

        {/* Programı oluştur (Sprint 35) — kayıtlı HAFTALIK slot düzenini kullanır; manuel gün/saat picker yok */}
        {isActive && (
          <div className="space-y-3 rounded-xl border border-border bg-surface-muted/40 p-3">
            <SectionTitle>Programı oluştur</SectionTitle>
            {weeklyCount === 0 ? (
              <p className="text-xs text-warning">Bu satırda saatli haftalık gün/saat tercihi yok. Yukarıdan “Tercihleri düzenle” ile haftalık düzeni (örn. Pzt 10:00 + Per 18:00) ekleyin.</p>
            ) : (
              <>
                <div className="flex flex-wrap items-center gap-2">
                  <Badge tone="rose">Haftada {weeklyCount} ders düzeni</Badge>
                  <div className="flex flex-wrap gap-1.5">
                    {weeklySlots.map((s, i) => (
                      <span key={i} className="rounded-full border border-border bg-surface px-2 py-0.5 text-xs text-foreground">{dayLabels[s.dayOfWeek]} {s.preferredStartTime!.slice(0, 5)}</span>
                    ))}
                  </div>
                </div>
                <p className="text-xs text-muted">Haftada 2 ders için 2 farklı gün/saat ekleyin. Sistem toplam ders hakkı bitene kadar bu haftalık düzeni tekrarlar. <strong>{l.availableLessonCount} ders / haftada {weeklyCount} slot → yaklaşık {estWeeks} hafta.</strong></p>
              </>
            )}
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
            <div className="flex flex-wrap items-center gap-3">
              <Button size="sm" loading={busy} disabled={weeklyCount === 0} onClick={generate}>Haftalık düzene göre oluştur ({l.availableLessonCount} ders)</Button>
            </div>
            {genMsg && <p className="text-xs text-muted">{genMsg}</p>}
          </div>
        )}

        {/* Kalan dersleri yeniden planla (Sprint 34) — preview + apply; Completed/geçmiş dokunulmaz, grup öğrencileri etkilenmez */}
        {isActive && (
          <div className="space-y-2 rounded-xl border border-border bg-surface-muted/40 p-3">
            <SectionTitle>Kalan dersleri yeniden planla</SectionTitle>
            <p className="text-xs text-muted">Güncel tercihlere (hoca/kapasite/gün-saat) göre gelecekteki planlı dersleri yeniden oluşturur. <strong>Tamamlanan dersler korunur</strong>; grup derslerinde diğer öğrenciler etkilenmez.</p>
            {(needInstructor || needCapacity) && (
              <div className="grid gap-3 sm:grid-cols-2">
                {needInstructor && (
                  <FormField label="Hoca (zorunlu)" hint="Sözleşmede hoca belirtilmemiş.">
                    <Select value={replanInstr} onChange={(e) => setReplanInstr(e.target.value)}>
                      <option value="">Seçin…</option>
                      {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
                    </Select>
                  </FormField>
                )}
                {needCapacity && (
                  <FormField label="Seans kapasitesi (zorunlu)" hint="Ekipman bağlı (1–50).">
                    <Input type="number" min={1} max={50} value={replanCap} onChange={(e) => setReplanCap(Number(e.target.value))} />
                  </FormField>
                )}
              </div>
            )}
            <div className="flex flex-wrap items-center gap-2">
              <Button size="sm" variant="secondary" loading={busy} onClick={doReplanPreview}>Önizle</Button>
              {replanPrev && <Button size="sm" loading={busy} onClick={doReplan}>Uygula</Button>}
            </div>
            {replanPrev && (
              <div className="rounded-lg border border-border bg-surface p-3 text-xs text-foreground">
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
                  <Stat label="Tamamlanan" value={replanPrev.deliveredCount} />
                  <Stat label="İptal edilecek" value={replanPrev.futureBookingsToCancel} />
                  <Stat label="Yeniden üretilecek" value={replanPrev.plannableCount} tone="success" />
                  <Stat label="Planlanamayan" value={replanPrev.notPlannableCount} tone={replanPrev.notPlannableCount > 0 ? "danger" : "success"} />
                </div>
                {replanPrev.warnings.length > 0 && (
                  <ul className="mt-2 list-disc pl-5 text-warning">{replanPrev.warnings.map((w, i) => <li key={i}>{w}</li>)}</ul>
                )}
              </div>
            )}
            {replanResult && (
              <div className="rounded-lg border border-success/40 bg-success/10 p-3 text-xs text-foreground">
                ✓ {replanResult.cancelledFutureBookings} ders iptal/taşındı, {replanResult.createdCount} ders yeniden planlandı
                (yeni {replanResult.newSessionCount}, gruba bağlanan {replanResult.reusedSessionCount}).
                {replanResult.notPlannedCount > 0 && <span className="text-danger"> {replanResult.notPlannedCount} ders uygunluk olmadığı için planlanamadı.</span>}
                {replanResult.skippedOutsideWorkingHoursCount > 0 && <span className="text-warning"> {replanResult.skippedOutsideWorkingHoursCount} ders çalışma saatleri dışında kaldı.</span>}
              </div>
            )}
            {replanMsg && <p className="text-xs text-danger">{replanMsg}</p>}
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
