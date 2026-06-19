"use client";

import { useCallback, useEffect, useState } from "react";
import { makeupCreditService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type MakeupPolicyDto,
  type StudentDto,
  type StudentMakeupCreditsDto,
  creditReasonLabels,
  creditTypeLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;

export default function MakeupCreditsPage() {
  const { canManage } = useRole();
  const [search, setSearch] = useState("");
  const [results, setResults] = useState<StudentDto[]>([]);
  const [selected, setSelected] = useState<StudentDto | null>(null);
  const [credits, setCredits] = useState<StudentMakeupCreditsDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [grantOpen, setGrantOpen] = useState(false);
  const [revokeOpen, setRevokeOpen] = useState(false);

  useEffect(() => {
    const t = setTimeout(async () => {
      try {
        setResults(search.trim() ? await studentService.search(search.trim()) : await studentService.list());
      } catch {
        setResults([]);
      }
    }, 300);
    return () => clearTimeout(t);
  }, [search]);

  const loadCredits = useCallback(async (studentId: string) => {
    setError(null);
    try {
      setCredits(await makeupCreditService.forStudent(studentId));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Krediler yüklenemedi.");
      setCredits(null);
    }
  }, []);

  const pick = (s: StudentDto) => {
    setSelected(s);
    loadCredits(s.id);
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Telafi Kredileri</h1>
        <p className="mt-1 text-sm text-muted">Öğrenci telafi kredisi bakiyesi ve hareketleri.</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {canManage && <PolicyCard />}

      <div className="grid gap-4 lg:grid-cols-[18rem_1fr]">
        <Card>
          <Input placeholder="Öğrenci ara…" value={search} onChange={(e) => setSearch(e.target.value)} />
          <div className="mt-3 max-h-96 space-y-1 overflow-y-auto">
            {results.length === 0 ? (
              <p className="text-sm text-muted">Öğrenci bulunamadı.</p>
            ) : (
              results.map((s) => (
                <button
                  key={s.id}
                  onClick={() => pick(s)}
                  className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm transition-colors ${
                    selected?.id === s.id ? "bg-primary-soft text-primary-hover" : "text-foreground hover:bg-surface-muted"
                  }`}
                >
                  <span>{s.fullName}</span>
                  {s.status === "Inactive" && <Badge tone="neutral">Pasif</Badge>}
                </button>
              ))
            )}
          </div>
        </Card>

        <Card>
          {!selected || !credits ? (
            <p className="text-sm text-muted">Bir öğrenci seçin.</p>
          ) : (
            <>
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium text-foreground">{credits.studentName}</p>
                  <p className="text-sm text-muted">Telafi kredisi bakiyesi</p>
                </div>
                <div className="flex items-center gap-2">
                  <Button size="sm" onClick={() => setGrantOpen(true)}>Kredi ver</Button>
                  <Button size="sm" variant="secondary" disabled={credits.balance <= 0} onClick={() => setRevokeOpen(true)}>Geri al</Button>
                </div>
              </div>

              {/* Sprint 20: kazanılan / kullanılan / kullanılabilir özeti */}
              <div className="mt-3 grid grid-cols-3 gap-2 text-center">
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Kazanılan</p><p className="text-lg font-semibold text-success">{credits.summary.earned}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Kullanılan</p><p className="text-lg font-semibold text-danger">{credits.summary.used}</p></div>
                <div className="rounded-lg border border-border p-2"><p className="text-xs text-muted">Kullanılabilir</p><p className="text-lg font-semibold text-foreground">{credits.summary.available}</p></div>
              </div>

              <div className="mt-4">
                <Table>
                  <THead><TR><TH>Tarih</TH><TH>İşlem</TH><TH>Gerekçe</TH><TH>Kaynak / Kullanım</TH><TH>Not</TH><TH className="text-right">Miktar</TH></TR></THead>
                  <tbody>
                    {credits.transactions.length === 0 ? (
                      <TableEmpty colSpan={6}>Henüz kredi hareketi yok.</TableEmpty>
                    ) : (
                      credits.transactions.map((t) => (
                        <TR key={t.id}>
                          <TD className="text-muted">{fmt(t.createdAt)}</TD>
                          <TD><Badge tone={t.amount > 0 ? "success" : "neutral"}>{creditTypeLabels[t.type]}</Badge></TD>
                          <TD className="text-muted">{creditReasonLabels[t.reason]}</TD>
                          <TD className="text-xs text-muted">{t.usedForLabel ? `→ ${t.usedForLabel}` : t.sourceLabel ?? "—"}</TD>
                          <TD className="text-muted">{t.notes ?? "—"}</TD>
                          <TD className={`text-right font-medium ${t.amount > 0 ? "text-success" : "text-danger"}`}>
                            {t.amount > 0 ? `+${t.amount}` : t.amount}
                          </TD>
                        </TR>
                      ))
                    )}
                  </tbody>
                </Table>
              </div>
            </>
          )}
        </Card>
      </div>

      {grantOpen && selected && (
        <CreditActionModal
          title={`${selected.fullName} — Kredi ver`}
          actionLabel="Kredi ver"
          onClose={() => setGrantOpen(false)}
          onSubmit={(amount, notes) => makeupCreditService.grant(selected.id, { amount, notes })}
          onDone={(c) => { setCredits(c); setGrantOpen(false); }}
        />
      )}
      {revokeOpen && selected && (
        <CreditActionModal
          title={`${selected.fullName} — Kredi geri al`}
          actionLabel="Geri al"
          onClose={() => setRevokeOpen(false)}
          onSubmit={(amount, notes) => makeupCreditService.revoke(selected.id, { amount, notes })}
          onDone={(c) => { setCredits(c); setRevokeOpen(false); }}
        />
      )}
    </div>
  );
}

function CreditActionModal({
  title, actionLabel, onClose, onSubmit, onDone,
}: {
  title: string;
  actionLabel: string;
  onClose: () => void;
  onSubmit: (amount: number, notes?: string) => Promise<StudentMakeupCreditsDto>;
  onDone: (c: StudentMakeupCreditsDto) => void;
}) {
  const [amount, setAmount] = useState("1");
  const [notes, setNotes] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      onDone(await onSubmit(Number(amount), notes || undefined));
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "İşlem başarısız.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={title}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Miktar">
          <Input type="number" min={1} max={10} value={amount} onChange={(e) => setAmount(e.target.value)} required />
        </FormField>
        <FormField label="Not (ops.)">
          <Input value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="gerekçe" />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>{actionLabel}</Button>
        </div>
      </form>
    </Modal>
  );
}

function PolicyCard() {
  const [policy, setPolicy] = useState<MakeupPolicyDto | null>(null);
  const [editing, setEditing] = useState(false);
  const [err, setErr] = useState<string | null>(null);
  const [ok, setOk] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    makeupCreditService.getPolicy().then(setPolicy).catch(() => setPolicy(null));
  }, []);

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!policy) return;
    setBusy(true);
    setErr(null);
    setOk(false);
    try {
      setPolicy(await makeupCreditService.updatePolicy(policy));
      setOk(true);
      setEditing(false);
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Kaydedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  if (!policy) return null;

  return (
    <Card>
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-foreground">Telafi Politikası</h3>
        {!editing && <Button size="sm" variant="ghost" onClick={() => { setEditing(true); setOk(false); }}>Düzenle</Button>}
      </div>
      {err && <Alert kind="error">{err}</Alert>}
      {ok && <Alert kind="success">Politika güncellendi.</Alert>}
      {!editing ? (
        <div className="mt-3 grid gap-2 text-sm text-muted sm:grid-cols-2">
          <p>Aylık manuel kredi limiti: <span className="text-foreground">{policy.monthlyManualCreditLimit}</span></p>
          <p>Varsayılan geçerlilik (gün): <span className="text-foreground">{policy.defaultCreditValidityDays}</span> <span className="text-xs">(şu an uygulanmıyor)</span></p>
          <p>Hoca manuel kredi verebilir: <span className="text-foreground">{policy.allowInstructorManualGrant ? "Evet" : "Hayır"}</span></p>
          <p>Kredisiz makeup (yönetici): <span className="text-foreground">{policy.allowMakeupBookingWithoutCreditByAdmin ? "Evet" : "Hayır"}</span> <span className="text-xs">(Sprint 20: artık kullanılmıyor — telafi hakkı zorunlu)</span></p>
        </div>
      ) : (
        <form onSubmit={save} className="mt-3 space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <FormField label="Aylık manuel kredi limiti">
              <Input type="number" min={0} max={100} value={policy.monthlyManualCreditLimit}
                onChange={(e) => setPolicy({ ...policy, monthlyManualCreditLimit: Number(e.target.value) })} />
            </FormField>
            <FormField label="Varsayılan geçerlilik (gün)" hint="Şu an yalnızca kaydedilir; expiry henüz uygulanmıyor">
              <Input type="number" min={0} max={3650} value={policy.defaultCreditValidityDays}
                onChange={(e) => setPolicy({ ...policy, defaultCreditValidityDays: Number(e.target.value) })} />
            </FormField>
          </div>
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" checked={policy.allowInstructorManualGrant}
              onChange={(e) => setPolicy({ ...policy, allowInstructorManualGrant: e.target.checked })} />
            Hocalar manuel telafi kredisi verebilir
          </label>
          <label className="flex items-center gap-2 text-sm text-foreground">
            <input type="checkbox" checked={policy.allowMakeupBookingWithoutCreditByAdmin}
              onChange={(e) => setPolicy({ ...policy, allowMakeupBookingWithoutCreditByAdmin: e.target.checked })} />
            Yönetici, kredisi olmayan öğrenci için telafi rezervasyonu yapabilir
          </label>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="secondary" onClick={() => setEditing(false)}>Vazgeç</Button>
            <Button type="submit" loading={busy}>Kaydet</Button>
          </div>
        </form>
      )}
    </Card>
  );
}
