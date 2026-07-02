"use client";

// Sprint 45: Ödemeler ekranı LINE-bazlı. Borç/gecikme line.dueDate + remaining/overdue'dan gelir;
// "ödeme" = fiilen ALINAN para (Paid). Taksit / vade / "ödendi işaretle" kavramı yok. Çoklu para birimi korunur (Sprint 41).
import { useCallback, useEffect, useMemo, useState } from "react";
import { paymentService, studentService, contractService } from "@/lib/studioApi";
import {
  type PaymentDto,
  type PaymentMethod,
  type StudentDto,
  type StudentContractLineDto,
  paymentMethodLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, useConfirm, useToast } from "@/components/ui";
import { errorMessage } from "@/lib/errors";
import { formatDate } from "@/lib/format";

const money = (n: number, c = "TRY") => `${n.toLocaleString("tr-TR")} ${c === "TRY" ? "₺" : c}`;
// Sprint 41: çoklu para birimi — currency başına ayrı ("4.500 ₺ + 120 €"). Tek currency'de tek değer.
const moneyMulti = (m: Map<string, number>) =>
  m.size === 0 ? money(0) : [...m.entries()].filter(([, n]) => n !== 0).map(([c, n]) => money(n, c)).join(" + ") || money(0);
const addCur = (m: Map<string, number>, cur: string, n: number) => { if (n) m.set(cur, (m.get(cur) ?? 0) + n); };

// Bir öğrencinin aktif satırları (borç/gecikme kaynağı) + alınan ödemeleri.
interface StudentGroup {
  studentProfileId: string;
  studentName: string;
  lines: StudentContractLineDto[];
  payments: PaymentDto[];          // alınan (Paid) ödemeler
  remainingByCur: Map<string, number>;
  overdueByCur: Map<string, number>;
  hasDebt: boolean;
  hasOverdue: boolean;
}

