"use client";

import { useEffect, useState } from "react";
import { classTypeService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type ClassTypeDto,
  type ClassTypeInput,
  capacityLabels,
  equipmentLabels,
  levelLabels,
  purposeLabels,
} from "@/lib/domain-types";
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
} from "@/components/ui";

const empty: ClassTypeInput = {
  name: "",
  equipment: "Reformer",
  capacityType: "Group",
  maxCapacity: 6,
  level: "AllLevels",
  purpose: "General",
  defaultDurationMinutes: 50,
  description: "",
  isActive: true,
};

export default function ClassTypesPage() {
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
    setForm({ ...c, description: c.description ?? "" });
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
    if (!confirm(`"${c.name}" grubunu silmek istediğinize emin misiniz?`)) return;
    try {
      await classTypeService.remove(c.id);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Silinemedi.");
    }
  };

  const num = (k: "maxCapacity" | "defaultDurationMinutes") => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: Number(e.target.value) }));

  // Sprint 26 ek (v1.1.1): ders tipi/grup yönetimi yalnız stüdyo yetkilisi (Instructor doğrudan URL ile giremez).
  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Pilates Grupları</h1>
          {/* Sprint 26 ek: ders tipi tanımı yalnız stüdyo yetkilisinin işi → "tanımlayın" yönlendirmesi yalnız yöneticilere. */}
          {canManage && <p className="mt-1 text-sm text-muted">Stüdyonuzun ders tiplerini tanımlayın.</p>}
        </div>
        {canManage && <Button onClick={openCreate}>+ Yeni grup</Button>}
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>Ad</TH>
            <TH>Ekipman</TH>
            <TH>Kapasite</TH>
            <TH>Seviye</TH>
            <TH>Süre</TH>
            <TH>Durum</TH>
            {canManage && <TH className="text-right">İşlem</TH>}
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={canManage ? 7 : 6}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={canManage ? 7 : 6}>Henüz pilates grubu yok.</TableEmpty>
          ) : (
            items.map((c) => (
              <TR key={c.id}>
                <TD>
                  <span className="font-medium">{c.name}</span>
                  <span className="ml-2 text-xs text-muted">{purposeLabels[c.purpose]}</span>
                </TD>
                <TD>{equipmentLabels[c.equipment]}</TD>
                <TD>
                  {capacityLabels[c.capacityType]} · {c.maxCapacity}
                </TD>
                <TD>{levelLabels[c.level]}</TD>
                <TD>{c.defaultDurationMinutes} dk</TD>
                <TD>{c.isActive ? <Badge tone="success">Aktif</Badge> : <Badge tone="neutral">Pasif</Badge>}</TD>
                {canManage && (
                  <TD className="text-right">
                    <div className="flex justify-end gap-2">
                      <Button variant="ghost" size="sm" onClick={() => openEdit(c)}>
                        Düzenle
                      </Button>
                      <Button variant="danger" size="sm" onClick={() => remove(c)}>
                        Sil
                      </Button>
                    </div>
                  </TD>
                )}
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
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Ekipman">
              <Select value={form.equipment} onChange={(e) => setForm((f) => ({ ...f, equipment: e.target.value as ClassTypeInput["equipment"] }))}>
                {Object.entries(equipmentLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </Select>
            </FormField>
            <FormField label="Kapasite tipi">
              <Select value={form.capacityType} onChange={(e) => setForm((f) => ({ ...f, capacityType: e.target.value as ClassTypeInput["capacityType"] }))}>
                {Object.entries(capacityLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </Select>
            </FormField>
            <FormField label="Maks. kapasite" hint="Private⇒1, Duo⇒2, Group⇒2..50">
              <Input type="number" min={1} value={form.maxCapacity} onChange={num("maxCapacity")} />
            </FormField>
            <FormField label="Süre (dk)">
              <Input type="number" min={10} max={240} value={form.defaultDurationMinutes} onChange={num("defaultDurationMinutes")} />
            </FormField>
            <FormField label="Seviye">
              <Select value={form.level} onChange={(e) => setForm((f) => ({ ...f, level: e.target.value as ClassTypeInput["level"] }))}>
                {Object.entries(levelLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </Select>
            </FormField>
            <FormField label="Amaç">
              <Select value={form.purpose} onChange={(e) => setForm((f) => ({ ...f, purpose: e.target.value as ClassTypeInput["purpose"] }))}>
                {Object.entries(purposeLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </Select>
            </FormField>
          </div>
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
