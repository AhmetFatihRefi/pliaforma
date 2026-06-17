"use client";

import { useCallback, useEffect, useState } from "react";
import { studioService, memberService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import type { StudioDto, JoinCodeDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input } from "@/components/ui";

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

      {canManage && <JoinCodeCard />}
    </div>
  );
}

/** Sprint 15: stüdyo katılım kodu — öğrenci/hoca bu kodla katılım isteği gönderir. */
function JoinCodeCard() {
  const [code, setCode] = useState<JoinCodeDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);

  const load = useCallback(async () => {
    try {
      setCode(await memberService.joinCode());
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Katılım kodu yüklenemedi.");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const regenerate = async () => {
    if (!confirm("Yeni kod üretilsin mi? Eski kod artık çalışmayacak.")) return;
    setBusy(true);
    setError(null);
    try {
      setCode(await memberService.regenerateJoinCode());
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Kod yenilenemedi.");
    } finally {
      setBusy(false);
    }
  };

  const copy = async () => {
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code.joinCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* clipboard yoksa sessiz geç */
    }
  };

  return (
    <Card>
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-sm font-semibold text-foreground">Katılım kodu</h2>
        {code && <Badge tone={code.isActive ? "success" : "neutral"}>{code.isActive ? "Aktif" : "Pasif"}</Badge>}
      </div>
      <p className="mb-4 text-sm text-muted">
        Öğrenciler ve hocalar bu kodu girip stüdyonuza katılım isteği gönderir. İsteği siz onaylarsınız.
      </p>
      {error && <Alert kind="error">{error}</Alert>}
      {code && (
        <div className="flex flex-wrap items-center gap-3">
          <span className="rounded-xl border border-border bg-surface-muted px-4 py-2 font-mono text-lg tracking-widest text-foreground">
            {code.formattedJoinCode || "—"}
          </span>
          <Button variant="secondary" size="sm" onClick={copy}>{copied ? "Kopyalandı" : "Kopyala"}</Button>
          <Button variant="ghost" size="sm" loading={busy} onClick={regenerate}>Kodu yenile</Button>
        </div>
      )}
    </Card>
  );
}
