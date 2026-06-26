"use client";

import { useEffect, useState } from "react";
import { classTypeService, equipmentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import { type ClassTypeDto, type ClassTypeInput, type EquipmentDto } from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  FormField,
  Input,
  Modal,
  Select,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  Textarea,
  TR,
  useConfirm,
  useToast,
} from "@/components/ui";

// Sprint 28A: ders tipi sadeleşti → ad + süre + açıklama. Sprint 32A: opsiyonel ekipman havuzu bağı (equipmentId).
const empty: ClassTypeInput = {
  name: "",
  defaultDurationMinutes: 50,
  description: "",
  isActive: true,
  equipmentId: null,
};

export default function ClassTypesPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const { canManage } = useRole();
  const [items, setItems] = useState<ClassTypeDto[]>([]);
  const [equipments, setEquipments] = useState<EquipmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ClassTypeInput>(empty);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      // Sprint 32A: ders tipi listesi + (form seçici için) aktif ekipman havuzları paralel yüklenir.
      const [types, eqs] = await Promise.all([classTypeService.list(), equipmentService.list(true)]);
      setItems(types);
      setEquipments(eqs);
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Liste yüklenemedi.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  const openCreate = () => {
    setEditingId(null);
    setForm(empty);
    setFormError(null);
    setOpen(true);
  };

  const openEdit = (c: ClassTypeDto) => {
    setEditingId(c.id);
    setForm({ name: c.name, defaultDurationMinutes: c.defaultDurationMinutes, description: c.description ?? "", isActive: c.isActive, equipmentId: c.equipmentId ?? null });
    setFormError(null);
    setOpen(true);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setFormError(null);
    try {
      const isEdit = !!editingId;
      if (editingId) await classTypeService.update(editingId, form);
      else await classTypeService.create(form);
      setOpen(false);
      await load();
      toast.success(isEdit ? "Grup güncellendi." : "Grup oluşturuldu.");
    } catch (err) {
      setFormError(err instanceof ApiError ? err.message : "Kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  };

  const remove = async (c: ClassTypeDto) => {
    if (!(await confirm({ title: "Grubu sil", tone: "danger", confirmLabel: "Sil", message: `"${c.name}" ders grubu silinecek.` }))) return;
    try {
      await classTypeService.remove(c.id);
      await load();
      toast.success("Grup silindi.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Silinemedi.");
    }
  };

  // Sprint 26 ek (v1.1.1): ders tipi/grup yönetimi yalnız stüdyo yetkilisi (Instructor doğrudan URL ile giremez).
  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Pilates Grupları</h1>
          <p className="mt-1 text-sm text-muted">Stüdyonuzun ders tiplerini tanımlayın (ad + süre).</p>
        </div>
        <Button onClick={openCreate}>+ Yeni grup</Button>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>Ad</TH>
            <TH>Süre</TH>
            <TH>Ekipman</TH>
            <TH>Durum</TH>
            <TH className="text-right">İşlem</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={5}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={5}>Henüz pilates grubu yok.</TableEmpty>
          ) : (
            items.map((c) => (
              <TR key={c.id}>
                <TD>
                  <span className="font-medium">{c.name}</span>
                  {c.description && <span className="ml-2 text-xs text-muted">{c.description}</span>}
                </TD>
                <TD>{c.defaultDurationMinutes} dk</TD>
                <TD>{c.equipmentName ? <Badge tone="rose">{c.equipmentName}</Badge> : <span className="text-xs text-muted">—</span>}</TD>
                <TD>{c.isActive ? <Badge tone="success">Aktif</Badge> : <Badge tone="neutral">Pasif</Badge>}</TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(c)}>Düzenle</Button>
                    <Button variant="danger" size="sm" onClick={() => remove(c)}>Sil</Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      <Modal open={open} onClose={() => setOpen(false)} title={editingId ? "Grubu düzenle" : "Yeni grup"}>
        <form onSubmit={save} className="space-y-4">
          {formError && <Alert kind="error">{formError}</Alert>}
          <FormField label="Ad" htmlFor="name">
            <Input id="name" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} required />
          </FormField>
          <FormField label="Süre (dk)">
            <Input type="number" min={10} max={240} value={form.defaultDurationMinutes}
              onChange={(e) => setForm((f) => ({ ...f, defaultDurationMinutes: Number(e.target.value) }))} />
          </FormField>
          <FormField label="Açıklama (opsiyonel)">
            <Textarea value={form.description ?? ""} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
          </FormField>
          <FormField label="Ekipman (opsiyonel)" hint="Bu grubu bir ekipman havuzuna bağlayın.">
            <Select
              value={form.equipmentId ?? ""}
              onChange={(e) => setForm((f) => ({ ...f, equipmentId: e.target.value || null }))}
            >
              <option value="">Ekipman yok</option>
              {equipments.map((eq) => (
                <option key={eq.id} value={eq.id}>{eq.name} ({eq.totalCapacity} kişi)</option>
              ))}
            </Select>
          </FormField>
          {editingId && (
            <label className="flex items-center gap-2 text-sm text-foreground">
              <input type="checkbox" checked={form.isActive ?? true} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
              Aktif
            </label>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>Vazgeç</Button>
            <Button type="submit" loading={saving}>{editingId ? "Kaydet" : "Oluştur"}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
