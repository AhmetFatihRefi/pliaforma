"use client";

import { useCallback, useEffect, useState } from "react";
import { platformService, type SecurityAuditQuery } from "@/lib/platformApi";
import { ApiError } from "@/lib/api";
import type { SecurityAuditLogPageDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, Input, Select, Table, THead, TR, TH, TD, TableEmpty } from "@/components/ui";

// Backend SecurityAuditEventType (string). Filtre dropdown'u için.
const EVENT_TYPES = [
  "LoginSucceeded", "LoginFailed", "AccountLockedOut", "Logout",
  "PasswordResetRequested", "PasswordResetCompleted", "PasswordChanged",
  "MFASetupStarted", "MFASetupCompleted", "MFAChallengeCreated", "MFASucceeded",
  "MFAFailed", "MFARecoveryCodeUsed", "MFAResetCompleted",
  "RoleChanged", "MemberInvited", "MembershipAccepted", "MembershipRejected", "MemberRemoved",
  "PaymentCreated", "PaymentUpdated", "PaymentMarkedPaid", "PaymentCancelled",
  "ContractCreated", "ContractAmended", "ContractCancelled",
  "StudentDataListed", "HealthNotesAccessed",
];

const PAGE_SIZE = 20;
const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 19)}`;

export default function SecurityAuditPage() {
  const [data, setData] = useState<SecurityAuditLogPageDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);

  // Filtreler (uygula'ya basınca etkin olur).
  const [eventType, setEventType] = useState("");
  const [outcome, setOutcome] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [studioId, setStudioId] = useState("");
  const [actorUserId, setActorUserId] = useState("");

  const load = useCallback(async (p: number) => {
    setLoading(true);
    try {
      const q: SecurityAuditQuery = { page: p, pageSize: PAGE_SIZE };
      if (eventType) q.eventType = eventType;
      if (outcome) q.outcome = outcome;
      if (from) q.from = new Date(from).toISOString();
      if (to) q.to = new Date(to).toISOString();
      if (studioId.trim()) q.studioId = studioId.trim();
      if (actorUserId.trim()) q.actorUserId = actorUserId.trim();
      setData(await platformService.securityAudit(q));
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Denetim kayıtları yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [eventType, outcome, from, to, studioId, actorUserId]);

  useEffect(() => {
    load(page);
  }, [page, load]);

  const applyFilters = () => {
    if (page === 1) load(1);
    else setPage(1); // page değişimi load'u tetikler
  };

  const resetFilters = () => {
    setEventType(""); setOutcome(""); setFrom(""); setTo(""); setStudioId(""); setActorUserId("");
    setPage(1);
  };

  const total = data?.total ?? 0;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Güvenlik Denetimi</h1>
          <p className="mt-1 text-sm text-muted">
            Tüm stüdyolar — salt-okunur denetim kayıtları (PII maskeli). Export yok.
          </p>
        </div>
        <Button variant="secondary" size="sm" onClick={() => load(page)}>Yenile</Button>
      </div>

      <Card>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Olay türü</span>
            <Select value={eventType} onChange={(e) => setEventType(e.target.value)}>
              <option value="">Tümü</option>
              {EVENT_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </Select>
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Sonuç</span>
            <Select value={outcome} onChange={(e) => setOutcome(e.target.value)}>
              <option value="">Tümü</option>
              <option value="Success">Success</option>
              <option value="Failure">Failure</option>
            </Select>
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Aktör (UserId)</span>
            <Input value={actorUserId} onChange={(e) => setActorUserId(e.target.value)} placeholder="GUID (opsiyonel)" />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Stüdyo (StudioId)</span>
            <Input value={studioId} onChange={(e) => setStudioId(e.target.value)} placeholder="GUID (opsiyonel)" />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Başlangıç</span>
            <Input type="datetime-local" value={from} onChange={(e) => setFrom(e.target.value)} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-muted">Bitiş</span>
            <Input type="datetime-local" value={to} onChange={(e) => setTo(e.target.value)} />
          </label>
        </div>
        <div className="mt-4 flex gap-2">
          <Button size="sm" onClick={applyFilters}>Filtrele</Button>
          <Button variant="secondary" size="sm" onClick={resetFilters}>Temizle</Button>
        </div>
      </Card>

      {err && <Alert kind="error">{err}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>Zaman (UTC)</TH>
            <TH>Olay</TH>
            <TH>Sonuç</TH>
            <TH>Aktör</TH>
            <TH>Stüdyo</TH>
            <TH>Hedef</TH>
            <TH>IP</TH>
            <TH>Detay</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={8}>Yükleniyor…</TableEmpty>
          ) : !data || data.items.length === 0 ? (
            <TableEmpty colSpan={8}>Kayıt yok.</TableEmpty>
          ) : (
            data.items.map((r) => (
              <TR key={r.id}>
                <TD className="whitespace-nowrap text-xs text-muted">{fmt(r.occurredAt)}</TD>
                <TD className="whitespace-nowrap font-medium">{r.eventType}</TD>
                <TD>
                  <Badge tone={r.outcome === "Success" ? "success" : "danger"}>{r.outcome}</Badge>
                </TD>
                <TD className="text-xs">{r.actorEmailMasked ?? (r.actorUserId ? r.actorUserId.slice(0, 8) : "—")}</TD>
                <TD className="text-xs">{r.studioName ?? (r.studioId ? r.studioId.slice(0, 8) : "—")}</TD>
                <TD className="text-xs">{r.targetType ? `${r.targetType}${r.targetId ? ` ${r.targetId.slice(0, 8)}` : ""}` : "—"}</TD>
                <TD className="text-xs text-muted">{r.ip ?? "—"}</TD>
                <TD className="text-xs text-muted">{r.detail ?? "—"}</TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      <div className="flex items-center justify-between text-sm text-muted">
        <span>Toplam {total} kayıt · sayfa {data?.page ?? page}/{totalPages}</span>
        <div className="flex gap-2">
          <Button variant="secondary" size="sm" disabled={page <= 1 || loading} onClick={() => setPage((p) => Math.max(1, p - 1))}>
            Önceki
          </Button>
          <Button variant="secondary" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage((p) => p + 1)}>
            Sonraki
          </Button>
        </div>
      </div>
    </div>
  );
}
