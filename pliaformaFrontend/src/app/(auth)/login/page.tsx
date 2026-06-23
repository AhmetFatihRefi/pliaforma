"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import type { AuthResponse } from "@/lib/types";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

export default function LoginPage() {
  const { login, applyAuth } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Sprint 31-MFA challenge adımı
  const [challengeToken, setChallengeToken] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [useRecovery, setUseRecovery] = useState(false);

  const routeAfterAuth = (auth: AuthResponse) => {
    if (auth.mfaSetupRequired) return router.replace("/mfa-setup");
    if (auth.user.globalRole === "PlatformAdmin" && !auth.activeStudio) router.replace("/platform");
    else if (auth.activeStudio) router.replace("/dashboard");
    else if (auth.memberships.length > 1) router.replace("/select-studio");
    else router.replace("/join-studio");
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const result = await login({ email, password });
      if (result.kind === "challenge") setChallengeToken(result.challengeToken);
      else routeAfterAuth(result.auth);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Giriş yapılamadı.");
    } finally {
      setLoading(false);
    }
  };

  const onVerifyMfa = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!challengeToken) return;
    setError(null);
    setLoading(true);
    try {
      const auth = useRecovery
        ? await authService.mfaRecoveryVerify(challengeToken, code)
        : await authService.mfaChallengeVerify(challengeToken, code);
      applyAuth(auth);
      routeAfterAuth(auth);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Doğrulama başarısız.");
    } finally {
      setLoading(false);
    }
  };

  if (challengeToken) {
    return (
      <Card>
        <h1 className="mb-1 text-xl font-semibold text-foreground">İki adımlı doğrulama</h1>
        <p className="mb-6 text-sm text-muted">
          {useRecovery ? "Kurtarma kodlarınızdan birini girin." : "Authenticator uygulamanızdaki 6 haneli kodu girin."}
        </p>
        <form onSubmit={onVerifyMfa} className="space-y-4">
          {error && <Alert kind="error">{error}</Alert>}
          <FormField label={useRecovery ? "Kurtarma kodu" : "Doğrulama kodu"} htmlFor="code">
            <Input id="code" inputMode={useRecovery ? "text" : "numeric"} autoComplete="one-time-code"
              value={code} onChange={(e) => setCode(e.target.value)} required autoFocus />
          </FormField>
          <Button type="submit" loading={loading} className="w-full">Doğrula</Button>
        </form>
        <button type="button" onClick={() => { setUseRecovery((v) => !v); setCode(""); setError(null); }}
          className="mt-4 w-full text-center text-sm text-muted hover:text-foreground">
          {useRecovery ? "Authenticator kodu kullan" : "Kurtarma kodu kullan"}
        </button>
      </Card>
    );
  }

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Tekrar hoş geldiniz</h1>
      <p className="mb-6 text-sm text-muted">Stüdyonuza giriş yapın.</p>

      <form onSubmit={onSubmit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}

        <FormField label="E-posta" htmlFor="email">
          <Input id="email" type="email" autoComplete="email" value={email}
            onChange={(e) => setEmail(e.target.value)} required />
        </FormField>

        <FormField label="Parola" htmlFor="password">
          <Input id="password" type="password" autoComplete="current-password" value={password}
            onChange={(e) => setPassword(e.target.value)} required />
        </FormField>

        <div className="text-right">
          <Link href="/forgot-password" className="text-sm text-muted hover:text-foreground">Şifremi unuttum</Link>
        </div>

        <Button type="submit" loading={loading} className="w-full">Giriş yap</Button>
      </form>

      <p className="mt-6 text-center text-sm text-muted">
        Hesabınız yok mu?{" "}
        <Link href="/register" className="font-medium text-primary hover:text-primary-hover">Hesap oluşturun</Link>
      </p>
      <p className="mt-2 text-center text-sm text-muted">
        Stüdyo sahibi misiniz?{" "}
        <Link href="/register-studio" className="font-medium text-primary hover:text-primary-hover">Stüdyo oluşturun</Link>
      </p>
    </Card>
  );
}
