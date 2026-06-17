"use client";

import { useCallback, useEffect, useState } from "react";
import { paymentService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type PaymentDto,
  type PaymentMethod,
  type PaymentStatus,
  type StudentDto,
  paymentMethodLabels,
  paymentStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";

const tone = (s: PaymentStatus) =>
  s === "Paid" ? "success" : s === "Overdue" ? "danger" : s === "Cancelled" || s === "Refunded" ? "neutral" : "warning";

export default function PaymentsPage() {
  const [items, setItems] = useState<PaymentDto[]>([]);
  const [status, setStatus] = useState<"" | PaymentStatus>("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await paymentService.list({ status: status || undefined }));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Ödemeler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => {
    load();
  }, [load]);

  const markPaid = async (p: PaymentDto) => {
    try { await paymentService.markPaid(p.id); await load(); }
    catch (e) { setError(e instanceof ApiError ? e.message : "İşlem başarısız."); }
  };
  const cancel = async (p: PaymentDto) => {
    if (!confirm("Ödeme iptal edilsin mi?")) return;
    try { await paymentService.cancel(p.id); await load(); }
    catch (e) { setError(e instanceof ApiError ? e.message : "İşlem başarısız."); }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ödemeler</h1>
          <p className="mt-1 text-sm text-muted">Manuel ödeme takibi (online ödeme yok).</p>
        </div>
        <Button onClick={() => setCreateOpen(true)}>+ Yeni ödeme</Button>
      </div>

      <Card>
        <FormField label="Durum filtresi">
          <Select value={status} onChange={(e) => setStatus(e.target.value as PaymentStatus | "")}>
            <option value="">Tümü</option>
            {(["Pending", "Paid", "Overdue", "Cancelled", "Refunded"] as PaymentStatus[]).map((s) => (
              <option key={s} value={s}>{paymentStatusLabels[s]}</option>
            ))}
          </Select>
        </FormField>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead><TR><TH>Öğrenci</TH><TH>Tutar</TH><TH>Vade</TH><TH>Durum</TH><TH>Yöntem</TH><TH className="text-right">İşlem</TH></TR></THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={6}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={6}>Ödeme kaydı yok.</TableEmpty>
          ) : (
            items.map((p) => (
              <TR key={p.id}>
                <TD className="font-medium">{p.studentName}</TD>
                <TD>{p.amount.toLocaleString("tr-TR")} {p.currency}</TD>
                <TD className="text-muted">{p.dueDate}</TD>
                <TD><Badge tone={tone(p.status)}>{paymentStatusLabels[p.status]}</Badge></TD>
                <TD className="text-muted">{p.method ? paymentMethodLabels[p.method] : "—"}</TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    {(p.status === "Pending" || p.status === "Overdue") && (
                      <>
                        <Button variant="ghost" size="sm" onClick={() => markPaid(p)}>Ödendi</Button>
                        <Button variant="ghost" size="sm" onClick={() => cancel(p)}>İptal</Button>
                      </>
                    )}
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      {createOpen && <CreatePaymentModal onClose={() => setCreateOpen(false)} onDone={() => { setCreateOpen(false); load(); }} />}
    </div>
  );
}

function CreatePaymentModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<StudentDto[]>([]);
  const [student, setStudent] = useState<StudentDto | null>(null);
  const [form, setForm] = useState({ amount: "", dueDate: new Date().toISOString().slice(0, 10), method: "" as "" | PaymentMethod, notes: "" });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const t = setTimeout(async () => {
      try { setResults(search.trim() ? await studentService.search(search.trim()) : await studentService.list()); }
      catch { setResults([]); }
    }, 300);
    return () => clearTimeout(t);
  }, [search]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!student) { setErr("Öğrenci seçin."); return; }
    setBusy(true);
    setErr(null);
    try {
      await paymentService.create(student.id, {
        amount: Number(form.amount),
        dueDate: form.dueDate,
        method: form.method || undefined,
        notes: form.notes || undefined,
      });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Oluşturulamadı.");
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
            <Button type="button" variant="ghost" size="sm" onClick={() => setStudent(null)}>Değiştir</Button>
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
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Tutar"><Input type="number" min={0} step="0.01" value={form.amount} onChange={(e) => setForm((f) => ({ ...f, amount: e.target.value }))} required /></FormField>
          <FormField label="Vade"><Input type="date" value={form.dueDate} onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))} required /></FormField>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <FormField label="Yöntem (ops.)">
            <Select value={form.method} onChange={(e) => setForm((f) => ({ ...f, method: e.target.value as PaymentMethod | "" }))}>
              <option value="">—</option>
              {(["Cash", "Card", "BankTransfer", "Other"] as PaymentMethod[]).map((m) => <option key={m} value={m}>{paymentMethodLabels[m]}</option>)}
            </Select>
          </FormField>
          <FormField label="Not (ops.)"><Input value={form.notes} onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))} /></FormField>
        </div>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Oluştur</Button>
        </div>
      </form>
    </Modal>
  );
}
