"use client";

import { useEffect, useState } from "react";
import { studioService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import { dayLabels, type StudioDto, type WorkingHourDayInput } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input } from "@/components/ui";

type FormState = Omit<StudioDto, "id" | "slug" | "isActive">;

const hhmm = (t?: string | null) => (t ? t.slice(0, 5) : "");

// Sprint 37 — "Demo/yeni stüdyo" önerilen varsayılanı: Pzt–Cmt 09:00–22:00, Pazar kapalı.
// (Stüdyo henüz kaydetmemişse formda bu öneri gösterilir; sistem o ana dek 07:00–23:00 fallback uygular.)
const SUGGESTED_DAYS: WorkingHourDayInput[] = [
  { dayOfWeek: "Monday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Tuesday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Wednesday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Thursday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Friday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Saturday", isClosed: false, opensAt: "09:00", closesAt: "22:00" },
  { dayOfWeek: "Sunday", isClosed: true, opensAt: null, closesAt: null },
];

const WEEKDAYS_AFTER_MONDAY = ["Tuesday", "Wednesday", "Thursday", "Friday"] as const;

function WorkingHoursCard({ canManage }: { canManage: boolean }) {
  const [days, setDays] = useState<WorkingHourDayInput[] | null>(null);
  const [configured, setConfigured] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const fromDto = (ds: WorkingHourDayInput[]) =>
    ds.map((d) => ({ dayOfWeek: d.dayOfWeek, isClosed: d.isClosed, opensAt: hhmm(d.opensAt), closesAt: hhmm(d.closesAt) }));

  useEffect(() => {
    (async () => {
      try {
        const wh = await studioService.getWorkingHours();
        setConfigured(wh.isConfigured);
        setDays(wh.isConfigured ? fromDto(wh.days) : SUGGESTED_DAYS.map((d) => ({ ...d })));
      } catch (e) {
        setError(e instanceof ApiError ? e.message : "Çalışma saatleri yüklenemedi.");
      }
    })();
  }, []);

  const upd = (i: number, patch: Partial<WorkingHourDayInput>) =>
    setDays((ds) => (ds ? ds.map((d, j) => (j === i ? { ...d, ...patch } : d)) : ds));

  const copyMondayToWeekdays = () =>
    setDays((ds) => {
      if (!ds) return ds;
      const mon = ds[0];
      return ds.map((d) =>
        WEEKDAYS_AFTER_MONDAY.includes(d.dayOfWeek as (typeof WEEKDAYS_AFTER_MONDAY)[number])
          ? { ...d, isClosed: mon.isClosed, opensAt: mon.opensAt, closesAt: mon.closesAt }
          : d,
      );
    });

  const closeWeekend = () =>
    setDays((ds) => (ds ? ds.map((d) => (d.dayOfWeek === "Saturday" || d.dayOfWeek === "Sunday" ? { ...d, isClosed: true } : d)) : ds));

  const save = async () => {
    if (!days) return;
    for (const d of days) {
      if (d.isClosed) continue;
      if (!d.opensAt || !d.closesAt) { setError(`${dayLabels[d.dayOfWeek]}: açılış ve kapanış saati zorunlu.`); return; }
      if (d.opensAt >= d.closesAt) { setError(`${dayLabels[d.dayOfWeek]}: açılış saati kapanıştan önce olmalı (gece aşan saat desteklenmez).`); return; }
    }
    setSaving(true); setError(null); setOk(null);
    try {
      const payload: WorkingHourDayInput[] = days.map((d) => ({
        dayOfWeek: d.dayOfWeek,
        isClosed: d.isClosed,
        opensAt: d.isClosed ? null : d.opensAt,
        closesAt: d.isClosed ? null : d.closesAt,
      }));
      const res = await studioService.updateWorkingHours(payload);
      setConfigured(res.isConfigured);
      setDays(fromDto(res.days));
      setOk("Çalışma saatleri kaydedildi.");
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <div className="mb-3">
        <h2 className="text-lg font-semibold text-foreground">Çalışma Saatleri</h2>
        <p className="text-xs text-muted">
          Boşluk bulma, ders oluşturma, program üretimi ve yeniden planlama bu saatlere göre çalışır. Kapalı günlerde
          ders oluşturulamaz/üretilmez.
        </p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}
      {ok && <Alert kind="success">{ok}</Alert>}

      {!configured && (
        <Alert kind="info">
          Çalışma saatleri henüz ayarlanmadı; şu an varsayılan <b>07:00–23:00</b> uygulanıyor. Aşağıdaki önerilen
          saatleri gözden geçirip kaydedin.
        </Alert>
      )}

      {!days ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : (
        <div className="space-y-3">
          {canManage && (
            <div className="flex flex-wrap gap-2">
              <Button type="button" variant="secondary" size="sm" onClick={copyMondayToWeekdays}>
                Hafta içini Pazartesi gibi yap
              </Button>
              <Button type="button" variant="secondary" size="sm" onClick={closeWeekend}>
                Cumartesi/Pazar kapalı
              </Button>
            </div>
          )}

          <div className="space-y-2">
            {days.map((d, i) => (
              <div key={d.dayOfWeek} className="flex flex-wrap items-center gap-3 rounded-xl border border-border px-3 py-2">
                <span className="w-24 text-sm font-medium text-foreground">{dayLabels[d.dayOfWeek]}</span>
                <label className="inline-flex items-center gap-1.5 text-sm text-muted">
                  <input
                    type="checkbox"
                    checked={!d.isClosed}
                    disabled={!canManage}
                    onChange={(e) => upd(i, { isClosed: !e.target.checked })}
                  />
                  Açık
                </label>
                {d.isClosed ? (
                  <Badge tone="neutral">Kapalı</Badge>
                ) : (
                  <div className="flex items-center gap-2">
                    <Input
                      type="time"
                      step={300}
                      value={d.opensAt ?? ""}
                      disabled={!canManage}
                      onChange={(e) => upd(i, { opensAt: e.target.value })}
                      className="w-32"
                    />
                    <span className="text-muted">–</span>
                    <Input
                      type="time"
                      step={300}
                      value={d.closesAt ?? ""}
                      disabled={!canManage}
                      onChange={(e) => upd(i, { closesAt: e.target.value })}
                      className="w-32"
                    />
                  </div>
                )}
              </div>
            ))}
          </div>

          {canManage && (
            <div className="flex justify-end pt-1">
              <Button type="button" loading={saving} onClick={save}>Çalışma saatlerini kaydet</Button>
            </div>
          )}
        </div>
      )}
    </Card>
  );
}

