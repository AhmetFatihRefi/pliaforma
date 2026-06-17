"use client";

import { useEffect, useState } from "react";
import { studioService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import type { StudioDto } from "@/lib/domain-types";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

type FormState = Omit<StudioDto, "id" | "slug" | "isActive">;

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
    </div>
  );
}
