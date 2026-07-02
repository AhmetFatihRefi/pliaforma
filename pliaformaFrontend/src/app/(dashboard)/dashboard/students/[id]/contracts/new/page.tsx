"use client";

// Sprint 36: "Yeni sözleşme" artık modal değil, kendi sayfası. Ekleyince öğrenci detayına (sözleşme listesine) döner.
// NOT (focus bug): Section MODÜL seviyesinde — render içinde tanımlanırsa her tuş vuruşunda remount olur, input focus kaçar.
import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { classTypeService, contractService, memberService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type ClassTypeDto,
  type DayOfWeek,
  type MemberDto,
  type PreferredSlotInput,
  type StudentContractDto,
  type StudentDto,
  daysOfWeek,
  dayLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Select, useToast } from "@/components/ui";

const today = () => new Date().toISOString().slice(0, 10);
const plus = (days: number) => new Date(Date.now() + days * 86400000).toISOString().slice(0, 10);

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold uppercase tracking-wide text-muted">{title}</p>
      {children}
    </div>
  );
}

export default function NewContractPage() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const { canManage } = useRole();
  const toast = useToast();

  const [student, setStudent] = useState<StudentDto | null>(null);
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [instructors, setInstructors] = useState<MemberDto[]>([]);
  const [headers, setHeaders] = useState<StudentContractDto[]>([]);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const [form, setForm] = useState({
    headerId: "", classTypeId: "", instructorProfileId: "", startDate: today(), endDate: plus(60),
    lessonCount: 10, pricePerLesson: 0, makeupAllowance: 2, dueDate: "", currency: "TRY",
  });
  // Kapasite tercihi (Farketmez / Bireysel / En fazla N / Diğer custom).
  const [capMode, setCapMode] = useState<"any" | "1" | "2" | "3" | "4" | "custom">("any");
  const [customCap, setCustomCap] = useState(5);
  const preferredMaxCapacity = capMode === "any" ? null : capMode === "custom" ? customCap : Number(capMode);
  const [prefSlots, setPrefSlots] = useState<PreferredSlotInput[]>([]);
  const [slotDay, setSlotDay] = useState<DayOfWeek>("Monday");
  const [slotTime, setSlotTime] = useState("");

  useEffect(() => {
    (async () => {
      try {
        const [s, ct, ms, cs] = await Promise.all([
          studentService.get(id).catch(() => null),
          classTypeService.list(true),
          memberService.list(),
          contractService.forStudent(id).catch(() => [] as StudentContractDto[]),
        ]);
        setStudent(s);
        setClassTypes(ct);
        setInstructors(ms.filter((m) => m.instructorProfileId));
        setHeaders(cs.filter((c) => c.status === "Active"));
      } catch (e) {
        setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
      }
    })();
  }, [id]);

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
  const backHref = `/dashboard/students/${id}`;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (prefSlots.length === 0) { setErr("En az bir tercih gün/saat ekleyin."); return; }
    // Sprint 45: Son ödeme tarihi zorunlu.
    if (!form.dueDate) { setErr("Son ödeme tarihi zorunludur."); return; }
    setErr(null); setBusy(true);
    try {
      await contractService.addLine(id, {
        headerId: form.headerId || null,
        classTypeId: form.classTypeId,
        instructorProfileId: form.instructorProfileId || null,
        startDate: form.startDate,
        endDate: form.endDate,
        lessonCount: Number(form.lessonCount),
        pricePerLesson: Number(form.pricePerLesson),
        makeupAllowance: Number(form.makeupAllowance),
        dueDate: form.dueDate,
        preferredMaxCapacity,
        currency: form.currency || "TRY",
        preferredSlots: prefSlots,
      });
      toast.success("Sözleşme eklendi.");
      router.push(backHref);
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Sözleşme eklenemedi.");
      setBusy(false);
    }
  };

  if (!canManage) {
    return (
      <div className="mx-auto max-w-3xl">
        <Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="flex items-center justify-between gap-3">
        <div>
          <Link href={backHref} className="text-sm text-muted hover:text-foreground">← Öğrenciye dön</Link>
          <h1 className="mt-1 text-2xl font-semibold text-foreground">Yeni sözleşme</h1>
          {student && <p className="text-sm text-muted">{student.fullName}</p>}
        </div>
      </div>

      <Card>
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
              <FormField label="Son ödeme tarihi" hint="Zorunlu. Müşteri bu tarihe kadar parça parça ödeyebilir; tarih geçince ödenmeyen kısım gecikmiş borç olur."><Input type="date" value={form.dueDate} onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))} required /></FormField>
            </div>
          </Section>

          <Section title="Haftalık ders gün/saatleri (en az 1)">
            <p className="text-xs text-muted">Haftada 2 ders için 2 farklı gün/saat ekleyin. Sistem toplam ders hakkı bitene kadar bu haftalık düzeni tekrarlar. Aynı gün farklı saat serbest; aynı gün+saat tekrar edemez.</p>
            <div className="flex flex-wrap items-end gap-2 rounded-xl border border-dashed border-border p-3">
              <Select value={slotDay} onChange={(e) => setSlotDay(e.target.value as DayOfWeek)} className="w-36">
                {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
              </Select>
              <Input type="time" value={slotTime} onChange={(e) => setSlotTime(e.target.value)} className="w-32" />
              <Button type="button" variant="secondary" size="sm" onClick={addPrefSlot}>+ Slot ekle</Button>
              <span className="text-xs text-muted">Saat boş = farketmez (üretimde kullanılmaz)</span>
            </div>
            {prefSlots.filter((s) => s.preferredStartTime).length > 0 && (
              <Badge tone="rose">Haftada {prefSlots.filter((s) => s.preferredStartTime).length} ders düzeni</Badge>
            )}
            {prefSlots.length > 0 && (
              <div className="flex flex-wrap gap-2">
                {prefSlots.map((s, i) => (
                  <span key={`${s.dayOfWeek}-${s.preferredStartTime ?? "any"}`} className="inline-flex items-center gap-1 rounded-full border border-border bg-surface px-2.5 py-1 text-xs text-foreground">
                    {slotChipLabel(s)}
                    <button type="button" className="text-muted hover:text-danger" onClick={() => setPrefSlots((xs) => xs.filter((_, j) => j !== i))}>×</button>
                  </span>
                ))}
              </div>
            )}
          </Section>

          <div className="flex justify-end gap-2 border-t border-border pt-4">
            <Button type="button" variant="secondary" onClick={() => router.push(backHref)}>Vazgeç</Button>
            <Button type="submit" loading={busy}>Sözleşme ekle</Button>
          </div>
        </form>
      </Card>
    </div>
  );
}
