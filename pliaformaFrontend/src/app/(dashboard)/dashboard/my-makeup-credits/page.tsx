"use client";

import { useCallback, useEffect, useState } from "react";
import { studentMakeupCreditService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { type MyMakeupCreditsDto, creditReasonLabels, creditTypeLabels } from "@/lib/domain-types";
import { Alert, Badge, Card, Table, TableEmpty, TD, TH, THead, TR } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;

export default function MyMakeupCreditsPage() {
  const [data, setData] = useState<MyMakeupCreditsDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await studentMakeupCreditService.mine());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Krediler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Telafi Kredilerim</h1>
          <p className="mt-1 text-sm text-muted">Telafi dersi rezervasyonunda kullanılır.</p>
        </div>
        <Badge tone={(data?.balance ?? 0) > 0 ? "rose" : "neutral"}>Bakiye: {data?.balance ?? 0}</Badge>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Card>
        <h3 className="mb-3 text-sm font-semibold text-muted">Hareket geçmişi</h3>
        <Table>
          <THead><TR><TH>Tarih</TH><TH>İşlem</TH><TH>Gerekçe</TH><TH className="text-right">Miktar</TH></TR></THead>
          <tbody>
            {loading ? (
              <TableEmpty colSpan={4}>Yükleniyor…</TableEmpty>
            ) : !data || data.transactions.length === 0 ? (
              <TableEmpty colSpan={4}>Henüz kredi hareketi yok.</TableEmpty>
            ) : (
              data.transactions.map((t) => (
                <TR key={t.id}>
                  <TD className="text-muted">{fmt(t.createdAt)}</TD>
                  <TD><Badge tone={t.amount > 0 ? "success" : "neutral"}>{creditTypeLabels[t.type]}</Badge></TD>
                  <TD className="text-muted">{creditReasonLabels[t.reason]}</TD>
                  <TD className={`text-right font-medium ${t.amount > 0 ? "text-success" : "text-danger"}`}>
                    {t.amount > 0 ? `+${t.amount}` : t.amount}
                  </TD>
                </TR>
              ))
            )}
          </tbody>
        </Table>
      </Card>
    </div>
  );
}