export default function PaymentsPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [onlyDebtors, setOnlyDebtors] = useState(true);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [ls, ps] = await Promise.all([contractService.allLines(), paymentService.list()]);
      setLines(ls); setPayments(ps); setError(null);
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
    const ensure = (id: string, name: string) => {
      let g = map.get(id);
      if (!g) {
        g = { studentProfileId: id, studentName: name, lines: [], payments: [], remainingByCur: new Map(), overdueByCur: new Map(), hasDebt: false, hasOverdue: false };
        map.set(id, g);
      }
      return g;
    };
    for (const l of lines) {
      const g = ensure(l.studentProfileId, l.studentName);
      g.lines.push(l);
      addCur(g.remainingByCur, l.currency, l.remaining);
      addCur(g.overdueByCur, l.currency, l.overdueAmount);
      if (l.remaining > 0) g.hasDebt = true;
      if (l.isOverdue) g.hasOverdue = true;
    }
    for (const p of payments) {
      if (p.status === "Cancelled") continue;
      ensure(p.studentProfileId, p.studentName).payments.push(p);
    }
    let arr = [...map.values()];
    if (onlyDebtors) arr = arr.filter((g) => g.hasDebt);
    // Önce gecikeni olanlar, sonra borcu olanlar, sonra ada göre.
    const sumMap = (m: Map<string, number>) => [...m.values()].reduce((s, n) => s + n, 0);
    arr.sort((a, b) =>
      (sumMap(b.overdueByCur) - sumMap(a.overdueByCur)) || (sumMap(b.remainingByCur) - sumMap(a.remainingByCur)) || a.studentName.localeCompare(b.studentName, "tr"));
    return arr;
  }, [lines, payments, onlyDebtors]);

  // Stüdyo özeti (üst şerit). Sprint 41: currency'ye göre gruplanır (yanlış tek-currency toplamı yok).
  const totals = useMemo(() => {
    const remaining = new Map<string, number>();
    const overdue = new Map<string, number>();
    let debtorCount = 0, overdueCount = 0;
    for (const l of lines) {
      addCur(remaining, l.currency, l.remaining);
      addCur(overdue, l.currency, l.overdueAmount);
    }
    // Borçlu / gecikmeli öğrenci sayısı (tüm aktif satırlardan, filtreden bağımsız).
    const byStudent = new Map<string, { debt: boolean; overdue: boolean }>();
    for (const l of lines) {
      const s = byStudent.get(l.studentProfileId) ?? { debt: false, overdue: false };
      if (l.remaining > 0) s.debt = true;
      if (l.isOverdue) s.overdue = true;
      byStudent.set(l.studentProfileId, s);
    }
    for (const s of byStudent.values()) { if (s.debt) debtorCount++; if (s.overdue) overdueCount++; }
    return { debtorCount, overdueCount, remaining, overdue };
  }, [lines]);

  const cancelPayment = async (p: PaymentDto) => {
    if (!(await confirm({ title: "Ödemeyi iptal et", tone: "danger", confirmLabel: "İptal et", message: `${money(p.amount, p.currency)} tutarındaki alınan ödeme kaydı iptal edilecek.` }))) return;
    try { await paymentService.cancel(p.id); await load(); toast.success("Ödeme iptal edildi."); }
    catch (e) { toast.error(errorMessage(e, "İşlem başarısız.")); }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ödemeler</h1>
          <p className="mt-1 text-sm text-muted">Öğrenci bazlı borç takibi (manuel; online ödeme yok). Kalan ödeme ve gecikmiş borç sözleşmenin son ödeme tarihinden hesaplanır. Tıklayınca paketler ve alınan ödemeler açılır.</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>+ Ödeme ekle</Button>
      </div>

      {/* Stüdyo özeti */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <SummaryStat label="Borçlu öğrenci" value={String(totals.debtorCount)} />
        <SummaryStat label="Güncel kalan ödeme" value={moneyMulti(totals.remaining)} />
        <SummaryStat label="Vadesi geçen öğrenci" value={String(totals.overdueCount)} tone={totals.overdueCount ? "danger" : undefined} />
        <SummaryStat label="Güncel gecikmiş borç" value={moneyMulti(totals.overdue)} tone={totals.overdueCount ? "danger" : undefined} />
      </div>

      <Card>
        <label className="flex cursor-pointer items-center gap-2 text-sm text-foreground">
          <input type="checkbox" checked={onlyDebtors} onChange={(e) => setOnlyDebtors(e.target.checked)} className="accent-[var(--primary)]" />
          Yalnız kalan ödemesi olan öğrenciler
        </label>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-center text-sm text-muted">Yükleniyor…</p>
      ) : groups.length === 0 ? (
        <p className="text-center text-sm text-muted">{onlyDebtors ? "Kalan ödemesi olan öğrenci yok." : "Kayıt yok."}</p>
      ) : (
        <div className="space-y-2">
          {groups.map((g) => (
            <StudentDebtCard
              key={g.studentProfileId}
              group={g}
              open={expanded.has(g.studentProfileId)}
              onToggle={() => toggle(g.studentProfileId)}
              onCancel={cancelPayment}
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
  group: g, open, onToggle, onCancel,
}: {
  group: StudentGroup; open: boolean; onToggle: () => void;
  onCancel: (p: PaymentDto) => void;
}) {
  const activePayments = useMemo(() => g.payments.filter((p) => p.status !== "Cancelled").slice()
    .sort((a, b) => (b.paidAt ?? b.dueDate).localeCompare(a.paidAt ?? a.dueDate)), [g.payments]);

  return (
    <Card className="overflow-hidden p-0">
      <button type="button" onClick={onToggle} className="flex w-full items-center justify-between gap-3 px-4 py-3 text-left transition hover:bg-surface-muted">
        <div className="flex min-w-0 items-center gap-2">
          <span className={`text-muted transition-transform ${open ? "rotate-90" : ""}`}>▸</span>
          <span className="truncate font-medium text-foreground">{g.studentName}</span>
        </div>
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-1.5">
          {g.hasDebt ? (
            <Badge tone="warning">Kalan {moneyMulti(g.remainingByCur)}</Badge>
          ) : (
            <Badge tone="success">Borç yok</Badge>
          )}
          {g.hasOverdue && <Badge tone="danger">Gecikmiş {moneyMulti(g.overdueByCur)}</Badge>}
        </div>
      </button>

      {open && (
        <div className="space-y-3 border-t border-border px-4 py-3">
          {/* Paket (line) bazında borç durumu */}
          {g.lines.length === 0 ? (
            <p className="text-xs text-muted">Aktif paket yok.</p>
          ) : g.lines.slice().sort((a, b) => Number(b.isOverdue) - Number(a.isOverdue) || b.remaining - a.remaining).map((l) => (
            <div key={l.id} className="rounded-xl border border-border px-3 py-2 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="truncate font-medium text-foreground">{l.classTypeName} · {formatDate(l.startDate)} – {formatDate(l.endDate)}</span>
                <span className="shrink-0">
                  <span className={l.remaining > 0 ? "font-semibold text-danger" : "text-success"}>kalan {money(l.remaining, l.currency)}</span>
                  {l.overpaidAmount > 0 && <span className="ml-1 text-success">· fazla {money(l.overpaidAmount, l.currency)}</span>}
                </span>
              </div>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted">
                <span>toplam {money(l.totalPrice, l.currency)} · ödenen {money(l.paid, l.currency)}</span>
                {!l.dueDate ? (
                  <span>· son ödeme tarihi belirlenmemiş</span>
                ) : l.isOverdue ? (
                  <span className="font-medium text-danger">· vadesi geçti {formatDate(l.dueDate)} ({l.daysOverdue} gün) · gecikmiş {money(l.overdueAmount, l.currency)}</span>
                ) : (
                  <span>· son ödeme tarihi {formatDate(l.dueDate)}</span>
                )}
              </div>
            </div>
          ))}

          {/* Alınan ödemeler */}
          <div>
            <p className="mb-1 text-xs font-semibold text-muted">Alınan ödemeler</p>
            {activePayments.length === 0 ? (
              <p className="text-xs text-muted">Henüz alınan ödeme yok.</p>
            ) : (
              <ul className="divide-y divide-border rounded-xl border border-border">
                {activePayments.map((p) => (
                  <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                    <div className="flex items-center gap-2">
                      <Badge tone="success">Ödendi</Badge>
                      <span className="text-muted">{p.paidAt ? `alındı ${formatDate(p.paidAt)}` : formatDate(p.dueDate)}</span>
                      {p.lineLabel && <span className="text-muted">· {p.lineLabel}</span>}
                      {p.method && <span className="text-muted">· {paymentMethodLabels[p.method]}</span>}
                      {p.notes && <span className="text-muted">· {p.notes}</span>}
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="font-medium text-foreground">{money(p.amount, p.currency)}</span>
                      <Button variant="danger" size="sm" onClick={() => onCancel(p)}>İptal et</Button>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
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
  const [form, setForm] = useState({ lineId: "", amount: "", method: "" as "" | PaymentMethod, notes: "" });
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
    `${l.classTypeName} · ${formatDate(l.startDate)} – ${formatDate(l.endDate)} · kalan ödeme ${money(l.remaining, l.currency)}`;

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
    <Modal open onClose={onClose} title="Alınan ödeme kaydı">
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
          <FormField label="Alınan tutar"><Input type="number" min={0} step="0.01" value={form.amount} onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))} required /></FormField>
          <FormField label="Yöntem (ops.)">
            <Select placeholder="Yöntem seç" value={form.method} onChange={(e) => setForm((f) => ({ ...f, method: e.target.value as PaymentMethod | "" }))}>
              <option value="">—</option>
              {(["Cash", "Card", "BankTransfer", "Other"] as PaymentMethod[]).map((m) => <option key={m} value={m}>{paymentMethodLabels[m]}</option>)}
            </Select>
          </FormField>
        </div>
        <FormField label="Not (ops.)"><Input value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} /></FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy} disabled={!student || !form.lineId}>Ödemeyi kaydet</Button>
        </div>
      </form>
    </Modal>
  );
}
