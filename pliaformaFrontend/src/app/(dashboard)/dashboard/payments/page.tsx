"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { paymentService, studentService, contractService } from "@/lib/studioApi";
import {
  type PaymentDto,
  type PaymentMethod,
  type PaymentStatus,
  type StudentDto,
  type StudentContractLineDto,
  paymentMethodLabels,
  paymentStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, useConfirm, useToast } from "@/components/ui";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

const money = (n: number, c = "TRY") => `${n.toLocaleString("tr-TR")} ${c === "TRY" ? "₺" : c}`;
// Sprint 41: çoklu para birimi — currency başına toplamları "4.500 ₺ + 120 €" gibi ayrı gösterir.
// Tek currency'de tek değer döner (görünüm bozulmaz).
const moneyMulti = (m: Map<string, number>) =>
  m.size === 0 ? money(0) : [...m.entries()].map(([c, n]) => money(n, c)).join(" + ");
const statusTone = (s: PaymentStatus) =>
  s === "Paid" ? "success" : s === "Overdue" ? "danger" : s === "Cancelled" || s === "Refunded" ? "neutral" : "warning";
const isOpen = (p: PaymentDto) => p.status === "Pending" || p.status === "Overdue";

// Bir öğrencinin tüm ödemeleri + türetilmiş özet (borç hesaplamasını stüdyo kendisi yapsın diye).
interface StudentGroup {
  studentProfileId: string;
  studentName: string;
  payments: PaymentDto[];
  open: PaymentDto[];      // ödenmemiş (Pending + Overdue)
  overdue: PaymentDto[];   // vadesi geçmiş
  outstanding: number;     // ödenmemiş toplam
  overdueAmount: number;   // geciken toplam
  currency: string;
}

