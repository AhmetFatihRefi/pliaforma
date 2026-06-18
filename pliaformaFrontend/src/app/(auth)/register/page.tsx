"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

/**
 * Sprint 15: açık hesap oluşturma. Stüdyosuz bir Standard kullanıcı yaratır; ardından kullanıcı
 * /join-studio üzerinden bir stüdyoya öğrenci/hoca olarak katılım isteği gönderir.
 */
export default function RegisterPage() {
  const { register } = useAuth();
  const router = useRouter();
  const [form, setForm] = useState({ fullName: "", email: "", password: "", phone: "" });
  // Sprint 17 Rev2: kayıt niyeti — /join-studio ekranını belirler (öğrenci JoinCode formu vs. hoca davet bekleme).
  const [intent, setIntent] = useState<"Student" | "Instructor">("Student");
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
      await register({ ...form, phone: form.phone || undefined, intent });
      router.replace("/check-email"); // Sprint 16: önce e-posta doğrulama ekranı
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
        if (err.errors) setFieldErrors(err.errors);
      } else setError("Hesap oluşturulamadı.");
    } finally {
      setLoading(false);
    }
  };

  const fe = (name: string) => fieldErrors[name]?.[0];

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Hesap oluşturun</h1>
      <p className="mb-6 text-sm text-muted">Öğrenci veya hoca olarak bir stüdyoya katılmak için önce hesabınızı açın.</p>

      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}

        <FormField label="Nasıl katılacaksınız?" htmlFor="intent" hint="Hocalar stüdyo davetiyle, öğrenciler katılım koduyla katılır. Sonradan değişebilir.">
          <div className="grid grid-cols-2 gap-2" id="intent" role="radiogroup">
            {([["Student", "Öğrenci"], ["Instructor", "Hoca"]] as const).map(([val, label]) => (
              <button
                key={val}
                type="button"
                role="radio"
                aria-checked={intent === val}
                onClick={() => setIntent(val)}
                className={`rounded-xl border px-3 py-2 text-sm font-medium transition ${
                  intent === val
                    ? "border-primary bg-primary-soft text-primary"
                    : "border-border bg-surface text-muted hover:text-foreground"
                }`}
              >
                {label} olarak
              </button>
            ))}
          </div>
        </FormField>

        <FormField label="Ad soyad" htmlFor="fullName" error={fe("FullName")}>
          <Input id="fullName" value={form.fullName} onChange={set("fullName")} required />
        </FormField>

        <FormField label="E-posta" htmlFor="email" error={fe("Email")}>
          <Input id="email" type="email" autoComplete="email" value={form.email} onChange={set("email")} required />
        </FormField>

        <FormField label="Parola" htmlFor="password" hint="En az 8 karakter" error={fe("Password")}>
          <Input id="password" type="password" autoComplete="new-password" value={form.password} onChange={set("password")} required />
        </FormField>

        <FormField label="Telefon (opsiyonel)" htmlFor="phone" error={fe("Phone")}>
          <Input id="phone" value={form.phone} onChange={set("phone")} />
        </FormField>

        <Button type="submit" loading={loading} className="w-full">
          Hesap oluştur
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-muted">
        Zaten hesabınız var mı?{" "}
        <Link href="/login" className="font-medium text-primary hover:text-primary-hover">
          Giriş yapın
        </Link>
      </p>
      <p className="mt-2 text-center text-sm text-muted">
        Stüdyo sahibi misiniz?{" "}
        <Link href="/register-studio" className="font-medium text-primary hover:text-primary-hover">
          Stüdyo oluşturun
        </Link>
      </p>
    </Card>
  );
}
