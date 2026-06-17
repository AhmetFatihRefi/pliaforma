"use client";

import { useCallback, useEffect, useState } from "react";
import { availabilityService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useInstructors } from "@/lib/useInstructors";
import { type DayOfWeek, type InstructorAvailabilityDto, dayLabels, daysOfWeek } from "@/lib/domain-types";
import {
  Alert, Badge, Button, FormField, Input, Modal, Select, Table, TableEmpty, TD, TH, THead, TR,
} from "@/components/ui";

const hm = (t: string) => t.slice(0, 5); // "09:00:00" -> "09:00"
const toApiTime = (t: string) => (t.length === 5 ? `${t}:00` : t); // "09:00" -> "09:00:00"

export default function AvailabilityPage() {
  const { instructors, isManager } = useInstructors();
  const [instructorId, setInstructorId] = useState<string>("");
  const [items, setItems] = useState<InstructorAvailabilityDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<InstructorAvailabilityDto | null>(null);
  const [form, setForm] = useState({ dayOfWeek: "Monday" as DayOfWeek, startTime: "09:00", endTime: "12:00" });
  const [saving, setSaving] = useState(false);
  const [formErr, setFormErr] = useState<string | null>(null);

  useEffect(() => {
    if (!instructorId && instructors.length) setInstructorId(instructors[0].id);
  }, [instructors, instructorId]);

  const load = useCallback(async (id: string) => {
    setLoading(true);
    try {
      setItems(await availabilityService.list(isManager ? id : undefined));
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Müsaitlikler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [isManager]);

  useEffect(() => {
    if (instructorId) load(instructorId);
  }, [instructorId, load]);

  const openCreate = () => {
    setEditing(null);
    setForm({ dayOfWeek: "Monday", startTime: "09:00", endTime: "12:00" });
    setFormErr(null);
    setOpen(true);
  };
  const openEdit = (a: InstructorAvailabilityDto) => {
    setEditing(a);
    setForm({ dayOfWeek: a.dayOfWeek, startTime: hm(a.startTime), endTime: hm(a.endTime) });
    setFormErr(null);
    setOpen(true);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setFormErr(null);
    try {
      const payload = { dayOfWeek: form.dayOfWeek, startTime: toApiTime(form.startTime), endTime: toApiTime(form.endTime) };
      if (editing) await availabilityService.update(editing.id, { ...payload, isActive: editing.isActive });
      else await availabilityService.create(instructorId, payload);
      setOpen(false);
      await load(instructorId);
    } catch (err) {
      setFormErr(err instanceof ApiError ? err.message : "Kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  };

  const remove = async (a: InstructorAvailabilityDto) => {
    if (!confirm("Müsaitlik silinsin mi?")) return;
    try {
      await availabilityService.remove(a.id);
      await load(instructorId);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Silinemedi.");
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Müsaitlik</h1>
          <p className="mt-1 text-sm text-muted">Hocaların haftalık çalışma saatlerini tanımlayın.</p>
        </div>
        <Button onClick={openCreate} disabled={!instructorId}>+ Müsaitlik ekle</Button>
      </div>

      {isManager && (
        <FormField label="Hoca">
          <Select value={instructorId} onChange={(e) => setInstructorId(e.target.value)}>
            {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
          </Select>
        </FormField>
      )}

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR><TH>Gün</TH><TH>Saat</TH><TH>Durum</TH><TH className="text-right">İşlem</TH></TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={4}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={4}>Bu hoca için müsaitlik yok.</TableEmpty>
          ) : (
            items.map((a) => (
              <TR key={a.id}>
                <TD className="font-medium">{dayLabels[a.dayOfWeek]}</TD>
                <TD>{hm(a.startTime)} – {hm(a.endTime)}</TD>
                <TD>{a.isActive ? <Badge tone="success">Aktif</Badge> : <Badge tone="neutral">Pasif</Badge>}</TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(a)}>Düzenle</Button>
                    <Button variant="danger" size="sm" onClick={() => remove(a)}>Sil</Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      <Modal open={open} onClose={() => setOpen(false)} title={editing ? "Müsaitlik düzenle" : "Müsaitlik ekle"}>
        <form onSubmit={save} className="space-y-4">
          {formErr && <Alert kind="error">{formErr}</Alert>}
          <FormField label="Gün">
            <Select value={form.dayOfWeek} onChange={(e) => setForm((f) => ({ ...f, dayOfWeek: e.target.value as DayOfWeek }))}>
              {daysOfWeek.map((d) => <option key={d} value={d}>{dayLabels[d]}</option>)}
            </Select>
          </FormField>
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Başlangıç">
              <Input type="time" value={form.startTime} onChange={(e) => setForm((f) => ({ ...f, startTime: e.target.value }))} required />
            </FormField>
            <FormField label="Bitiş">
              <Input type="time" value={form.endTime} onChange={(e) => setForm((f) => ({ ...f, endTime: e.target.value }))} required />
            </FormField>
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>İptal</Button>
            <Button type="submit" loading={saving}>{editing ? "Kaydet" : "Ekle"}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