export default function PaymentsPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const [items, setItems] = useState<PaymentDto[]>([]);
  const [onlyDebtors, setOnlyDebtors] = useState(true);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await paymentService.list());
      setError(null);
    } catch (e) {
      setError(errorMessage(e, "Ödemeler yüklenemedi."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const toggle = (id: string) =>
    setExpanded((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });

  const groups = useMemo<StudentGroup[]>(() => {
    const map = new Map<string, StudentGroup>();
    for (const p of items) {
      let g = map.get(p.studentProfileId);
      if (!g) {
        g = { studentProfileId: p.studentProfileId, studentName: p.studentName, payments: [], open: [], overdue: [], outstanding: 0, overdueAmount: 0, currency: p.currency };
        map.set(p.studentProfileId, g);
      }
      g.payments.push(p);
      if (isOpen(p)) { g.open.push(p); g.outstanding += p.amount; g.currency = p.currency; }
      if (p.status === "Overdue") { g.overdue.push(p); g.overdueAmount += p.amount; }
    }
    let arr = [...map.values()];
    if (onlyDebtors) arr = arr.filter((g) => g.open.length > 0);
    // Önce gecikeni olanlar (tutarına göre), sonra borcu olanlar, sonra ada göre.
    arr.sort((a, b) =>
      (b.overdueAmount - a.overdueAmount) || (b.outstanding - a.outstanding) || a.studentName.localeCompare(b.studentName, "tr"));
    return arr;
  }, [items, onlyDebtors]);

  // Tüm stüdyo özeti (üst şerit). Sprint 41: toplamlar para birimine göre gruplanır (yanlış tek-currency toplamı yok).
  const totals = useMemo(() => {
    const debtors = groups.filter((g) => g.open.length > 0);
    const outstanding = new Map<string, number>();
    const overdue = new Map<string, number>();
    let overdueCount = 0;
    for (const g of groups) {
      for (const p of g.open) outstanding.set(p.currency, (outstanding.get(p.currency) ?? 0) + p.amount);
      for (const p of g.overdue) { overdue.set(p.currency, (overdue.get(p.currency) ?? 0) + p.amount); overdueCount++; }
    }
    return { debtorCount: debtors.length, overdueCount, outstanding, overdue };
  }, [groups]);

  const markPaid = async (p: PaymentDto) => {
    if (!(await confirm({
      title: "Ödemeyi 'ödendi' işaretle",
      confirmLabel: "Ödendi",
      message: `${money(p.amount, p.currency)} tutarındaki taksit 'ödendi' olarak işaretlenecek (vade ${formatDate(p.dueDate)}).`,
    }))) return;
    try { await paymentService.markPaid(p.id); await load(); toast.success("Ödeme 'ödendi' olarak işaretlendi."); }
    catch (e) { toast.error(errorMessage(e, "İşlem başarısız.")); }
  };
  const cancel = async (p: PaymentDto) => {
    if (!(await confirm({ title: "Ödemeyi iptal et", tone: "danger", confirmLabel: "İptal et", message: "Bu ödeme kaydı iptal edilecek." }))) return;
    try { await paymentService.cancel(p.id); await load(); toast.success("Ödeme iptal edildi."); }
    catch (e) { toast.error(errorMessage(e, "İşlem başarısız.")); }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ödemeler</h1>
          <p className="mt-1 text-sm text-muted">Öğrenci bazlı borç takibi (manuel; online ödeme yok). Tıklayınca taksitler ve hangi sözleşmeye ait olduğu açılır.</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>+ Yeni ödeme</Button>
      </div>

      {/* Stüdyo özeti */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <SummaryStat label="Borçlu öğrenci" value={String(totals.debtorCount)} />
        <SummaryStat label="Ödenmemiş toplam" value={moneyMulti(totals.outstanding)} />
        <SummaryStat label="Geciken taksit" value={String(totals.overdueCount)} tone={totals.overdueCount ? "danger" : undefined} />
        <SummaryStat label="Geciken toplam" value={moneyMulti(totals.overdue)} tone={totals.overdueCount ? "danger" : undefined} />
      </div>

      <Card>
        <label className="flex cursor-pointer items-center gap-2 text-sm text-foreground">
          <input type="checkbox" checked={onlyDebtors} onChange={(e) => setOnlyDebtors(e.target.checked)} className="accent-[var(--primary)]" />
          Yalnız borcu olan öğrenciler
        </label>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-center text-sm text-muted">Yükleniyor…</p>
      ) : groups.length === 0 ? (
        <p className="text-center text-sm text-muted">{onlyDebtors ? "Borcu olan öğrenci yok." : "Ödeme kaydı yok."}</p>
      ) : (
        <div className="space-y-2">
          {groups.map((g) => (
            <StudentDebtCard
              key={g.studentProfileId}
              group={g}
              open={expanded.has(g.studentProfileId)}
              onToggle={() => toggle(g.studentProfileId)}
              onPaid={markPaid}
              onCancel={cancel}
            />
          ))}
        </div>
      )}

      {createOpen && <CreatePaymentModal onClose={() => setCreateOpen(false)} onDone={() => { setCreateOpen(false); load(); }} />}
    </div>
  );
}

function SummaryStat({ label, value, tone }: { label: string; value: string; tone?: "danger" }) {
  return (
    <div className={`rounded-xl border px-3 py-2 ${tone === "danger" ? "border-danger/40 bg-danger/5" : "border-border bg-surface"}`}>
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-0.5 text-lg font-semibold ${tone === "danger" ? "text-danger" : "text-foreground"}`}>{value}</p>
    </div>
  );
}

function StudentDebtCard({
  group: g, open, onToggle, onPaid, onCancel,
}: {
  group: StudentGroup; open: boolean; onToggle: () => void;
  onPaid: (p: PaymentDto) => void; onCancel: (p: PaymentDto) => void;
}) {
  // Sprint 41: rozet toplamları para birimine göre (öğrencide karışık currency olabilir).
  const openByCur = useMemo(() => {
    const m = new Map<string, number>();
    for (const p of g.open) m.set(p.currency, (m.get(p.currency) ?? 0) + p.amount);
    return m;
  }, [g.open]);
  const overdueByCur = useMemo(() => {
    const m = new Map<string, number>();
    for (const p of g.overdue) m.set(p.currency, (m.get(p.currency) ?? 0) + p.amount);
    return m;
  }, [g.overdue]);

  // Detay: sözleşme/satır bazında grupla (hangi sözleşmeye ait olduğu görünsün; önceki sözleşmeler de ayrı satır).
  const byLine = useMemo(() => {
    const m = new Map<string, { label: string; payments: PaymentDto[] }>();
    for (const p of g.payments) {
      const key = p.studentContractLineId ?? "none";
      const label = p.lineLabel ?? "Diğer (sözleşmesiz / eski kayıt)";
      const e = m.get(key) ?? { label, payments: [] };
      e.payments.push(p);
      m.set(key, e);
    }
    // Açık (ödenmemiş) borcu olan sözleşmeler üstte; her birinde taksitler vadeye göre.
    return [...m.values()]
      .map((x) => ({ ...x, payments: x.payments.slice().sort((a, b) => a.dueDate.localeCompare(b.dueDate)) }))
      .sort((a, b) => Number(b.payments.some(isOpen)) - Number(a.payments.some(isOpen)) || a.label.localeCompare(b.label, "tr"));
  }, [g.payments]);

  return (
    <Card className="overflow-hidden p-0">
      <button type="button" onClick={onToggle} className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-surface-muted">
        <div className="flex min-w-0 items-center gap-2">
          <span className={`text-muted transition-transform ${open ? "rotate-90" : ""}`}>▸</span>
          <span className="truncate font-medium text-foreground">{g.studentName}</span>
        </div>
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-1.5">
          {g.open.length > 0 ? (
            <Badge tone="warning">{g.open.length} taksit · {moneyMulti(openByCur)}</Badge>
          ) : (
            <Badge tone="success">Borç yok</Badge>
          )}
          {g.overdue.length > 0 && <Badge tone="danger">Geciken {g.overdue.length} · {moneyMulti(overdueByCur)}</Badge>}
        </div>
      </button>

      {open && (
        <div className="space-y-3 border-t border-border px-4 py-3">
          {byLine.map((line, i) => {
            const lineOpen = line.payments.filter(isOpen);
            const lineSum = lineOpen.reduce((s, p) => s + p.amount, 0);
            return (
              <div key={i} className="rounded-xl border border-border">
                <div className="flex items-center justify-between gap-2 border-b border-border bg-surface-muted px-3 py-1.5">
                  <span className="truncate text-xs font-medium text-foreground">{line.label}</span>
                  {lineOpen.length > 0 && <span className="shrink-0 text-xs font-semibold text-warning">{lineOpen.length} taksit · {money(lineSum, lineOpen[0]?.currency ?? g.currency)}</span>}
                </div>
                <ul className="divide-y divide-border">
                  {line.payments.map((p) => (
                    <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                      <div className="flex items-center gap-2">
                        <Badge tone={statusTone(p.status)}>{paymentStatusLabels[p.status]}</Badge>
                        <span className="text-muted">Vade {formatDate(p.dueDate)}</span>
                        {p.method && <span className="text-muted">· {paymentMethodLabels[p.method]}</span>}
                        {p.notes && <span className="text-muted">· {p.notes}</span>}
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="font-medium text-foreground">{money(p.amount, p.currency)}</span>
                        {isOpen(p) && (
                          <div className="flex gap-1.5">
                            <Button variant="ghost" size="sm" onClick={() => onPaid(p)}>Ödendi</Button>
                            <Button variant="danger" size="sm" onClick={() => onCancel(p)}>İptal et</Button>
                          </div>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      )}
    </Card>
  );
}

function CreatePaymentModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<StudentDto[]>([]);
  const [student, setStudent] = useState<StudentDto | null>(null);
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [linesLoading, setLinesLoading] = useState(false);
  const [form, setForm] = useState({ lineId: "", amount: "", dueDate: new Date().toISOString().slice(0, 10), method: "" as "" | PaymentMethod, notes: "" });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const t = setTimeout(async () => {
      try { setResults(search.trim() ? await studentService.search(search.trim()) : await studentService.list()); }
      catch { setResults([]); }
    }, 300);
    return () => clearTimeout(t);
  }, [search]);

  // Öğrenci seçilince sözleşme satırlarını getir (ödeme 29A'dan beri bir satıra bağlanmalı).
  useEffect(() => {
    if (!student) { setLines([]); return; }
    setLinesLoading(true);
    contractService.lines(student.id)
      .then((ls) => setLines(ls.filter((l) => l.status !== "Cancelled")))
      .catch(() => setLines([]))
      .finally(() => setLinesLoading(false));
  }, [student]);

  const lineLabel = (l: StudentContractLineDto) =>
    `${l.classTypeName} · ${formatDate(l.startDate)} – ${formatDate(l.endDate)} · kalan borç ${money(l.remaining, l.currency)}`;

  const pickLine = (id: string) => {
    const l = lines.find((x) => x.id === id);
    setForm((f) => ({ ...f, lineId: id, amount: l && l.remaining > 0 ? String(l.remaining) : f.amount }));
  };

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!student) { setErr("Öğrenci seçin."); return; }
    if (!form.lineId) { setErr("Ödemenin bağlanacağı sözleşme satırını seçin."); return; }
    setBusy(true);
    setErr(null);
    try {
      await paymentService.create(student.id, {
        studentContractLineId: form.lineId,
        amount: Number(form.amount),
        dueDate: form.dueDate,
        method: form.method || undefined,
        notes: form.notes || undefined,
      });
      onDone();
    } catch (e2) {
      setErr(errorMessage(e2, "Oluşturulamadı."));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Yeni ödeme kaydı">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        {student ? (
          <div className="flex items-center justify-between rounded-xl border border-border px-3 py-2 text-sm">
            <span className="font-medium text-foreground">{student.fullName}</span>
            <Button type="button" variant="ghost" size="sm" onClick={() => { setStudent(null); setForm((f) => ({ ...f, lineId: "" })); }}>Değiştir</Button>
          </div>
        ) : (
          <>
            <Input placeholder="Öğrenci ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
            <div className="max-h-40 space-y-1 overflow-y-auto">
              {results.map((s) => (
                <button key={s.id} type="button" onClick={() => setStudent(s)} className="flex w-full justify-between rounded-lg px-3 py-2 text-left text-sm text-foreground hover:bg-surface-muted">
                  {s.fullName}
                </button>
              ))}
            </div>
          </>
        )}

        {student && (
          <FormField label="Sözleşme satırı (paket)">
            {linesLoading ? (
              <p className="text-sm text-muted">Yükleniyor…</p>
            ) : lines.length === 0 ? (
              <Alert kind="info">Bu öğrencinin aktif sözleşme satırı yok. Önce Öğrenciler ekranından sözleşme satırı ekleyin.</Alert>
            ) : (
              <Select placeholder="Paket seç" value={form.lineId} onChange={(e) => pickLine(e.target.value)} required>
                <option value="">— Satır seçin —</option>
                {lines.map((l) => <option key={l.id} value={l.id}>{lineLabel(l)}</option>)}
              </Select>
            )}
          </FormField>
        )}

        <div className="grid grid-cols-2 gap-3">
          <FormField label="Tutar"><Input type="number" min={0} step="0.01" value={form.amount} onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))} required /></FormField>
          <FormField label="Vade"><Input type="date" value={form.dueDate} onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))} required /></FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Yöntem (ops.)">
            <Select placeholder="Yöntem seç" value={form.method} onChange={(e) => setForm((f) => ({ ...f, method: e.target.value as PaymentMethod | "" }))}>
              <option value="">—</option>
              {(["Cash", "Card", "BankTransfer", "Other"] as PaymentMethod[]).map((m) => <option key={m} value={m}>{paymentMethodLabels[m]}</option>)}
            </Select>
          </FormField>
          <FormField label="Not (ops.)"><Input value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} /></FormField>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy} disabled={!student || !form.lineId}>Oluştur</Button>
        </div>
      </form>
    </Modal>
  );
}
