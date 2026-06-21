"use client";

import { useCallback, useEffect, useState } from "react";
import { attendanceService, classTypeService, contractService, memberService, paymentService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type AttendanceSummaryDto,
  type ClassTypeDto,
  type ContractScheduleSlotInput,
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
  studentStatusLabels,
} from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  FormField,
  Input,
  Modal,
  Select,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  Textarea,
  TR,
} from "@/components/ui";

const emptyGuest: GuestInput = {
  fullName: "", phone: "", email: "", source: "Phone", level: null, notes: "", healthNotes: "",
  emergencyContactName: "", emergencyContactPhone: "", birthDate: "",
};

const statusTone = (s: StudentDto["status"]) => (s === "Active" ? "success" : s === "Guest" ? "warning" : "neutral");

export default function StudentsPage() {
  const { canManage } = useRole();
  const [items, setItems] = useState<StudentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");

  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<StudentDto | null>(null);
  const [convertFor, setConvertFor] = useState<StudentDto | null>(null);
  const [summaryFor, setSummaryFor] = useState<StudentDto | null>(null);
  const [financeFor, setFinanceFor] = useState<StudentDto | null>(null);
  const [contractsForStudent, setContractsForStudent] = useState<StudentDto | null>(null);

  const load = useCallback(async (q: string) => {
    setLoading(true);
    try {
      setItems(q.trim() ? await studentService.search(q.trim()) : await studentService.list());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Öğrenciler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const t = setTimeout(() => load(search), 300);
    return () => clearTimeout(t);
  }, [search, load]);

  const openCreate = () => {
    setEditing(null);
    setFormOpen(true);
  };
  const openEdit = (s: StudentDto) => {
    setEditing(s);
    setFormOpen(true);
  };

  const toggleActive = async (s: StudentDto) => {
    try {
      if (s.isActive) await studentService.deactivate(s.id);
      else await studentService.reactivate(s.id);
      await load(search);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "İşlem başarısız.");
    }
  };

  // Sprint 26 ek (v1.1.1): öğrenci yönetimi yalnız stüdyo yetkilisi (Instructor doğrudan URL ile giremez).
  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Öğrenciler</h1>
          <p className="mt-1 text-sm text-muted">Misafir oluşturun, gerçek hesaba bağlayın, yönetin.</p>
        </div>
        <Button onClick={openCreate}>+ Misafir ekle</Button>
      </div>

      <Input placeholder="Ara: ad, telefon veya e-posta…" value={search} onChange={(e) => setSearch(e.target.value)} />

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>İsim</TH>
            <TH>İletişim</TH>
            <TH>Durum</TH>
            <TH>Kaynak</TH>
            <TH className="text-right">İşlem</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={5}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={5}>{search ? "Eşleşen öğrenci yok." : "Henüz öğrenci yok."}</TableEmpty>
          ) : (
            items.map((s) => (
              <TR key={s.id}>
                <TD>
                  <span className="font-medium">{s.fullName}</span>
                  {s.healthNotes && <span className="ml-2 text-xs text-warning">⚕ sağlık notu</span>}
                </TD>
                <TD className="text-muted">{s.phone || s.email || "—"}</TD>
                <TD><Badge tone={statusTone(s.status)}>{studentStatusLabels[s.status]}</Badge></TD>
                <TD><Badge tone="neutral">{studentSourceLabels[s.source]}</Badge></TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => setSummaryFor(s)}>Katılım</Button>
                    <Button variant="ghost" size="sm" onClick={() => setContractsForStudent(s)}>Sözleşmeler</Button>
                    <Button variant="ghost" size="sm" onClick={() => setFinanceFor(s)}>Ödemeler</Button>
                    <Button variant="ghost" size="sm" onClick={() => openEdit(s)}>Düzenle</Button>
                    {s.status === "Guest" && (
                      <Button variant="ghost" size="sm" onClick={() => setConvertFor(s)}>Dönüştür</Button>
                    )}
                    <Button variant="ghost" size="sm" onClick={() => toggleActive(s)}>
                      {s.isActive ? "Pasifleştir" : "Aktifleştir"}
                    </Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      {formOpen && (
        <GuestFormModal student={editing} onClose={() => setFormOpen(false)} onSaved={() => { setFormOpen(false); load(search); }} />
      )}
      {convertFor && (
        <ConvertModal student={convertFor} onClose={() => setConvertFor(null)} onDone={() => { setConvertFor(null); load(search); }} />
      )}
      {summaryFor && (
        <AttendanceSummaryModal student={summaryFor} onClose={() => setSummaryFor(null)} />
      )}
      {financeFor && (
        <StudentFinanceModal student={financeFor} onClose={() => setFinanceFor(null)} />
      )}
      {contractsForStudent && (
        <ContractsModal student={contractsForStudent} onClose={() => setContractsForStudent(null)} />
      )}
    </div>
  );
}

