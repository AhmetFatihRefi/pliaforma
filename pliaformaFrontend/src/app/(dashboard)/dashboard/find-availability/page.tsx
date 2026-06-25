"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { availabilityService, classTypeService } from "@/lib/studioApi";
import { errorMessage } from "@/lib/errors";
import { useRole } from "@/lib/useRole";
import { useInstructors } from "@/lib/useInstructors";
import type { AvailabilityWindowDto, ClassTypeDto } from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  FormField,
  Input,
  Select,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  TR,
  useToast,
} from "@/components/ui";

const todayStr = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};
const hhmm = (iso: string) => iso.slice(11, 16);
// Boş aralığın bitişi: en geç başlangıç + ders süresi (wall-clock dakika aritmetiği).
const hhmmPlus = (iso: string, min: number) => {
  const t = Number(iso.slice(11, 13)) * 60 + Number(iso.slice(14, 16)) + min;
  return `${String(Math.floor(t / 60)).padStart(2, "0")}:${String(t % 60).padStart(2, "0")}`;
};

export default function FindAvailabilityPage() {
  const toast = useToast();
  const router = useRouter();
  const { role } = useRole();
  const { instructors } = useInstructors();
  const canView = role === "Owner" || role === "StudioAdmin" || role === "Instructor";

  const [classTypes, setClassTypes] = useState<ClassTypeDto[]>([]);
  const [form, setForm] = useState({
    classTypeId: "",
    durationMinutes: 60,
    requiredCapacity: 1,
    from: todayStr(),
    to: todayStr(),
    instructorId: "",
  });
  const [windows, setWindows] = useState<AvailabilityWindowDto[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canView) return;
    classTypeService
      .list(true)
      .then((cts) => {
        setClassTypes(cts);
        setForm((f) => ({ ...f, classTypeId: f.classTypeId || cts[0]?.id || "", durationMinutes: cts[0]?.defaultDurationMinutes || 60 }));
      })
      .catch((e) => setError(errorMessage(e, "Ders tipleri yüklenemedi.")));
  }, [canView]);

  const selectedCt = useMemo(() => classTypes.find((c) => c.id === form.classTypeId), [classTypes, form.classTypeId]);

  const search = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.classTypeId) return;
    setLoading(true);
    setError(null);
    try {
      const res = await availabilityService.classTypeSlots({
        classTypeId: form.classTypeId,
        from: form.from,
        to: form.to,
        durationMinutes: Number(form.durationMinutes),
        requiredCapacity: Number(form.requiredCapacity),
        instructorId: form.instructorId || undefined,
        // Adım = ders süresi (mola dahil blok). Boşluklar ders-bloğu hizasında dilimlenir (temiz saatler).
        incrementMinutes: Number(form.durationMinutes),
      });
      setWindows(res);
    } catch (err) {
      setError(errorMessage(err, "Boşluk araması başarısız."));
      setWindows(null);
    } finally {
      setLoading(false);
    }
  };

  // "Bu saatte ders aç" → seans oluşturma için Ders Programı sayfasını ön-doldurulmuş aç.
  const openCreate = (w: AvailabilityWindowDto, instructorId?: string) => {
    const p = new URLSearchParams();
    p.set("newAt", w.startWindowFrom);
    p.set("classTypeId", form.classTypeId);
    if (instructorId) p.set("instructorId", instructorId);
    p.set("capacity", String(w.availableEquipmentCapacity ?? form.requiredCapacity));
    router.push(`/dashboard/schedule?${p.toString()}`);
  };

  if (!canView)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo ekibi içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Boşluk Bul</h1>
        <p className="mt-1 text-sm text-muted">
          Seçtiğiniz ders tipi, süre ve kapasiteye göre dersin açılabileceği uygun boş aralıkları ve müsait hocaları bulun.
        </p>
      </div>

      <Card>
        <form onSubmit={search} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label="Ders tipi">
              <Select
                value={form.classTypeId}
                onChange={(e) => {
                  const ct = classTypes.find((c) => c.id === e.target.value);
                  setForm((f) => ({ ...f, classTypeId: e.target.value, durationMinutes: ct?.defaultDurationMinutes || f.durationMinutes }));
                }}
              >
                {classTypes.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}{c.equipmentName ? ` · ${c.equipmentName}` : ""}</option>
                ))}
              </Select>
            </FormField>
            <FormField label="Hoca (opsiyonel)">
              <Select value={form.instructorId} onChange={(e) => setForm((f) => ({ ...f, instructorId: e.target.value }))}>
                <option value="">Tüm uygun hocalar</option>
                {instructors.map((i) => <option key={i.id} value={i.id}>{i.name}</option>)}
              </Select>
            </FormField>
          </div>
          <div className="grid gap-4 sm:grid-cols-4">
            <FormField label="Süre (dk)">
              <Input type="number" min={5} value={form.durationMinutes}
                onChange={(e) => setForm((f) => ({ ...f, durationMinutes: Number(e.target.value) }))} />
            </FormField>
            <FormField label="Kişi / kapasite">
              <Input type="number" min={1} value={form.requiredCapacity}
                onChange={(e) => setForm((f) => ({ ...f, requiredCapacity: Number(e.target.value) }))} />
            </FormField>
            <FormField label="Tarih (başlangıç)">
              <Input type="date" value={form.from} onChange={(e) => setForm((f) => ({ ...f, from: e.target.value, to: f.to < e.target.value ? e.target.value : f.to }))} />
            </FormField>
            <FormField label="Tarih (bitiş)">
              <Input type="date" value={form.to} min={form.from} onChange={(e) => setForm((f) => ({ ...f, to: e.target.value }))} />
            </FormField>
          </div>
          <div className="flex items-end justify-end gap-4">
            <Button type="submit" loading={loading}>Boşluk Ara</Button>
          </div>
        </form>
      </Card>

      <Alert kind="info">
        Uygun aralık, dersin sığabileceği <span className="font-medium">boş zaman aralığını</span> gösterir (ders süresi mola dahil blok olarak çalışır).
        Çalışma penceresi 07:00–23:00.
        {selectedCt && !selectedCt.equipmentId && " Bu ders tipi ekipmana bağlı değil; kapasite yalnız hoca müsaitliğiyle sınırlı."}
      </Alert>

      {error && <Alert kind="error">{error}</Alert>}

      {windows !== null && (
        <Table>
          <THead>
            <TR>
              <TH>Tarih</TH>
              <TH>Uygun aralık</TH>
              <TH>Süre</TH>
              <TH>Ekipman kapasitesi</TH>
              <TH>Müsait hocalar</TH>
              <TH className="text-right">İşlem</TH>
            </TR>
          </THead>
          <tbody>
            {loading ? (
              <TableEmpty colSpan={6}>Aranıyor…</TableEmpty>
            ) : windows.length === 0 ? (
              <TableEmpty colSpan={6}>Bu kriterlerde uygun boşluk bulunamadı.</TableEmpty>
            ) : (
              windows.map((w, idx) => (
                <TR key={idx}>
                  <TD>{w.date}</TD>
                  <TD>
                    <span className="font-medium">{hhmm(w.startWindowFrom)} – {hhmmPlus(w.startWindowTo, w.durationMinutes)}</span>
                  </TD>
                  <TD>{w.durationMinutes} dk</TD>
                  <TD>
                    {w.availableEquipmentCapacity == null
                      ? <span className="text-xs text-muted">ekipmansız</span>
                      : <Badge tone="rose">{w.availableEquipmentCapacity} kişi</Badge>}
                  </TD>
                  <TD>
                    <div className="flex flex-wrap gap-1">
                      {w.availableInstructors.map((i) => (
                        <Badge key={i.id} tone="neutral">{i.name}</Badge>
                      ))}
                    </div>
                  </TD>
                  <TD className="text-right">
                    <Button variant="ghost" size="sm" onClick={() => openCreate(w, w.availableInstructors[0]?.id)}>
                      Bu saatte ders aç →
                    </Button>
                  </TD>
                </TR>
              ))
            )}
          </tbody>
        </Table>
      )}
    </div>
  );
}
