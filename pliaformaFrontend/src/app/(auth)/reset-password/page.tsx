"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

/** Sprint 16: şifre sıfırlama. Linkteki token + yeni şifre ile /api/auth/reset-password. */
export default function ResetPasswordPage() {
  const [token, setToken] = useState<string | null>(null);
  const [password, setPassword] = useState("");
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setToken(new URLSearchParams(window.location.search).get("token"));
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token) {
      setError("Geçersiz veya eksik sıfırlama bağlantısı.");
      return;
    }
    setError(null);
    setLoading(true);
    try {
      await authService.resetPassword(token, password);
      setDone(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Şifre güncellenemedi.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Şifre sıfırla</h1>

      {done ? (
        <div className="space-y-4">
          <Alert kind="success">Şifreniz güncellendi. Lütfen yeni şifrenizle giriş yapın.</Alert>
          <Link href="/login"><Button className="w-full">Giriş yap</Button></Link>
        </div>
      ) : (
        <form onSubmit={onSubmit} className="space-y-4">
          <p className="text-sm text-muted">Yeni şifrenizi belirleyin.</p>
          {error && <Alert kind="error">{error}</Alert>}
          {token === null && <Alert kind="warning">Bağlantıda doğrulama kodu bulunamadı. Lütfen e-postanızdaki bağlantıyı kullanın.</Alert>}
          <FormField label="Yeni şifre" htmlFor="password" hint="En az 8 karakter">
            <Input id="password" type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </FormField>
          <Button type="submit" loading={loading} className="w-full">Şifreyi güncelle</Button>
        </form>
      )}

      <p className="mt-6 text-center text-sm text-muted">
        <Link href="/login" className="font-medium text-primary hover:text-primary-hover">Giriş yap</Link>
      </p>
    </Card>
  );
}
