"use client";

import { useCallback, useEffect, useState } from "react";
import { reportService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type InstructorsReportDto,
  type PaymentsReportDto,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";
import { formatDate } from "@/lib/format";

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
  const [d, setD] = useState<PaymentsReportDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await reportService.payments(month)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Rapor yüklenemedi."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { load(); }, [load]);

  // Sprint 45: "Ödenen" = bu ay alınan ödemeler; "Kalan ödeme / Gecikmiş borç" = aktif satırların anlık durumu (line-bazlı).
  return (
    <div className="space-y-4">
      {err && <Alert kind="error">{err}</Alert>}
      {d && (
        <div className="grid gap-4 sm:grid-cols-3">
          <Card><p className="text-xs uppercase text-muted">Bu ay alınan ödeme</p><p className="mt-1 text-xl font-semibold text-success">{money(d.totals.paidAmount)}</p><p className="text-xs text-muted">{d.totals.paidCount} ödeme</p></Card>
          <Card><p className="text-xs uppercase text-muted">Güncel kalan ödeme</p><p className="mt-1 text-xl font-semibold text-warning">{money(d.totals.pendingAmount)}</p><p className="text-xs text-muted">{d.totals.pendingCount} paket · ay seçiminden etkilenmez</p></Card>
          <Card><p className="text-xs uppercase text-muted">Güncel gecikmiş borç</p><p className="mt-1 text-xl font-semibold text-danger">{money(d.totals.overdueAmount)}</p><p className="text-xs text-muted">{d.totals.overdueCount} paket · ay seçiminden etkilenmez</p></Card>
        </div>
      )}
      <p className="text-xs text-muted">"Alınan ödeme" seçili <span className="font-medium">ay</span>a göre filtrelenir. "Kalan ödeme" ve "Gecikmiş borç" ise ay seçiminden bağımsız, her zaman <span className="font-medium">güncel (bugünkü)</span> durumu gösterir (paketin son ödeme tarihinden hesaplanır). Aşağıda seçili ayda alınan ödemeler listelenir.</p>
      <Table>
        <THead><TR><TH>Öğrenci</TH><TH>Tutar</TH><TH>Alındı</TH><TH>Durum</TH></TR></THead>
        <tbody>
          {loading ? <TableEmpty colSpan={4}>Yükleniyor…</TableEmpty>
            : !d || d.payments.length === 0 ? <TableEmpty colSpan={4}>Bu ay alınan ödeme yok.</TableEmpty>
            : d.payments.map((p) => (
              <TR key={p.id}>
                <TD className="font-medium">{p.studentName}</TD>
                <TD>{money(p.amount)}</TD>
                <TD className="text-muted">{p.paidAt ? formatDate(p.paidAt) : formatDate(p.dueDate)}</TD>
                <TD><Badge tone="success">Ödendi</Badge></TD>
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