// ===== Sprint 18a: Öğrenci sözleşmeleri + sözleşmeden program =====
// Sprint 28B: sözleşme = header + line'lar. Birincil görünüm LINE kartları (+ Yeni line ekle / edit / cancel).
// Header'lar yalnız "Programı oluştur" (schedule gen 28C'ye kadar header-bazlı). Amendment akışı kaldırıldı.
function ContractsModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [contracts, setContracts] = useState<StudentContractDto[]>([]);
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [instructors, setInstructors] = useState<MemberDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const today = new Date().toISOString().slice(0, 10);
  const plus = (days: number) => new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
  const emptyLine = { headerId: "", classTypeId: "", instructorProfileId: "", startDate: today, endDate: plus(60),
    lessonCount: 10, pricePerLesson: 0, makeupAllowance: 2, dueDate: "", isIndividual: false, currency: "TRY" };
  const [form, setForm] = useState(emptyLine);
  // Sprint 29A: yeni line'da EN AZ 1 tercih gün/saat zorunlu (gün zorunlu, saat opsiyonel; aynı gün+saat tekrar edemez).
  const [prefSlots, setPrefSlots] = useState<PreferredSlotInput[]>([]);
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");
  const addPrefSlot = () => {
    const t = slotTime ? `${slotTime}:00` : null;
    if (prefSlots.some((s) => s.dayOfWeek === slotDay && (s.preferredStartTime ?? null) === t)) {
      setErr("Aynı gün ve saat tercihi zaten eklendi."); return;
    }
    setErr(null);
    setPrefSlots((xs) => [...xs, { dayOfWeek: slotDay, preferredStartTime: t, notes: null }]);
  };
  const slotChipLabel = (s: PreferredSlotInput) => `${dayLabels[s.dayOfWeek]}${s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : ""}`;

  const load = useCallback(async () => {
    try {
      const [cs, ct, ms, ls] = await Promise.all([
        contractService.forStudent(student.id),
        classTypeService.list(true),
        memberService.list(),
        contractService.lines(student.id).catch(() => [] as StudentContractLineDto[]),
      ]);
      setContracts(cs);
      setClassTypes(ct);
      setInstructors(ms.filter((m) => m.role === "Instructor" && m.instructorProfileId));
      setLines(ls);
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    }
  }, [student.id]);

  useEffect(() => { load(); }, [load]);

  const activeHeaders = contracts.filter((c) => c.status === "Active");

  const addLine = async (e: React.FormEvent) => {
    e.preventDefault();
    if (prefSlots.length === 0) { setErr("En az bir tercih gün/saat ekleyin."); return; }
    setErr(null); setBusy(true);
    try {
      await contractService.addLine(student.id, {
        headerId: form.headerId || null,
        classTypeId: form.classTypeId,
        instructorProfileId: form.instructorProfileId,
        startDate: form.startDate,
        endDate: form.endDate,
        lessonCount: Number(form.lessonCount),
        pricePerLesson: Number(form.pricePerLesson),
        makeupAllowance: Number(form.makeupAllowance),
        dueDate: form.dueDate || null,
        isIndividual: form.isIndividual,
        currency: form.currency || "TRY",
        preferredSlots: prefSlots,
      });
      setForm(emptyLine);
      setPrefSlots([]); setSlotTime("");
      await load();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Satır eklenemedi.");
    } finally {
      setBusy(false);
    }
  };

  const cancelHeader = async (id: string) => {
    if (!confirm("Sözleşme (header) iptal edilsin mi?")) return;
    try { await contractService.cancel(id); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İptal edilemedi."); }
  };

  const computedTotal = (Number(form.lessonCount) || 0) * (Number(form.pricePerLesson) || 0);

  return (
    <Modal open onClose={onClose} title={`Sözleşmeler — ${student.fullName}`}>
      <div className="space-y-5">
        {err && <Alert kind="error">{err}</Alert>}

        {/* Yeni line (ek alım). Header verilmezse aktif header'a; yoksa yeni header otomatik. */}
        <form onSubmit={addLine} className="grid grid-cols-2 gap-3 rounded-xl border border-border bg-surface p-3">
          <p className="col-span-2 text-xs font-semibold text-muted">Yeni satır (alım) ekle</p>
          {activeHeaders.length > 1 && (
            <FormField label="Sözleşme (header)" hint="Boş = en güncel aktif / yeni">
              <Select value={form.headerId} onChange={(e) => setForm((f) => ({ ...f, headerId: e.target.value }))}>
                <option value="">Otomatik (en güncel aktif / yeni)</option>
                {activeHeaders.map((c) => <option key={c.id} value={c.id}>{c.classTypeName} · {c.startDate}</option>)}
              </Select>
            </FormField>
          )}
          <FormField label="Ders grubu">
            <Select value={form.classTypeId} onChange={(e) => setForm((f) => ({ ...f, classTypeId: e.target.value }))} required>
              <option value="">Seçin…</option>
              {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Hoca">
            <Select value={form.instructorProfileId} onChange={(e) => setForm((f) => ({ ...f, instructorProfileId: e.target.value }))} required>
              <option value="">Seçin…</option>
              {instructors.map((m) => <option key={m.instructorProfileId!} value={m.instructorProfileId!}>{m.fullName}</option>)}
            </Select>
          </FormField>
          <FormField label="Başlangıç"><Input type="date" value={form.startDate} onChange={(e) => setForm((f) => ({ ...f, startDate: e.target.value }))} required /></FormField>
          <FormField label="Bitiş"><Input type="date" value={form.endDate} onChange={(e) => setForm((f) => ({ ...f, endDate: e.target.value }))} required /></FormField>
          <FormField label="Ders sayısı"><Input type="number" min={1} value={form.lessonCount} onChange={(e) => setForm((f) => ({ ...f, lessonCount: Number(e.target.value) }))} /></FormField>
          <FormField label="Ders başı ücret"><Input type="number" min={0} step="0.01" value={form.pricePerLesson} onChange={(e) => setForm((f) => ({ ...f, pricePerLesson: Number(e.target.value) }))} /></FormField>
          <FormField label="Toplam ücret" hint="ders sayısı × ders başı (otomatik)">
            <Input value={`${computedTotal.toLocaleString("tr-TR")} ${form.currency}`} readOnly disabled />
          </FormField>
          <FormField label="Para birimi"><Input value={form.currency} onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value.toUpperCase() }))} maxLength={8} /></FormField>
          <FormField label="Telafi hakkı"><Input type="number" min={0} value={form.makeupAllowance} onChange={(e) => setForm((f) => ({ ...f, makeupAllowance: Number(e.target.value) }))} /></FormField>
          <FormField label="Son ödeme tarihi (ops.)"><Input type="date" value={form.dueDate} onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))} /></FormField>
          <FormField label="Bireysel mi?">
            <label className="flex h-9 items-center gap-2 text-sm text-muted">
              <input type="checkbox" checked={form.isIndividual} onChange={(e) => setForm((f) => ({ ...f, isIndividual: e.target.checked }))} />
              Bireysel dersler
            </label>
          </FormField>
          {/* Sprint 29A: tercih gün/saatleri — en az 1 zorunlu (gün zorunlu, saat opsiyonel). */}
          <div className="col-span-2 space-y-2 rounded-lg border border-dashed border-border p-2">
            <p className="text-xs font-semibold text-muted">Tercih gün/saatleri (en az 1)</p>
            <div className="flex flex-wrap items-end gap-2">
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
                  <span key={i} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-2 py-0.5 text-xs text-foreground">
                    {slotChipLabel(s)}
                    <button type="button" className="text-muted hover:text-danger" onClick={() => setPrefSlots((xs) => xs.filter((_, j) => j !== i))}>×</button>
                  </span>
                ))}
              </div>
            )}
          </div>
          <div className="col-span-2 flex justify-end">
            <Button type="submit" loading={busy}>Satır ekle</Button>
          </div>
        </form>

        {/* Line kartları (birincil) — ödeme/borç + edit/cancel. */}
        <div className="space-y-2">
          <p className="text-xs font-semibold text-muted">Sözleşme satırları</p>
          {lines.length === 0 ? (
            <p className="text-sm text-muted">Henüz satır yok.</p>
          ) : (
            lines.map((l) => <LineCard key={l.id} line={l} onChanged={load} onError={setErr} />)
          )}
        </div>

        {/* Header'lar — yalnız "Programı oluştur" (schedule gen 28C'ye kadar header-bazlı). */}
        {contracts.length > 0 && (
          <div className="space-y-3 border-t border-border pt-3">
            <p className="text-xs font-semibold text-muted">Program (header bazlı — geçiş; line-bazlı program 28C)</p>
            {contracts.map((c) => <ContractRow key={c.id} contract={c} onChanged={load} onCancel={() => cancelHeader(c.id)} />)}
          </div>
        )}
      </div>
    </Modal>
  );
}

