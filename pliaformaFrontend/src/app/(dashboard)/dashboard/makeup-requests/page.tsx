"use client";

import { useCallback, useEffect, useState } from "react";
import { classTypeService, makeupRequestService } from "@/lib/studioApi";
import { useInstructors } from "@/lib/useInstructors";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type ClassTypeDto,
  type MakeupEligibleLineDto,
  type MakeupRequestDto,
  type MakeupRequestStatus,
  type StudentLevel,
  makeupRequestStatusLabels,
  studentLevelLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Modal, Select, Table, TableEmpty, TD, TH, THead, TR, useConfirm } from "@/components/ui";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;
const STATUSES: MakeupRequestStatus[] = ["Pending", "Approved", "Rejected", "Withdrawn"];
const conflictLabel = (r?: string | null) =>
  r === "instructor" ? "hoca dolu" : r === "student" ? "öğrenci dolu" :
  r === "pending-instructor" ? "hoca (talep)" : r === "pending-student" ? "öğrenci (talep)" : null;

export default function MakeupRequestsPage() {
  const confirm = useConfirm();
  const { canManage } = useRole();
  const { instructors } = useInstructors();
  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [status, setStatus] = useState<MakeupRequestStatus | "">("Pending");
  const [rows, setRows] = useState<MakeupRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [banner, setBanner] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [editApprove, setEditApprove] = useState<MakeupRequestDto | null>(null);
  const [rejectFor, setRejectFor] = useState<MakeupRequestDto | null>(null);
  const [requestOpen, setRequestOpen] = useState(false);

  useEffect(() => {
    classTypeService.list(true).then(setClassTypes).catch(() => setClassTypes([]));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await makeupRequestService.list(status || undefined));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Talepler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [status]);

  useEffect(() => { load(); }, [load]);

  const approve = async (r: MakeupRequestDto) => {
    if (!(await confirm({ title: "Telafi talebini onayla", confirmLabel: "Onayla", message: "Gerçek telafi dersi oluşturulacak ve 1 telafi kredisi kullanılacak." }))) return;
    setBusyId(r.id);
    setError(null);
    try {
      await makeupRequestService.approve(r.id);
      setBanner(`${r.studentName} için telafi dersi onaylandı.`);
      await load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Onaylanamadı.");
    } finally {
      setBusyId(null);
    }
  };

  // Sprint 30: hoca da kendi öğrencisi için talep oluşturabilir → ders programındaki "+ Telafi talebi" buraya taşındı.
  const withdraw = async (r: MakeupRequestDto) => {
    if (!(await confirm({ title: "Telafi talebini geri çek", tone: "danger", confirmLabel: "Geri çek", message: "Talep geri çekilecek; saat boşalır ve rezerve telafi hakkı serbest kalır." }))) return;
    setBusyId(r.id);
    setError(null);
    try { await makeupRequestService.withdraw(r.id); setBanner("Telafi talebi geri çekildi."); await load(); }
    catch (e) { setError(e instanceof ApiError ? e.message : "Geri çekilemedi."); }
    finally { setBusyId(null); }
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Telafi Talepleri</h1>
          <p className="mt-1 text-sm text-muted">
            {canManage
              ? "Telafi dersi taleplerini onaylayın, düzenleyin veya reddedin; buradan yeni talep de oluşturabilirsiniz."
              : "Kendi öğrencileriniz için telafi dersi talebi oluşturun ve takip edin (onay stüdyo yöneticisindedir)."}
          </p>
        </div>
        <Button onClick={() => setRequestOpen(true)}>+ Yeni telafi talebi</Button>
      </div>

      <Card>
        <FormField label="Durum">
          <Select clearable value={status} onChange={(e) => setStatus(e.target.value as MakeupRequestStatus | "")}>
            <option value="">Tümü</option>
            {STATUSES.map((s) => <option key={s} value={s}>{makeupRequestStatusLabels[s]}</option>)}
          </Select>
        </FormField>
      </Card>

      {error && <Alert kind="error">{error}</Alert>}
      {banner && <Alert kind="success">{banner}</Alert>}

      <Card>
        <Table>
          <THead><TR><TH>Tarih/Saat</TH><TH>Öğrenci</TH><TH>Hoca</TH><TH>Ders Tipi</TH><TH>Açıklama</TH><TH>Durum</TH><TH className="text-right">İşlem</TH></TR></THead>
          <tbody>
            {loading ? (
              <TableEmpty colSpan={7}>Yükleniyor…</TableEmpty>
            ) : rows.length === 0 ? (
              <TableEmpty colSpan={7}>Talep yok.</TableEmpty>
            ) : (
              rows.map((r) => (
                <TR key={r.id}>
                  <TD className="whitespace-nowrap text-muted">{fmt(r.startAt)}</TD>
                  <TD className="font-medium">{r.studentName}</TD>
                  <TD className="text-muted">{r.instructorName}</TD>
                  <TD className="text-muted">{r.classTypeName}</TD>
                  <TD className="text-xs text-muted">{r.description ?? "—"}</TD>
                  <TD>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <Badge tone={r.status === "Pending" ? "warning" : r.status === "Approved" ? "success" : "neutral"}>
                        {makeupRequestStatusLabels[r.status]}
                      </Badge>
                      {r.status === "Pending" && r.hasConflict && <Badge tone="danger">çakışma: {conflictLabel(r.conflictReason)}</Badge>}
                    </div>
                  </TD>
                  <TD className="text-right">
                    {r.status !== "Pending" ? (
                      <span className="text-xs text-muted">—</span>
                    ) : canManage ? (
                      <div className="flex justify-end gap-1.5">
                        <Button size="sm" loading={busyId === r.id} onClick={() => approve(r)}>Onayla</Button>
                        <Button size="sm" variant="secondary" onClick={() => setEditApprove(r)}>Düzenle</Button>
                        <Button size="sm" variant="ghost" onClick={() => setRejectFor(r)}>Reddet</Button>
                      </div>
                    ) : (
                      <Button size="sm" variant="ghost" loading={busyId === r.id} onClick={() => withdraw(r)}>Geri çek</Button>
                    )}
                  </TD>
                </TR>
              ))
            )}
          </tbody>
        </Table>
      </Card>

      {editApprove && (
        <EditApproveModal
          request={editApprove}
          instructors={instructors}
          classTypes={classTypes}
          onClose={() => setEditApprove(null)}
          onDone={() => { setEditApprove(null); setBanner("Telafi talebi düzenlenerek onaylandı."); load(); }}
        />
      )}
      {rejectFor && (
        <RejectModal
          request={rejectFor}
          onClose={() => setRejectFor(null)}
          onDone={() => { setRejectFor(null); setBanner("Telafi talebi reddedildi."); load(); }}
        />
      )}
      {requestOpen && (
        <MakeupRequestModal
          onClose={() => setRequestOpen(false)}
          onDone={() => { setRequestOpen(false); setBanner("Telafi talebi oluşturuldu."); load(); }}
        />
      )}
    </div>
  );
}

