"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card } from "@/components/ui";

/** Sprint 16: kayıt sonrası "e-postanızı doğrulayın" ekranı + tekrar gönder. */
export default function CheckEmailPage() {
  const { user, reloadMe } = useAuth();
  const router = useRouter();
  const [msg, setMsg] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const resend = async () => {
    setBusy(true);
    setMsg(null);
    try {
      await authService.resendVerification();
      setMsg("Doğrulama e-postası gönderildi. Gelen kutunuzu kontrol edin.");
    } catch (e) {
      setMsg(e instanceof ApiError ? e.message : "Gönderilemedi.");
    } finally {
      setBusy(false);
    }
  };

  const continueNext = async () => {
    await reloadMe();
    router.replace("/join-studio");
  };

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">E-posta adresinizi doğrulayın</h1>
      <p className="mb-6 text-sm text-muted">
        {user?.email ? <><span className="font-medium text-foreground">{user.email}</span> adresine </> : null}
        bir doğrulama bağlantısı gönderdik. Stüdyoya katılmak için önce e-postanızı doğrulayın.
      </p>

      {msg && <Alert kind="info">{msg}</Alert>}

      <div className="mt-4 space-y-3">
        <Button onClick={resend} loading={busy} variant="secondary" className="w-full">Doğrulama e-postasını tekrar gönder</Button>
        <Button onClick={continueNext} className="w-full">Doğruladım, devam et</Button>
      </div>

      <p className="mt-6 text-center text-sm text-muted">
        <Link href="/login" className="font-medium text-primary hover:text-primary-hover">Giriş ekranına dön</Link>
      </p>
    </Card>
  );
}