// Sprint 28B: line kartı — tam alanlar + ödenen/kalan + edit (DueDate/Notes) + güvenli cancel.
function LineCard({ line: l, onChanged, onError }: { line: StudentContractLineDto; onChanged: () => void; onError: (m: string) => void }) {
  const [editing, setEditing] = useState(false);
  const [due, setDue] = useState(l.dueDate ?? "");
  const [busy, setBusy] = useState(false);
  // Sprint 28C-1: line-bazlı program üretimi (bireysellik line'dan; bireysel line gruba bağlanmaz).
  const [genOpen, setGenOpen] = useState(false);
  const [days, setDays] = useState<DayOfWeek[]>([]);
  const [time, setTime] = useState("18:00");
  const [reuse, setReuse] = useState(!l.isIndividual);
  const [genMsg, setGenMsg] = useState<string | null>(null);
  // Sprint 29A: tercih gün/saatleri (chip + ekle/sil).
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");
  const [slotBusy, setSlotBusy] = useState(false);
  const addSlot = async () => {
    setSlotBusy(true);
    try { await contractService.addPreferredSlot(l.id, { dayOfWeek: slotDay, preferredStartTime: slotTime ? `${slotTime}:00` : null, notes: null }); setSlotTime(""); onChanged(); }
    catch (e) { onError(e instanceof ApiError ? e.message : "Tercih eklenemedi."); }
    finally { setSlotBusy(false); }
  };
  const removeSlot = async (slotId: string) => {
    setSlotBusy(true);
    try { await contractService.removePreferredSlot(slotId); onChanged(); }
    catch (e) { onError(e instanceof ApiError ? e.message : "Tercih kaldırılamadı."); }
    finally { setSlotBusy(false); }
  };

  const saveEdit = async () => {
    setBusy(true);
    try { await contractService.updateLine(l.id, { dueDate: due || null, notes: null }); setEditing(false); onChanged(); }
    catch (e) { onError(e instanceof ApiError ? e.message : "Güncellenemedi."); }
    finally { setBusy(false); }
  };
  const cancelLine = async () => {
    if (!confirm("Bu satır iptal edilsin mi? (bağlı ödeme/ders/telafi varsa engellenir)")) return;
    setBusy(true);
    try { await contractService.cancelLine(l.id); onChanged(); }
    catch (e) { onError(e instanceof ApiError ? e.message : "İptal edilemedi."); }
    finally { setBusy(false); }
  };
  const generate = async () => {
    if (days.length === 0) { setGenMsg("En az bir gün seçin."); return; }
    setBusy(true); setGenMsg(null);
    try {
      const r = await contractService.generateLineSchedule(l.id, {
        slots: days.map((d) => ({ dayOfWeek: d, startTime: `${time}:00` })),
        reuseExistingGroup: l.isIndividual ? false : reuse,
      });
      setGenMsg(`${r.createdCount} ders oluşturuldu (yeni ${r.newSessionCount}, gruba bağlanan ${r.reusedSessionCount}, atlanan ${r.skippedCount}).`);
      onChanged();
    } catch (e) { setGenMsg(e instanceof ApiError ? e.message : "Program oluşturulamadı."); }
    finally { setBusy(false); }
  };

  return (
    <div className="rounded-xl border border-border bg-surface p-3 text-xs">
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-sm font-medium text-foreground">
          {l.classTypeName} · {l.instructorName}
          {l.isIndividual && <Badge tone="warning">Bireysel</Badge>}
        </span>
        <div className="flex items-center gap-2">
          <Badge tone={l.status === "Active" ? "success" : "neutral"}>{studentContractStatusLabels[l.status]}</Badge>
          {l.status === "Active" && <Button variant="ghost" size="sm" onClick={() => setGenOpen((v) => !v)}>Programı oluştur</Button>}
          {l.status === "Active" && <Button variant="ghost" size="sm" onClick={() => { setDue(l.dueDate ?? ""); setEditing((v) => !v); }}>Düzenle</Button>}
          {l.status === "Active" && <Button variant="ghost" size="sm" disabled={busy} onClick={cancelLine}>İptal</Button>}
        </div>
      </div>
      <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-0.5 text-muted sm:grid-cols-3">
        <span>Tarih: {l.startDate} → {l.endDate}</span>
        <span>Ders: {l.scheduledLessonCount}/{l.lessonCount} (kalan {l.availableLessonCount})</span>
        <span>Telafi hakkı: {l.makeupAllowance}</span>
        <span>Ders başı: {l.pricePerLesson.toLocaleString("tr-TR")} {l.currency}</span>
        <span>Toplam: {l.totalPrice.toLocaleString("tr-TR")} {l.currency}</span>
        <span>Son ödeme: {l.dueDate ?? "Belirlenmedi"}</span>
        <span>Ödenen: {l.paid.toLocaleString("tr-TR")} {l.currency}</span>
        <span className={l.remaining > 0 ? "text-danger" : "text-success"}>Kalan: {l.remaining.toLocaleString("tr-TR")} {l.currency}</span>
      </div>
      {/* Sprint 29A: tercih gün/saatleri. */}
      <div className="mt-2 border-t border-border pt-2">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-muted">Tercih gün/saatleri:</span>
          {l.preferredSlots.length === 0 ? (
            <span className="text-muted">—</span>
          ) : (
            l.preferredSlots.map((s) => (
              <span key={s.id} className="inline-flex items-center gap-1 rounded-full border border-border bg-background px-2 py-0.5 text-foreground">
                {dayLabels[s.dayOfWeek]}{s.preferredStartTime ? ` ${s.preferredStartTime.slice(0, 5)}` : ""}
                {l.status === "Active" && <button type="button" disabled={slotBusy} className="text-muted hover:text-danger" onClick={() => removeSlot(s.id)}>×</button>}
              </span>
            ))
          )}
        </div>
        {l.status === "Active" && (
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <Select value={slotDay} onChange={(e) => setSlotDay(e.target.value as DayOfWeek)} className="w-32">
              {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
            </Select>
            <Input type="time" value={slotTime} onChange={(e) => setSlotTime(e.target.value)} className="w-28" />
            <Button size="sm" variant="ghost" disabled={slotBusy} onClick={addSlot}>+ Tercih ekle</Button>
            <span className="text-muted">Saat boş = farketmez</span>
          </div>
        )}
      </div>
      {editing && (
        <div className="mt-2 flex items-center gap-2 border-t border-border pt-2">
          <span className="text-muted">Son ödeme tarihi:</span>
          <Input type="date" value={due} onChange={(e) => setDue(e.target.value)} className="w-40" />
          <Button size="sm" loading={busy} onClick={saveEdit}>Kaydet</Button>
          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>Vazgeç</Button>
        </div>
      )}
      {genOpen && l.status === "Active" && (
        <div className="mt-2 space-y-2 border-t border-border pt-2">
          <div className="flex flex-wrap gap-2">
            {daysOfWeek.map((d) => (
              <label key={d} className={`cursor-pointer rounded-lg border px-2 py-1 text-xs ${days.includes(d) ? "border-primary bg-primary-soft text-primary" : "border-border text-muted"}`}>
                <input type="checkbox" className="hidden" checked={days.includes(d)}
                  onChange={(e) => setDays((xs) => e.target.checked ? [...xs, d] : xs.filter((x) => x !== d))} />
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
    </div>
  );
}

// Sprint 28B: amendment akışı kaldırıldı (yeni alım = yeni line). Header yalnız "Programı oluştur" (schedule gen 28C'ye kadar header-bazlı).
function ContractRow({ contract, onChanged, onCancel }: { contract: StudentContractDto; onChanged: () => void; onCancel: () => void }) {
  const [genOpen, setGenOpen] = useState(false);
  const [days, setDays] = useState<DayOfWeek[]>([]);
  const [time, setTime] = useState("18:00");
  const [reuse, setReuse] = useState(true);
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const slots = (): ContractScheduleSlotInput[] => days.map((d) => ({ dayOfWeek: d, startTime: `${time}:00` }));

  const generate = async () => {
    if (days.length === 0) { setMsg("En az bir gün seçin."); return; }
    setBusy(true); setMsg(null);
    try {
      const r = await contractService.generateSchedule(contract.id, { slots: slots(), reuseExistingGroup: reuse, individual: null });
      setMsg(`${r.createdCount} ders oluşturuldu (yeni seans ${r.newSessionCount}, gruba bağlanan ${r.reusedSessionCount}, atlanan ${r.skippedCount}).`);
      onChanged();
    } catch (e) {
      setMsg(e instanceof ApiError ? e.message : "Program oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="rounded-xl border border-border bg-surface p-3">
      <div className="flex items-center justify-between gap-2">
        <div>
          <p className="text-sm font-medium text-foreground">
            {contract.classTypeName} · {contract.instructorName} {contract.isIndividualDefault && <Badge tone="warning">Bireysel</Badge>}
          </p>
          <p className="text-xs text-muted">
            {contract.startDate} → {contract.endDate} · {contract.scheduledLessonCount}/{contract.totalLessonCount} ders ·
            kalan {contract.availableLessonCount} · telafi {contract.makeupAllowance} · {contract.price} {contract.currency}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Badge tone={contract.status === "Active" ? "success" : "neutral"}>{studentContractStatusLabels[contract.status]}</Badge>
          {contract.status === "Active" && (
            <>
              <Button variant="ghost" size="sm" onClick={() => setGenOpen((v) => !v)}>Programı oluştur</Button>
              <Button variant="ghost" size="sm" onClick={onCancel}>İptal</Button>
            </>
          )}
        </div>
      </div>

      {genOpen && contract.status === "Active" && (
        <div className="mt-3 space-y-2 border-t border-border pt-3">
          <div className="flex flex-wrap gap-2">
            {daysOfWeek.map((d) => (
              <label key={d} className={`cursor-pointer rounded-lg border px-2 py-1 text-xs ${days.includes(d) ? "border-primary bg-primary-soft text-primary" : "border-border text-muted"}`}>
                <input type="checkbox" className="hidden" checked={days.includes(d)}
                  onChange={(e) => setDays((xs) => e.target.checked ? [...xs, d] : xs.filter((x) => x !== d))} />
                {dayLabels[d]}
              </label>
            ))}
          </div>
          <div className="flex items-center gap-3">
            <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} className="w-32" />
            <label className="flex items-center gap-2 text-xs text-muted">
              <input type="checkbox" checked={reuse} onChange={(e) => setReuse(e.target.checked)} /> Mevcut gruba bağlan
            </label>
            <Button size="sm" loading={busy} onClick={generate}>Oluştur ({contract.availableLessonCount} ders)</Button>
          </div>
          {msg && <p className="text-xs text-muted">{msg}</p>}
        </div>
      )}
    </div>
  );
}

// Sprint 28B: borç LINE-bazlı (currency özet + line kalemleri + atanmamış); ödeme LINE'a bağlanır ("Diğer ödeme" = line yok).
function StudentFinanceModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [debt, setDebt] = useState<StudentDebtSummaryDto | null>(null);
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [payAmount, setPayAmount] = useState("");
  const [payDue, setPayDue] = useState(new Date().toISOString().slice(0, 10));
  const [payLineId, setPayLineId] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const [ps, d, ls] = await Promise.all([
        paymentService.forStudent(student.id),
        contractService.debt(student.id),
        contractService.lines(student.id),
      ]);
      setPayments(ps); setDebt(d); setLines(ls);
      setErr(null);
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
    // Sprint 29A (strict payment): ödeme bir sözleşme satırına (paket) bağlanmak zorunda.
    if (!payLineId) { setErr("Ödeme için bir sözleşme satırı (paket) seçin."); return; }
    wrap(() => paymentService.create(student.id, {
      amount: Number(payAmount), dueDate: payDue, studentContractLineId: payLineId,
    })).then(() => { setPayAmount(""); setPayLineId(""); });
  };

  const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;
  const activeLines = lines.filter((l) => l.status === "Active");

  return (
    <Modal open onClose={onClose} title={`${student.fullName} — Finans`}>
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}

        {/* Borç özeti — para birimi bazında (cross-currency tek toplam YOK) + line kalemleri. */}
        {debt && (
          <div className="rounded-xl border border-border bg-surface p-3">
            <h3 className="mb-2 text-sm font-semibold text-foreground">Borç özeti</h3>
            {debt.byCurrency.length === 0 ? (
              <p className="text-xs text-muted">Henüz satır/borç yok.</p>
            ) : (
              <div className="space-y-1 text-sm">
                {debt.byCurrency.map((c) => (
                  <div key={c.currency} className="flex items-center justify-between gap-2">
                    <span className="text-muted">{c.currency}</span>
                    <span className="text-foreground">
                      ödenen {money(c.paid, c.currency)} / {money(c.total, c.currency)} ·
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
          {/* Sprint 29A (strict payment): ödeme bir paket (line) seçilmeden eklenemez; "Diğer ödeme" kaldırıldı. */}
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

        <div className="flex justify-end pt-2">
          <Button variant="secondary" onClick={onClose}>Kapat</Button>
        </div>
      </div>
    </Modal>
  );
}

function AttendanceSummaryModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [month, setMonth] = useState(new Date().toISOString().slice(0, 7));
  const [summary, setSummary] = useState<AttendanceSummaryDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSummary(await attendanceService.studentSummary(student.id, month));
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Özet yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [student.id, month]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <Modal open onClose={onClose} title={`${student.fullName} — Katılım özeti`}>
      <div className="space-y-3">
        {err && <Alert kind="error">{err}</Alert>}
        <input
          type="month"
          value={month}
          onChange={(e) => setMonth(e.target.value)}
          className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground"
        />
        {loading ? (
          <p className="text-sm text-muted">Yükleniyor…</p>
        ) : summary ? (
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

function GuestFormModal({ student, onClose, onSaved }: { student: StudentDto | null; onClose: () => void; onSaved: () => void }) {
  const [form, setForm] = useState<GuestInput>(
    student
      ? {
          fullName: student.fullName,
          phone: student.phone ?? "",
          email: student.email ?? "",
          source: student.source,
          level: student.level ?? null,
          notes: student.notes ?? "",
          healthNotes: student.healthNotes ?? "",
          emergencyContactName: student.emergencyContactName ?? "",
          emergencyContactPhone: student.emergencyContactPhone ?? "",
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
    setSaving(true);
    setErr(null);
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
    <Modal open onClose={onClose} title={student ? "Öğrenciyi düzenle" : "Misafir öğrenci ekle"}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Ad soyad">
          <Input value={form.fullName} onChange={set("fullName")} required />
        </FormField>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Telefon">
            <Input value={form.phone ?? ""} onChange={set("phone")} />
          </FormField>
          <FormField label="E-posta">
            <Input type="email" value={form.email ?? ""} onChange={set("email")} />
          </FormField>
          <FormField label="Kaynak">
            <Select value={form.source} onChange={(e) => setForm((f) => ({ ...f, source: e.target.value as StudentSource }))}>
              {Object.entries(studentSourceLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </Select>
          </FormField>
          {/* Sprint 28A: seviye artık öğrenci özelliği (opsiyonel). */}
          <FormField label="Seviye">
            <Select value={form.level ?? ""} onChange={(e) => setForm((f) => ({ ...f, level: (e.target.value || null) as StudentLevel | null }))}>
              <option value="">Belirtilmemiş</option>
              {Object.entries(studentLevelLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
            </Select>
          </FormField>
          <FormField label="Doğum tarihi">
            <Input type="date" value={form.birthDate ?? ""} onChange={set("birthDate")} />
          </FormField>
          <FormField label="Acil durum kişisi">
            <Input value={form.emergencyContactName ?? ""} onChange={set("emergencyContactName")} />
          </FormField>
          <FormField label="Acil durum telefonu">
            <Input value={form.emergencyContactPhone ?? ""} onChange={set("emergencyContactPhone")} />
          </FormField>
        </div>
        <FormField label="Not">
          <Textarea value={form.notes ?? ""} onChange={set("notes")} />
        </FormField>
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

function ConvertModal({ student, onClose, onDone }: { student: StudentDto; onClose: () => void; onDone: () => void }) {
  const [email, setEmail] = useState(student.email ?? "");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await studentService.convertToUser(student.id, { email, initialPassword: password || undefined });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Dönüştürülemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${student.fullName} → Kullanıcıya bağla`}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-sm text-muted">
          E-posta sistemde varsa o hesaba bağlanır; yoksa yeni hesap oluşturulur. Öğrenci profili ve
          geçmişi korunur.
        </p>
        <FormField label="E-posta">
          <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </FormField>
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
