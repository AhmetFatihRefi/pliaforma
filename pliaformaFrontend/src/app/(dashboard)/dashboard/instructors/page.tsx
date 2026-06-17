"use client";

import { useCallback, useEffect, useState } from "react";
import { classTypeService, instructorService, memberService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type ClassTypeDto,
  type InstructorAssignmentDto,
  type MemberDto,
  type RateType,
  rateTypeLabels,
} from "@/lib/domain-types";
import type { StudioRole } from "@/lib/types";
import {
  Alert,
  Badge,
  Button,
  FormField,
  Input,
  Modal,
  Select,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  TR,
} from "@/components/ui";

const statusTone = (s: MemberDto["status"]) =>
  s === "Active" ? "success" : s === "Pending" ? "warning" : "neutral";

export default function InstructorsPage() {
  const { role: myRole, canManage } = useRole();
  const isOwner = myRole === "Owner";

  const [members, setMembers] = useState<MemberDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [inviteOpen, setInviteOpen] = useState(false);
  const [invite, setInvite] = useState({
    email: "",
    role: "Instructor" as StudioRole,
    displayName: "",
    rate: 0,
    rateType: "PerSession" as RateType,
  });
  const [inviteErr, setInviteErr] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [assignFor, setAssignFor] = useState<MemberDto | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setMembers(await memberService.list(true));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Üyeler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  // StudioAdmin yalnızca Instructor davet edebilir (Owner her ikisini).
  const inviteRoleOptions: StudioRole[] = isOwner ? ["Instructor", "StudioAdmin"] : ["Instructor"];

  const sendInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setInviteErr(null);
    try {
      const isInstructor = invite.role === "Instructor";
      await memberService.invite({
        email: invite.email,
        role: invite.role,
        displayName: invite.displayName || undefined,
        rate: isInstructor ? Number(invite.rate) : undefined,
        rateType: isInstructor ? invite.rateType : undefined,
      });
      setInviteOpen(false);
      setInvite({ email: "", role: "Instructor", displayName: "", rate: 0, rateType: "PerSession" });
      await load();
    } catch (err) {
      setInviteErr(err instanceof ApiError ? err.message : "Davet gönderilemedi.");
    } finally {
      setSaving(false);
    }
  };

  const changeRole = async (m: MemberDto, role: StudioRole) => {
    try {
      await memberService.changeRole(m.membershipId, role);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Rol değiştirilemedi.");
    }
  };

  const editRate = async (m: MemberDto) => {
    if (!m.instructorProfileId) return;
    const input = prompt(`${m.fullName} için yeni ücret:`, String(m.rate ?? 0));
    if (input === null) return;
    try {
      await instructorService.updateRate(m.instructorProfileId, { rate: Number(input), rateType: m.rateType ?? "PerSession" });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Ücret güncellenemedi.");
    }
  };

  const remove = async (m: MemberDto) => {
    if (!confirm(`${m.fullName} stüdyodan çıkarılsın mı?`)) return;
    try {
      await memberService.remove(m.membershipId);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Çıkarılamadı.");
    }
  };

  // Owner koruması + StudioAdmin↔StudioAdmin koruması (UI kolaylığı; backend de zorlar).
  const canRemove = (m: MemberDto) =>
    canManage && m.role !== "Owner" && !(myRole === "StudioAdmin" && m.role === "StudioAdmin");
  const canChangeRole = (m: MemberDto) => isOwner && m.role !== "Owner";

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Hocalar & Üyeler</h1>
          <p className="mt-1 text-sm text-muted">Davet edin, rol/ücret belirleyin, gruplara atayın.</p>
        </div>
        {canManage && <Button onClick={() => setInviteOpen(true)}>+ Davet et</Button>}
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>İsim</TH>
            <TH>E-posta</TH>
            <TH>Rol</TH>
            <TH>Durum</TH>
            <TH>Ücret</TH>
            {canManage && <TH className="text-right">İşlem</TH>}
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={canManage ? 6 : 5}>Yükleniyor…</TableEmpty>
          ) : members.length === 0 ? (
            <TableEmpty colSpan={canManage ? 6 : 5}>Henüz üye yok.</TableEmpty>
          ) : (
            members.map((m) => (
              <TR key={m.membershipId}>
                <TD className="font-medium">{m.fullName}</TD>
                <TD className="text-muted">{m.email}</TD>
                <TD>
                  {canChangeRole(m) ? (
                    <select
                      value={m.role}
                      onChange={(e) => changeRole(m, e.target.value as StudioRole)}
                      className="h-8 rounded-lg border border-border bg-surface px-2 text-xs text-foreground focus:border-primary focus:outline-none"
                      aria-label="Rol değiştir"
                    >
                      <option value="StudioAdmin">StudioAdmin</option>
                      <option value="Instructor">Instructor</option>
                    </select>
                  ) : (
                    <Badge tone={m.role === "Owner" ? "rose" : "neutral"}>{m.role}</Badge>
                  )}
                </TD>
                <TD><Badge tone={statusTone(m.status)}>{m.status}</Badge></TD>
                <TD>{m.instructorProfileId ? `${m.rate} · ${rateTypeLabels[m.rateType ?? "PerSession"]}` : "—"}</TD>
                {canManage && (
                  <TD className="text-right">
                    <div className="flex justify-end gap-2">
                      {m.instructorProfileId && (
                        <>
                          <Button variant="ghost" size="sm" onClick={() => editRate(m)}>Ücret</Button>
                          <Button variant="ghost" size="sm" onClick={() => setAssignFor(m)}>Gruplar</Button>
                        </>
                      )}
                      {canRemove(m) ? (
                        <Button variant="danger" size="sm" onClick={() => remove(m)}>Çıkar</Button>
                      ) : m.role === "Owner" ? (
                        <span className="px-2 text-xs text-muted/60">korumalı</span>
                      ) : null}
                    </div>
                  </TD>
                )}
              </TR>
            ))
          )}
        </tbody>
      </Table>

      <Modal open={inviteOpen} onClose={() => setInviteOpen(false)} title="Üye davet et">
        <form onSubmit={sendInvite} className="space-y-4">
          {inviteErr && <Alert kind="error">{inviteErr}</Alert>}
          <FormField label="E-posta" hint="Kayıtlı bir kullanıcı olmalı">
            <Input type="email" value={invite.email} onChange={(e) => setInvite((v) => ({ ...v, email: e.target.value }))} required />
          </FormField>
          <FormField label="Rol" hint={isOwner ? undefined : "StudioAdmin yalnızca stüdyo sahibi tarafından atanır"}>
            <Select value={invite.role} onChange={(e) => setInvite((v) => ({ ...v, role: e.target.value as StudioRole }))}>
              {inviteRoleOptions.map((r) => <option key={r} value={r}>{r}</option>)}
            </Select>
          </FormField>
          <FormField label="Görünen ad (opsiyonel)">
            <Input value={invite.displayName} onChange={(e) => setInvite((v) => ({ ...v, displayName: e.target.value }))} />
          </FormField>
          {invite.role === "Instructor" && (
            <div className="grid grid-cols-2 gap-3">
              <FormField label="Ücret">
                <Input type="number" min={0} value={invite.rate} onChange={(e) => setInvite((v) => ({ ...v, rate: Number(e.target.value) }))} />
              </FormField>
              <FormField label="Ücret tipi">
                <Select value={invite.rateType} onChange={(e) => setInvite((v) => ({ ...v, rateType: e.target.value as RateType }))}>
                  {Object.entries(rateTypeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                </Select>
              </FormField>
            </div>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => setInviteOpen(false)}>İptal</Button>
            <Button type="submit" loading={saving}>Davet gönder</Button>
          </div>
        </form>
      </Modal>

      {assignFor && <AssignModal member={assignFor} onClose={() => setAssignFor(null)} />}
    </div>
  );
}

function AssignModal({ member, onClose }: { member: MemberDto; onClose: () => void }) {
  const [assigned, setAssigned] = useState<InstructorAssignmentDto[]>([]);
  const [allTypes, setAllTypes] = useState<ClassTypeDto[]>([]);
  const [selected, setSelected] = useState("");
  const [override, setOverride] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const profileId = member.instructorProfileId!;

  const load = useCallback(async () => {
    try {
      const [a, t] = await Promise.all([instructorService.classTypes(profileId), classTypeService.list()]);
      setAssigned(a);
      setAllTypes(t);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Yüklenemedi.");
    }
  }, [profileId]);

  useEffect(() => {
    load();
  }, [load]);

  const available = allTypes.filter((t) => !assigned.some((a) => a.classTypeId === t.id));

  const add = async () => {
    if (!selected) return;
    setBusy(true);
    setErr(null);
    try {
      const ov = override.trim() === "" ? null : Number(override);
      if (ov !== null && ov < 0) throw new Error("Ücret override negatif olamaz.");
      await instructorService.assign(profileId, { classTypeId: selected, rateOverride: ov });
      setSelected("");
      setOverride("");
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : e instanceof Error ? e.message : "Atanamadı.");
    } finally {
      setBusy(false);
    }
  };

  const editOverride = async (a: InstructorAssignmentDto) => {
    const input = prompt(
      `"${a.classTypeName}" için ücret override (boş = hoca ana ücreti):`,
      a.rateOverride != null ? String(a.rateOverride) : "",
    );
    if (input === null) return;
    const ov = input.trim() === "" ? null : Number(input);
    if (ov !== null && ov < 0) {
      setErr("Ücret override negatif olamaz.");
      return;
    }
    setBusy(true);
    try {
      await instructorService.updateAssignment(a.id, ov);
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Güncellenemedi.");
    } finally {
      setBusy(false);
    }
  };

  const drop = async (assignmentId: string) => {
    setBusy(true);
    try {
      await instructorService.unassign(assignmentId);
      await load();
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Kaldırılamadı.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open onClose={onClose} title={`${member.fullName} — Gruplar`}>
      <div className="space-y-4">
        {err && <Alert kind="error">{err}</Alert>}

        <div className="space-y-2">
          {assigned.length === 0 ? (
            <p className="text-sm text-muted">Henüz gruba atanmamış.</p>
          ) : (
            assigned.map((a) => (
              <div key={a.id} className="flex items-center justify-between rounded-xl border border-border px-3 py-2">
                <div>
                  <span className="text-sm text-foreground">{a.classTypeName}</span>
                  <span className="ml-2 text-xs text-muted">
                    {a.rateOverride != null ? `override: ${a.rateOverride}` : `ana ücret: ${member.rate ?? "—"}`}
                  </span>
                </div>
                <div className="flex gap-1">
                  <Button variant="ghost" size="sm" onClick={() => editOverride(a)} disabled={busy}>Ücret</Button>
                  <Button variant="ghost" size="sm" onClick={() => drop(a.id)} disabled={busy}>Kaldır</Button>
                </div>
              </div>
            ))
          )}
        </div>

        <div className="grid grid-cols-2 gap-2 border-t border-border pt-4">
          <FormField label="Gruba ekle">
            <Select value={selected} onChange={(e) => setSelected(e.target.value)}>
              <option value="">Grup seçin…</option>
              {available.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </Select>
          </FormField>
          <FormField label="Ücret override (ops.)" hint="Boş = hoca ana ücreti">
            <Input type="number" min={0} value={override} onChange={(e) => setOverride(e.target.value)} placeholder="—" />
          </FormField>
          <div className="col-span-2 flex justify-end">
            <Button onClick={add} disabled={!selected || busy}>Ekle</Button>
          </div>
        </div>
      </div>
    </Modal>
  );
}
