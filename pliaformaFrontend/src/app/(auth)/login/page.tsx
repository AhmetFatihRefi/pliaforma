"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

export default function LoginPage() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const auth = await login({ email, password });
      if (auth.user.globalRole === "PlatformAdmin" && !auth.activeStudio) router.replace("/platform");
      else if (auth.activeStudio) router.replace("/dashboard");
      else if (auth.memberships.length > 1) router.replace("/select-studio");
      else router.replace("/join-studio"); // aktif stüdyo üyeliği yok → katılım ekranı (Sprint 15)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Giriş yapılamadı.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Tekrar hoş geldiniz</h1>
      <p className="mb-6 text-sm text-muted">Stüdyonuza giriş yapın.</p>

      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}

        <FormField label="E-posta" htmlFor="email">
          <Input
            id="email"
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </FormField>

        <FormField label="Parola" htmlFor="password">
          <Input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </FormField>

        <Button type="submit" loading={loading} className="w-full">
          Giriş yap
        </Button>
      </form>

      <p className="mt-6 text-center text-sm text-muted">
        Hesabınız yok mu?{" "}
        <Link href="/register" className="font-medium text-primary hover:text-primary-hover">
          Hesap oluşturun
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
