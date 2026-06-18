"use client";

import { useState } from "react";
import Link from "next/link";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

/** Sprint 16: şifremi unuttum. Her durumda generic başarı (kullanıcı var/yok sızdırılmaz). */
export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await authService.forgotPassword(email);
      setSent(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "İşlem başarısız.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Şifremi unuttum</h1>
      <p className="mb-6 text-sm text-muted">E-posta adresinizi girin; hesabınız varsa sıfırlama bağlantısı gönderilir.</p>

      {sent ? (
        <div className="space-y-4">
          <Alert kind="success">Eğer bu e-posta ile kayıt varsa şifre sıfırlama bağlantısı gönderildi. Lütfen gelen kutunuzu kontrol edin.</Alert>
          <Link href="/login"><Button variant="secondary" className="w-full">Giriş ekranına dön</Button></Link>
        </div>
      ) : (
        <form onSubmit={onSubmit} className="space-y-4">
          {error && <Alert kind="error">{error}</Alert>}
          <FormField label="E-posta" htmlFor="email">
            <Input id="email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </FormField>
          <Button type="submit" loading={loading} className="w-full">Sıfırlama bağlantısı gönder</Button>
        </form>
      )}

      <p className="mt-6 text-center text-sm text-muted">
        <Link href="/login" className="font-medium text-primary hover:text-primary-hover">Giriş yap</Link>
      </p>
    </Card>
  );
}