// Telafi dersi talebi (Sprint 28C-2: LINE-bazlı; Sprint 30: ders programından buraya taşındı). Uygun LINE listesi
// (öğrenci+ders tipi+hoca+kalan telafi+seviye). Instructor → yalnız kendi line'ları (backend zorlar); manager → tümü.
function MakeupRequestModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const [eligible, setEligible] = useState<MakeupEligibleLineDto[]>([]);
  const [level, setLevel] = useState<StudentLevel | "">("");
  const [lineId, setLineId] = useState("");
  const [startAt, setStartAt] = useState("");
  const [description, setDescription] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    makeupRequestService.eligibleStudents(level || null).then(setEligible).catch(() => setEligible([]));
  }, [level]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lineId) { setErr("Satır (paket) seçin."); return; }
    setBusy(true);
    setErr(null);
    try {
      await makeupRequestService.create({ studentContractLineId: lineId, startAt, description: description || undefined });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Talep oluşturulamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title="Telafi dersi talebi">
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <Alert kind="info">Talep onaya gider; onaylanana kadar saat bloklanır ve seçili satırın telafi hakkı rezerve edilir.</Alert>
        <FormField label="Seviye filtresi (ops.)">
          <Select value={level} onChange={(e) => { setLevel(e.target.value as StudentLevel | ""); setLineId(""); }}>
            <option value="">Tüm seviyeler</option>
            {(Object.keys(studentLevelLabels) as StudentLevel[]).map((lv) => <option key={lv} value={lv}>{studentLevelLabels[lv]}</option>)}
          </Select>
        </FormField>
        <FormField label="Satır (öğrenci · ders tipi · tarih · kalan telafi)">
          <Select value={lineId} onChange={(e) => setLineId(e.target.value)} required>
            <option value="">Seçin…</option>
            {eligible.map((l) => (
              <option key={l.lineId} value={l.lineId}>
                {l.studentName} · {l.classTypeName} · {l.startDate}–{l.endDate} · {l.availableCredits} telafi{l.level ? ` · ${studentLevelLabels[l.level]}` : ""}{l.isIndividual ? " · Bireysel" : " · Grup"} · {l.instructorName}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label="Başlangıç" hint="Gelecek bir zaman olmalı (telafi satır bitişinden sonra da olabilir)">
          <Input type="datetime-local" value={startAt} onChange={(e) => setStartAt(e.target.value)} required />
        </FormField>
        <FormField label="Açıklama (ops.)">
          <Input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="örn. 12 Mart dersinin telafisi" />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy} disabled={eligible.length === 0}>Talep oluştur</Button>
        </div>
      </form>
    </Modal>
  );
}

function EditApproveModal({ request, instructors, classTypes, onClose, onDone }: {
  request: MakeupRequestDto; instructors: { id: string; name: string }[]; classTypes: ClassTypeDto[];
  onClose: () => void; onDone: () => void;
}) {
  const [startAt, setStartAt] = useState(request.startAt.slice(0, 16));
  const [instructorId, setInstructorId] = useState(request.requestedInstructorProfileId);
  const [classTypeId, setClassTypeId] = useState(request.classTypeId);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await makeupRequestService.approve(request.id, {
        startAt,
        requestedInstructorProfileId: instructorId,
        classTypeId,
      });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Onaylanamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${request.studentName} — Düzenleyerek onayla`}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <p className="text-sm text-muted">Tarih/saat, hoca veya ders tipini değiştirerek onaylayabilirsiniz. Çakışma final değerlerle yeniden kontrol edilir.</p>
        <FormField label="Başlangıç">
          <Input type="datetime-local" value={startAt} onChange={(e) => setStartAt(e.target.value)} required />
        </FormField>
        <FormField label="Hoca">
          <Select value={instructorId} onChange={(e) => setInstructorId(e.target.value)}>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
        <FormField label="Ders Tipi">
          <Select value={classTypeId} onChange={(e) => setClassTypeId(e.target.value)}>
            {classTypes.map((c) => <option key={c.id} value={c.id}>{c.name} · {c.defaultDurationMinutes}dk</option>)}
          </Select>
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Düzenleyerek onayla</Button>
        </div>
      </form>
    </Modal>
  );
}

function RejectModal({ request, onClose, onDone }: { request: MakeupRequestDto; onClose: () => void; onDone: () => void }) {
  const [note, setNote] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setErr(null);
    try {
      await makeupRequestService.reject(request.id, { decisionNote: note || undefined });
      onDone();
    } catch (e2) {
      setErr(e2 instanceof ApiError ? e2.message : "Reddedilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${request.studentName} — Talebi reddet`}>
      <form onSubmit={submit} className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}
        <FormField label="Red gerekçesi (ops.)">
          <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder="örn. uygun saat yok" />
        </FormField>
        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" loading={busy}>Reddet</Button>
        </div>
      </form>
    </Modal>
  );
}
