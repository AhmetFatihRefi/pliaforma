"use client";

import { useEffect, useState } from "react";
import { equipmentService } from "@/lib/studioApi";
import { errorMessage } from "@/lib/errors";
import { useRole } from "@/lib/useRole";
import type { EquipmentDto, EquipmentInput } from "@/lib/domain-types";
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
  useToast,
} from "@/components/ui";

// Sprint 32A: Ekipman havuzu. TotalCapacity = quantity * perUnitConcurrentUsage. Enforcement 32B'de gelir.
const empty: EquipmentInput = {
  name: "",
  description: "",
  quantity: 1,
  perUnitConcurrentUsage: 1,
  isActive: true,
};

export default function EquipmentPage() {
  const confirm = useConfirm();
  const toast = useToast();
  const { canManage } = useRole();
  const [items, setItems] = useState<EquipmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [open, setOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<EquipmentInput>(empty);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      setItems(await equipmentService.list());
      setError(null);
    } catch (e) {
      setError(errorMessage(e, "Liste yüklenemedi."));
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

  const openEdit = (e: EquipmentDto) => {
    setEditingId(e.id);
    setForm({
      name: e.name,
      description: e.description ?? "",
      quantity: e.quantity,
      perUnitConcurrentUsage: e.perUnitConcurrentUsage,
      isActive: e.isActive,
    });
    setFormError(null);
    setOpen(true);
  };

  const save = async (ev: React.FormEvent) => {
    ev.preventDefault();
    setSaving(true);
    setFormError(null);
    try {
      if (editingId) await equipmentService.update(editingId, form);
      else await equipmentService.create(form);
      toast.success(editingId ? "Ekipman güncellendi." : "Ekipman oluşturuldu.");
      setOpen(false);
      await load();
    } catch (err) {
      setFormError(errorMessage(err, "Kaydedilemedi."));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (e: EquipmentDto) => {
    if (e.boundClassTypeCount > 0) {
      toast.error(
        `"${e.name}" ${e.boundClassTypeCount} ders tipine bağlı; silinemez. Önce bağlantıları kaldırın veya pasifleştirin.`,
      );
      return;
    }
    if (!(await confirm({ title: "Ekipmanı sil", tone: "danger", confirmLabel: "Sil", message: `"${e.name}" ekipmanı silinecek.` }))) return;
    try {
      await equipmentService.remove(e.id);
      toast.success("Ekipman silindi.");
      await load();
    } catch (err) {
      toast.error(errorMessage(err, "Silinemedi."));
    }
  };

  const previewTotal = (form.quantity || 0) * (form.perUnitConcurrentUsage || 0);

  // Ders tipi/grup yönetimiyle aynı kapı: yalnız stüdyo yöneticisi.
  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Ekipman / Envanter</h1>
          <p className="mt-1 text-sm text-muted">
            Stüdyonuzun ekipman havuzlarını tanımlayın (reformer, mat, vb.). Bir ders tipini bir havuza bağlayabilirsiniz.
          </p>
        </div>
        <Button onClick={openCreate}>+ Yeni ekipman</Button>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      <Alert kind="info">
        <span className="font-medium">Toplam kapasite = Adet × Aynı anda kullanım.</span>{" "}
        Örn: 4 reformer × 1 = 4 kişi. Kapasite kontrolü sonraki aşamada (ders oluştururken) devreye girecek.
      </Alert>

      <Table>
        <THead>
          <TR>
            <TH>Ad</TH>
            <TH>Adet</TH>
            <TH>Aynı anda / adet</TH>
            <TH>Toplam kapasite</TH>
            <TH>Bağlı grup</TH>
            <TH>Durum</TH>
            <TH className="text-right">İşlem</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={7}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={7}>Henüz ekipman tanımlanmadı.</TableEmpty>
          ) : (
            items.map((e) => (
              <TR key={e.id}>
                <TD>
                  <span className="font-medium">{e.name}</span>
                  {e.description && <span className="ml-2 text-xs text-muted">{e.description}</span>}
                </TD>
                <TD>{e.quantity}</TD>
                <TD>{e.perUnitConcurrentUsage}</TD>
                <TD><span className="font-semibold">{e.totalCapacity}</span> kişi</TD>
                <TD>{e.boundClassTypeCount > 0 ? <Badge tone="rose">{e.boundClassTypeCount} grup</Badge> : <span className="text-xs text-muted">—</span>}</TD>
                <TD>{e.isActive ? <Badge tone="success">Aktif</Badge> : <Badge tone="neutral">Pasif</Badge>}</TD>
                <TD className="text-right">
                  <div className="flex justify-end gap-2">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(e)}>Düzenle</Button>
                    <Button variant="danger" size="sm" onClick={() => remove(e)}>Sil</Button>
                  </div>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      <Modal open={open} onClose={() => setOpen(false)} title={editingId ? "Ekipmanı düzenle" : "Yeni ekipman"}>
        <form onSubmit={save} className="space-y-4">
          {formError && <Alert kind="error">{formError}</Alert>}
          <FormField label="Ad" htmlFor="eq-name">
            <Input id="eq-name" value={form.name} onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))} required placeholder="Örn: Reformer" />
          </FormField>
          <FormField label="Açıklama (opsiyonel)">
            <Textarea value={form.description ?? ""} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
          </FormField>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Adet" hint="Bu ekipmandan kaç adet var?">
              <Input type="number" min={0} value={form.quantity}
                onChange={(e) => setForm((f) => ({ ...f, quantity: Number(e.target.value) }))} />
            </FormField>
            <FormField label="Aynı anda / adet" hint="1 ekipmanı aynı anda kaç kişi kullanır? (Reformer/Mat = 1)">
              <Input type="number" min={1} value={form.perUnitConcurrentUsage}
                onChange={(e) => setForm((f) => ({ ...f, perUnitConcurrentUsage: Number(e.target.value) }))} />
            </FormField>
          </div>
          <div className="rounded-xl border border-border bg-surface-muted px-4 py-3 text-sm">
            Toplam kapasite: <span className="font-semibold text-foreground">{previewTotal} kişi</span>
            <span className="ml-1 text-xs text-muted">({form.quantity || 0} × {form.perUnitConcurrentUsage || 0})</span>
          </div>
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
