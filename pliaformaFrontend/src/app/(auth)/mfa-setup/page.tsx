"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { QRCodeSVG } from "qrcode.react";
import { useAuth } from "@/lib/auth";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import type { AuthResponse } from "@/lib/types";
import { Alert, Button, Card, FormField, Input } from "@/components/ui";

export default function MfaSetupPage() {
  const { user, loading, applyAuth } = useAuth();
  const router = useRouter();
  const [otpauthUri, setOtpauthUri] = useState<string | null>(null);
  const [manualKey, setManualKey] = useState("");
  const [code, setCode] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [pendingAuth, setPendingAuth] = useState<AuthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!loading && !user) router.replace("/login");
  }, [loading, user, router]);

  useEffect(() => {
    if (loading || !user) return;
    (async () => {
      try {
        const r = await authService.mfaSetupStart();
        setOtpauthUri(r.otpauthUri);
        setManualKey(r.manualKey);
      } catch (e) {
        setError(e instanceof ApiError ? e.message : "MFA kurulumu başlatılamadı.");
      }
    })();
  }, [loading, user]);

  const verify = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const r = await authService.mfaSetupVerify(code);
      setRecoveryCodes(r.recoveryCodes);
      setPendingAuth(r.auth);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Doğrulama başarısız.");
    } finally {
      setBusy(false);
    }
  };

  const finish = () => {
    if (pendingAuth) applyAuth(pendingAuth);
    router.replace("/dashboard");
  };

  if (loading || !user) return null;

  if (recoveryCodes) {
    return (
      <Card>
        <h1 className="mb-1 text-xl font-semibold text-foreground">Kurtarma kodları</h1>
        <p className="mb-4 text-sm text-muted">
          Authenticator&apos;ınıza erişemezseniz bu kodlarla giriş yapabilirsiniz. <b>Bir daha gösterilmez</b> — güvenli bir yerde saklayın.
        </p>
        <div className="mb-4 grid grid-cols-2 gap-2 rounded-xl border border-border bg-surface-muted p-4 font-mono text-sm">
          {recoveryCodes.map((c) => <span key={c}>{c}</span>)}
        </div>
        <Button onClick={finish} className="w-full">Sakladım, devam et</Button>
      </Card>
    );
  }

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">İki adımlı doğrulama kurulumu</h1>
      <p className="mb-4 text-sm text-muted">
        Owner/Yönetici hesapları için MFA zorunludur. Authenticator uygulamanızla (Google/Microsoft Authenticator, 1Password, Authy…) QR&apos;ı tarayın.
      </p>
      {error && <Alert kind="error">{error}</Alert>}
      {otpauthUri ? (
        <>
          <div className="mb-4 flex justify-center rounded-xl border border-border bg-white p-4">
            <QRCodeSVG value={otpauthUri} size={180} />
          </div>
          <p className="mb-4 text-xs text-muted">
            QR taranamıyorsa elle ekleyin: <code className="rounded bg-surface-muted px-1 py-0.5 font-mono">{manualKey}</code>
          </p>
          <form onSubmit={verify} className="space-y-3">
            <FormField label="Authenticator kodu" htmlFor="code">
              <Input id="code" inputMode="numeric" autoComplete="one-time-code" value={code}
                onChange={(e) => setCode(e.target.value)} required autoFocus />
            </FormField>
            <Button type="submit" loading={busy} className="w-full">Doğrula ve etkinleştir</Button>
          </form>
        </>
      ) : (
        !error && <p className="text-sm text-muted">Yükleniyor…</p>
      )}
    </Card>
  );
}
