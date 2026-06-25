"use client";

import { useCallback, useEffect, useState } from "react";
import { reportService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type InstructorsReportDto,
  type PaymentStatus,
  type PaymentsReportDto,
  paymentStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Select, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";

const thisMonth = () => new Date().toISOString().slice(0, 7);
const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;

export default function ReportsPage() {
  const [tab, setTab] = useState<"payments" | "instructors">("payments");
  const [month, setMonth] = useState(thisMonth());

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Raporlar</h1>
        <p className="mt-1 text-sm text-muted">Aylık ödeme ve hoca iş hacmi.</p>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <FormField label="Ay"><Input type="month" value={month} onChange={(e) => setMonth(e.target.value)} /></FormField>
        <div className="flex gap-2">
          <Button variant={tab === "payments" ? "primary" : "secondary"} size="sm" onClick={() => setTab("payments")}>Ödemeler</Button>
          <Button variant={tab === "instructors" ? "primary" : "secondary"} size="sm" onClick={() => setTab("instructors")}>Hoca iş hacmi</Button>
        </div>
      </div>

      {tab === "payments" ? <PaymentsReport month={month} /> : <InstructorsReport month={month} />}
    </div>
  );
}

function PaymentsReport({ month }: { month: string }) {
  const [status, setStatus] = useState<"" | PaymentStatus>("");
  const [d, setD] = useState<PaymentsReportDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await reportService.payments(month, status || undefined)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Rapor yüklenemedi."); }
    finally { setLoading(false); }
  }, [month, status]);
  useEffect(() => { load(); }, [load]);

  return (
    <div className="space-y-4">
      {err && <Alert kind="error">{err}</Alert>}
      {d && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Card><p className="text-xs uppercase text-muted">Ödenen</p><p className="mt-1 text-xl font-semibold text-success">{money(d.totals.paidAmount)}</p><p className="text-xs text-muted">{d.totals.paidCount}</p></Card>
          <Card><p className="text-xs uppercase text-muted">Bekleyen</p><p className="mt-1 text-xl font-semibold text-warning">{money(d.totals.pendingAmount)}</p><p className="text-xs text-muted">{d.totals.pendingCount}</p></Card>
          <Card><p className="text-xs uppercase text-muted">Gecikmiş</p><p className="mt-1 text-xl font-semibold text-danger">{money(d.totals.overdueAmount)}</p><p className="text-xs text-muted">{d.totals.overdueCount}</p></Card>
        </div>
      )}
      <FormField label="Durum">
        <Select clearable value={status} onChange={(e) => setStatus(e.target.value as PaymentStatus | "")}>
          <option value="">Tümü</option>
          {(["Pending", "Paid", "Overdue", "Cancelled", "Refunded"] as PaymentStatus[]).map((s) => <option key={s} value={s}>{paymentStatusLabels[s]}</option>)}
        </Select>
      </FormField>
      <Table>
        <THead><TR><TH>Öğrenci</TH><TH>Tutar</TH><TH>Vade</TH><TH>Durum</TH></TR></THead>
        <tbody>
          {loading ? <TableEmpty colSpan={4}>Yükleniyor…</TableEmpty>
            : !d || d.payments.length === 0 ? <TableEmpty colSpan={4}>Kayıt yok.</TableEmpty>
            : d.payments.map((p) => (
              <TR key={p.id}>
                <TD className="font-medium">{p.studentName}</TD>
                <TD>{money(p.amount)}</TD>
                <TD className="text-muted">{p.dueDate}</TD>
                <TD><Badge tone={p.status === "Paid" ? "success" : p.status === "Overdue" ? "danger" : "warning"}>{paymentStatusLabels[p.status]}</Badge></TD>
              </TR>
            ))}
        </tbody>
      </Table>
    </div>
  );
}

function InstructorsReport({ month }: { month: string }) {
  const [d, setD] = useState<InstructorsReportDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await reportService.instructors({ month })); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Rapor yüklenemedi."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { load(); }, [load]);

  // Sprint 30: maaş/ödeme hesabını stüdyo yetkilisi kendisi yapsın diye kırılım — biz para hesaplamayız, sayıları veririz.
  return (
    <div className="space-y-3">
      {err && <Alert kind="error">{err}</Alert>}
      <Alert kind="info">
        <span className="font-medium">İşlenen</span> = onaylanmış (tamamlanmış) ders, ödeme temeli. <span className="font-medium">Planlanan</span> aya atanan tüm dersler;
        <span className="font-medium"> Yaklaşan</span> henüz işlenmemiş; <span className="font-medium">İşlenmedi</span> hoca kaynaklı iptal (telafi üretmiş).
        Devir: <span className="font-medium">+</span> başkasından devraldığı, <span className="font-medium">−</span> başkasına devrettiği ders.
      </Alert>
      <div className="overflow-x-auto">
        <Table>
          <THead><TR>
            <TH>Hoca</TH><TH>Planlanan</TH><TH>İşlenen</TH><TH>Yaklaşan</TH><TH>İşlenmedi</TH><TH>Öğrenci</TH><TH>Devir (+/−)</TH>
          </TR></THead>
          <tbody>
            {loading ? <TableEmpty colSpan={7}>Yükleniyor…</TableEmpty>
              : !d || d.instructors.length === 0 ? <TableEmpty colSpan={7}>Hoca yok.</TableEmpty>
              : d.instructors.map((i) => (
                <TR key={i.instructorProfileId}>
                  <TD className="font-medium">{i.instructorName}</TD>
                  <TD>{i.planned}</TD>
                  <TD><span className="font-semibold text-success">{i.delivered}</span></TD>
                  <TD className="text-muted">{i.upcoming}</TD>
                  <TD>{i.notDelivered > 0 ? <Badge tone="danger">{i.notDelivered}</Badge> : "—"}</TD>
                  <TD>{i.distinctStudents}</TD>
                  <TD>
                    {i.substitutionSessions === 0 && i.gaveSubstitution === 0 ? "—" : (
                      <span className="flex gap-1">
                        {i.substitutionSessions > 0 && <Badge tone="rose">+{i.substitutionSessions}</Badge>}
                        {i.gaveSubstitution > 0 && <Badge tone="neutral">−{i.gaveSubstitution}</Badge>}
                      </span>
                    )}
                  </TD>
                </TR>
              ))}
          </tbody>
        </Table>
      </div>
    </div>
  );
}
