"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { platformService } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import {
  type PlatformStudioListItemDto,
  type StudioStatus,
  studioStatusLabels,
  subscriptionStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Card, Input, Select, Table, THead, TR, TH, TD, TableEmpty } from "@/components/ui";

const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;
const statusTone = (s: StudioStatus) => (s === "Active" ? "success" : s === "Suspended" ? "warning" : "danger");

export default function PlatformStudiosPage() {
  const [items, setItems] = useState<PlatformStudioListItemDto[]>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState<StudioStatus | "">("");
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await platformService.studios({ search: search || undefined, status: status || undefined }));
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Stüdyolar yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [search, status]);

  // Arama yazıldıkça debounce.
  useEffect(() => {
    const t = setTimeout(load, 300);
    return () => clearTimeout(t);
  }, [load]);

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Stüdyolar</h1>
        <p className="mt-1 text-sm text-muted">Tüm stüdyolar · {items.length} kayıt</p>
      </div>

      <div className="flex flex-col gap-3 sm:flex-row">
        <Input placeholder="Ad veya slug ara…" value={search} onChange={(e) => setSearch(e.target.value)} className="sm:max-w-xs" />
        <Select clearable value={status} onChange={(e) => setStatus(e.target.value as StudioStatus | "")} className="sm:max-w-[180px]">
          <option value="">Tüm durumlar</option>
          <option value="Active">Aktif</option>
          <option value="Suspended">Askıda</option>
          <option value="Cancelled">İptal</option>
        </Select>
      </div>

      {err && <Alert kind="error">{err}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : (
        <>
          {/* Masaüstü: tablo */}
          <div className="hidden lg:block">
            <Table>
              <THead>
                <TR>
                  <TH>Stüdyo</TH>
                  <TH>Sahip</TH>
                  <TH>Durum</TH>
                  <TH>Plan</TH>
                  <TH className="text-right">Kullanıcı</TH>
                  <TH className="text-right">Öğrenci</TH>
                  <TH className="text-right">Bu ay ödeme</TH>
                  <TH className="text-right">İşlem</TH>
                </TR>
              </THead>
              <tbody>
                {items.length === 0 ? (
                  <TableEmpty colSpan={8}>Stüdyo bulunamadı.</TableEmpty>
                ) : (
                  items.map((s) => (
                    <TR key={s.id}>
                      <TD>
                        <div className="font-medium text-foreground">{s.name}</div>
                        <div className="text-xs text-muted">{s.slug}</div>
                      </TD>
                      <TD>
                        <div className="text-foreground">{s.ownerName}</div>
                        <div className="text-xs text-muted">{s.ownerEmail}</div>
                      </TD>
                      <TD><Badge tone={statusTone(s.status)}>{studioStatusLabels[s.status]}</Badge></TD>
                      <TD>
                        {s.planName ?? "—"}
                        {s.subscriptionStatus && (
                          <span className="ml-1 text-xs text-muted">({subscriptionStatusLabels[s.subscriptionStatus]})</span>
                        )}
                      </TD>
                      <TD className="text-right">{s.userCount}</TD>
                      <TD className="text-right">{s.studentCount}</TD>
                      <TD className="text-right">{money(s.paymentAmountThisMonth)}</TD>
                      <TD className="text-right">
                        <Link href={`/platform/studios/${s.id}`} className="text-sm font-medium text-primary hover:text-primary-hover">
                          Detay →
                        </Link>
                      </TD>
                    </TR>
                  ))
                )}
              </tbody>
            </Table>
          </div>

          {/* Mobil: kartlar */}
          <div className="space-y-3 lg:hidden">
            {items.length === 0 ? (
              <Card><p className="text-sm text-muted">Stüdyo bulunamadı.</p></Card>
            ) : (
              items.map((s) => (
                <Link key={s.id} href={`/platform/studios/${s.id}`} className="block">
                  <Card>
                    <div className="flex items-start justify-between gap-2">
                      <div>
                        <p className="font-medium text-foreground">{s.name}</p>
                        <p className="text-xs text-muted">{s.ownerEmail}</p>
                      </div>
                      <Badge tone={statusTone(s.status)}>{studioStatusLabels[s.status]}</Badge>
                    </div>
                    <p className="mt-2 text-xs text-muted">
                      {s.userCount} kullanıcı · {s.studentCount} öğrenci · {money(s.paymentAmountThisMonth)} bu ay
                    </p>
                  </Card>
                </Link>
              ))
            )}
          </div>
        </>
      )}
    </div>
  );
}
