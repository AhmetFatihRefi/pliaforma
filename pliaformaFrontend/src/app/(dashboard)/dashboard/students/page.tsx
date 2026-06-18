"use client";

import { useCallback, useEffect, useState } from "react";
import { attendanceService, classTypeService, contractService, memberService, packagePlanService, paymentService, studentPackageService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type AttendanceSummaryDto,
  type ClassTypeDto,
  type ContractScheduleSlotInput,
  type DayOfWeek,
  type GuestInput,
  type MemberDto,
  type PackagePlanDto,
  type PaymentDto,
  type StudentContractDto,
  type StudentDto,
  type StudentPackageDto,
  type StudentSource,
  daysOfWeek,
  dayLabels,
  packageStatusLabels,
  paymentStatusLabels,
  studentContractStatusLabels,
  studentSourceLabels,
  studentStatusLabels,
} from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
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
  fullName: "", phone: "", email: "", source: "Phone", notes: "", healthNotes: "",
  emergencyContactName: "", emergencyContactPhone: "", birthDate: "",
};

const statusTone = (s: StudentDto["status"]) => (s === "Active" ? "success" : s === "Guest" ? "warning" : "neutral");

export default function StudentsPage() {
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
                    <Button variant="ghost" size="sm" onClick={() => setFinanceFor(s)}>Paket/Ödeme</Button>
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
function ContractsModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [contracts, setContracts] = useState<StudentContractDto[]>([]);
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [instructors, setInstructors] = useState<MemberDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const today = new Date().toISOString().slice(0, 10);
  const plus = (days: number) => new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);
  const [form, setForm] = useState({
    classTypeId: "", instructorProfileId: "", startDate: today, endDate: plus(60),
    totalLessonCount: 16, makeupAllowance: 2, price: 0, isIndividualDefault: false,
  });

  const load = useCallback(async () => {
    try {
      const [cs, ct, ms] = await Promise.all([
        contractService.forStudent(student.id),
        classTypeService.list(true),
        memberService.list(),
      ]);
      setContracts(cs);
      setClassTypes(ct);
      setInstructors(ms.filter((m) => m.role === "Instructor" && m.instructorProfileId));
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    }
  }, [student.id]);

  useEffect(() => { load(); }, [load]);

  const create = async (e: React.FormEvent) => {
    e.preventDefault();
    setErr(null); setBusy(true);
    try {
      await contractService.create(student.id, {
        classTypeId: form.classTypeId,
        instructorProfileId: form.instructorProfileId,
        startDate: form.startDate,
        endDate: form.endDate,
        totalLessonCount: Number(form.totalLessonCount),
        makeupAllowance: Number(form.makeupAllowance),
        price: Number(form.price),
        isIndividualDefault: form.isIndividualDefault,
      });
      await load();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Sözleşme oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  };

  const cancel = async (id: string) => {
    if (!confirm("Sözleşme iptal edilsin mi?")) return;
    try { await contractService.cancel(id); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İptal edilemedi."); }
  };

  return (
    <Modal open onClose={onClose} title={`Sözleşmeler — ${student.fullName}`}>
      <div className="space-y-5">
        {err && <Alert kind="error">{err}</Alert>}

        <form onSubmit={create} className="grid grid-cols-2 gap-3 rounded-xl border border-border bg-surface p-3">
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
          <FormField label="Ders hakkı"><Input type="number" min={1} value={form.totalLessonCount} onChange={(e) => setForm((f) => ({ ...f, totalLessonCount: Number(e.target.value) }))} /></FormField>
          <FormField label="Telafi hakkı"><Input type="number" min={0} value={form.makeupAllowance} onChange={(e) => setForm((f) => ({ ...f, makeupAllowance: Number(e.target.value) }))} /></FormField>
          <FormField label="Ücret"><Input type="number" min={0} value={form.price} onChange={(e) => setForm((f) => ({ ...f, price: Number(e.target.value) }))} /></FormField>
          <FormField label="Bireysel mi?">
            <label className="flex h-9 items-center gap-2 text-sm text-muted">
              <input type="checkbox" checked={form.isIndividualDefault} onChange={(e) => setForm((f) => ({ ...f, isIndividualDefault: e.target.checked }))} />
              Bireysel dersler
            </label>
          </FormField>
          <div className="col-span-2 flex justify-end">
            <Button type="submit" loading={busy}>Sözleşme oluştur</Button>
          </div>
        </form>

        <div className="space-y-3">
          {contracts.length === 0 ? (
            <p className="text-sm text-muted">Henüz sözleşme yok.</p>
          ) : (
            contracts.map((c) => <ContractRow key={c.id} contract={c} onChanged={load} onCancel={() => cancel(c.id)} />)
          )}
        </div>
      </div>
    </Modal>
  );
}

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

function StudentFinanceModal({ student, onClose }: { student: StudentDto; onClose: () => void }) {
  const [packages, setPackages] = useState<StudentPackageDto[]>([]);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [plans, setPlans] = useState<PackagePlanDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [sellPlan, setSellPlan] = useState("");
  const [payAmount, setPayAmount] = useState("");
  const [payDue, setPayDue] = useState(new Date().toISOString().slice(0, 10));
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const [pk, pm] = await Promise.all([
        studentPackageService.forStudent(student.id),
        paymentService.forStudent(student.id),
      ]);
      setPackages(pk);
      setPayments(pm);
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    }
  }, [student.id]);

  useEffect(() => {
    load();
    packagePlanService.list(true).then(setPlans).catch(() => setPlans([]));
  }, [load]);

  const wrap = async (fn: () => Promise<unknown>) => {
    setBusy(true); setErr(null);
    try { await fn(); await load(); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "İşlem başarısız."); }
    finally { setBusy(false); }
  };

  const sell = () => {
    if (!sellPlan) { setErr("Paket planı seçin."); return; }
    wrap(() => studentPackageService.sell(student.id, { packagePlanId: sellPlan })).then(() => setSellPlan(""));
  };
  const adjust = (p: StudentPackageDto) => {
    const v = prompt(`"${p.name}" için düzeltme (+/-):`, "1");
    if (!v) return;
    const n = Number(v);
    if (!n) return;
    wrap(() => studentPackageService.adjust(p.id, { amount: n, notes: "manuel" }));
  };
  const createPayment = () => {
    if (!payAmount) { setErr("Tutar girin."); return; }
    wrap(() => paymentService.create(student.id, { amount: Number(payAmount), dueDate: payDue })).then(() => setPayAmount(""));
  };

  return (
    <Modal open onClose={onClose} title={`${student.fullName} — Paket / Ödeme`}>
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}

        <div>
          <h3 className="mb-2 text-sm font-semibold text-foreground">Paketler</h3>
          <div className="space-y-2">
            {packages.length === 0 ? <p className="text-sm text-muted">Paket yok.</p> : packages.map((p) => (
              <div key={p.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
                <span>{p.name} · <span className="text-muted">{p.currentCreditBalance}/{p.initialCreditCount}</span></span>
                <div className="flex items-center gap-2">
                  <Badge tone={p.effectiveStatus === "Active" ? "success" : p.effectiveStatus === "Expired" || p.effectiveStatus === "Cancelled" ? "danger" : "neutral"}>{packageStatusLabels[p.effectiveStatus]}</Badge>
                  <Button variant="ghost" size="sm" disabled={busy} onClick={() => adjust(p)}>Düzelt</Button>
                  {p.status === "Active" && <Button variant="ghost" size="sm" disabled={busy} onClick={() => wrap(() => studentPackageService.cancel(p.id))}>İptal</Button>}
                </div>
              </div>
            ))}
          </div>
          <div className="mt-2 flex gap-2">
            <Select value={sellPlan} onChange={(e) => setSellPlan(e.target.value)}>
              <option value="">Paket planı seç…</option>
              {plans.map((pl) => <option key={pl.id} value={pl.id}>{pl.name} · {pl.creditCount} ders · {pl.price}₺</option>)}
            </Select>
            <Button size="sm" disabled={busy} onClick={sell}>Paket tanımla</Button>
          </div>
        </div>

        <div className="border-t border-border pt-3">
          <h3 className="mb-2 text-sm font-semibold text-foreground">Ödemeler</h3>
          <div className="space-y-2">
            {payments.length === 0 ? <p className="text-sm text-muted">Ödeme yok.</p> : payments.map((p) => (
              <div key={p.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
                <span>{p.amount.toLocaleString("tr-TR")} {p.currency} · <span className="text-muted">vade {p.dueDate}</span></span>
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
          <div className="mt-2 flex gap-2">
            <Input type="number" min={0} step="0.01" placeholder="Tutar" value={payAmount} onChange={(e) => setPayAmount(e.target.value)} />
            <Input type="date" value={payDue} onChange={(e) => setPayDue(e.target.value)} />
            <Button size="sm" disabled={busy} onClick={createPayment}>Ödeme ekle</Button>
          </div>
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
