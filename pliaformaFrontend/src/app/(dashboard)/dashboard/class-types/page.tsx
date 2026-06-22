"use client";

import { useEffect, useState } from "react";
import { classTypeService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import { type ClassTypeDto, type ClassTypeInput } from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  FormField,
  Input,
  Modal,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  Textarea,
  TR,
  useConfirm,
} from "@/components/ui";

// Sprint 28A: ders tipi sadeleşti → yalnız ad + süre + açıklama (ekipman/kapasite/seviye/amaç kaldırıldı).
const empty: ClassTypeInput = {
  name: "",
  defaultDurationMinutes: 50,
  description: "",
  isActive: true,
};

export default function ClassTypesPage() {
  const confirm = useConfirm();
  const { canManage } = useRole();
  const [items, setItems] = useState<ClassTypeDto[]>([]);
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
      setItems(await classTypeService.list());
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
    setForm({ name: c.name, defaultDurationMinutes: c.defaultDurationMinutes, description: c.description ?? "", isActive: c.isActive });
    setFormError(null);
    setOpen(true);
  };

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setFormError(null);
    try {
      if (editingId) await classTypeService.update(editingId, form);
      else await classTypeService.create(form);
      setOpen(false);
      await load();
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
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Silinemedi.");
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
            <TH>Durum</TH>
            <TH className="text-right">İşlem</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={4}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={4}>Henüz pilates grubu yok.</TableEmpty>
          ) : (
            items.map((c) => (
              <TR key={c.id}>
                <TD>
                  <span className="font-medium">{c.name}</span>
                  {c.description && <span className="ml-2 text-xs text-muted">{c.description}</span>}
                </TD>
                <TD>{c.defaultDurationMinutes} dk</TD>
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
          {editingId && (
            <label className="flex items-center gap-2 text-sm text-foreground">
              <input type="checkbox" checked={form.isActive ?? true} onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))} />
              Aktif
            </label>
          )}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => setOpen(false)}>İptal</Button>
            <Button type="submit" loading={saving}>{editingId ? "Kaydet" : "Oluştur"}</Button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