export default function SettingsPage() {
  const { canManage } = useRole();
  const [studio, setStudio] = useState<StudioDto | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [ok, setOk] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const s = await studioService.getMine();
        setStudio(s);
        setForm({
          name: s.name,
          timeZoneId: s.timeZoneId,
          currency: s.currency,
          contactEmail: s.contactEmail ?? "",
          contactPhone: s.contactPhone ?? "",
          addressLine: s.addressLine ?? "",
          city: s.city ?? "",
        });
      } catch (e) {
        setError(e instanceof ApiError ? e.message : "Stüdyo yüklenemedi.");
      }
    })();
  }, []);

  const set = (k: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => (f ? { ...f, [k]: e.target.value } : f));

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setSaving(true);
    setError(null);
    setOk(null);
    try {
      const updated = await studioService.update(form);
      setStudio(updated);
      setOk("Stüdyo bilgileri kaydedildi.");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kaydedilemedi.");
    } finally {
      setSaving(false);
    }
  };

  if (!form || !studio) {
    return <p className="text-sm text-muted">{error ?? "Yükleniyor…"}</p>;
  }

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Stüdyo Ayarları</h1>
        <p className="mt-1 text-sm text-muted">
          <span className="font-mono">{studio.slug}</span>
        </p>
      </div>

      <Card>
        <form onSubmit={save} className="space-y-4">
          {error && <Alert kind="error">{error}</Alert>}
          {ok && <Alert kind="success">{ok}</Alert>}

          <FormField label="Stüdyo adı">
            <Input value={form.name} onChange={set("name")} disabled={!canManage} required />
          </FormField>
          <div className="grid grid-cols-2 gap-3">
            <FormField label="Zaman dilimi">
              <Input value={form.timeZoneId} onChange={set("timeZoneId")} disabled={!canManage} />
            </FormField>
            <FormField label="Para birimi" hint="3 harf (ISO 4217)">
              <Input value={form.currency} onChange={set("currency")} disabled={!canManage} maxLength={3} />
            </FormField>
            <FormField label="İletişim e-posta">
              <Input type="email" value={form.contactEmail ?? ""} onChange={set("contactEmail")} disabled={!canManage} />
            </FormField>
            <FormField label="İletişim telefon">
              <Input value={form.contactPhone ?? ""} onChange={set("contactPhone")} disabled={!canManage} />
            </FormField>
            <FormField label="Adres">
              <Input value={form.addressLine ?? ""} onChange={set("addressLine")} disabled={!canManage} />
            </FormField>
            <FormField label="Şehir">
              <Input value={form.city ?? ""} onChange={set("city")} disabled={!canManage} />
            </FormField>
          </div>

          {canManage && (
            <div className="flex justify-end pt-2">
              <Button type="submit" loading={saving}>Kaydet</Button>
            </div>
          )}
        </form>
      </Card>

      {/* Sprint 37: Çalışma saatleri (haftalık). */}
      <WorkingHoursCard canManage={canManage} />
      {/* Sprint 17 Rev3: JoinCode kartı kaldırıldı — onboarding artık invite-only (kodla katılım yok). */}
    </div>
  );
}
