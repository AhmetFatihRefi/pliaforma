"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

export default function RegisterStudioPage() {
  const { registerStudio } = useAuth();
  const router = useRouter();
  const [form, setForm] = useState({ studioName: "", ownerFullName: "", email: "", password: "", phone: "" });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [loading, setLoading] = useState(false);

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [k]: e.target.value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setFieldErrors({});
    setLoading(true);
    try {
      const auth = await registerStudio({ ...form, phone: form.phone || undefined });
      if (auth.mfaSetupRequired) router.replace("/mfa-setup"); // Sprint 31-MFA: yeni Owner MFA kurmalı
      else router.replace("/dashboard");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        if (err.errors) setFieldErrors(err.errors);
      } else setError("Stüdyo oluşturulamadı.");
    } finally {
      setLoading(false);
    }
  };

  const fe = (name: string) => fieldErrors[name]?.[0];

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Stüdyonuzu oluşturun</h1>
      <p className="mb-6 text-sm text-muted">Birkaç adımda Pliaforma&apos;ya başlayın.</p>

      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}

        <FormField label="Stüdyo adı" htmlFor="studioName" error={fe("StudioName")}>
          <Input id="studioName" value={form.studioName} onChange={set("studioName")} required />
        </FormField>

        <FormField label="Ad soyad" htmlFor="ownerFullName" error={fe("OwnerFullName")}>
          <Input id="ownerFullName" value={form.ownerFullName} onChange={set("ownerFullName")} required />
        </FormField>

        <FormField label="E-posta" htmlFor="email" error={fe("Email")}>
          <Input id="email" type="email" autoComplete="email" value={form.email} onChange={set("email")} required />
        </FormField>

        <FormField label="Parola" htmlFor="password" hint="En az 10 karakter; büyük/küçük harf, rakam ve özel karakter" error={fe("Password")}>
          <Input
            id="password"
            type="password"
            autoComplete="new-password"
            value={form.password}
            onChange={set("password")}
            required
          />
        </FormField>

        <FormField label="Telefon (opsiyonel)" htmlFor="phone" error={fe("Phone")}>
          <Input id="phone" value={form.phone} onChange={set("phone")} />
        </FormField>

        <Button type="submit" loading={loading} className="w-full">
          Stüdyo oluştur
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-muted">
        Zaten hesabınız var mı?{" "}
        <Link href="/login" className="font-medium text-primary hover:text-primary-hover">
          Giriş yapın
        </Link>
      </p>
    </Card>
  );
}
