"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { authService } from "@/lib/services";

/**
 * Sprint 16: e-postası doğrulanmamış kullanıcıya dashboard'da uyarı + tekrar gönder.
 * Doğrulanmışsa hiçbir şey göstermez. (register-studio owner'ı için de geçerli — Seçenek A.)
 */
export function EmailVerificationBanner() {
  const { user, reloadMe } = useAuth();
  const [busy, setBusy] = useState(false);
  const [msg, setMsg] = useState<string | null>(null);

  if (!user || user.emailVerified) return null;

  const resend = async () => {
    setBusy(true);
    setMsg(null);
    try {
      await authService.resendVerification();
      setMsg("Doğrulama e-postası gönderildi.");
    } catch {
      setMsg("Gönderilemedi.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-wrap items-center justify-between gap-2 border-b border-warning/40 bg-warning/10 px-6 py-2 text-sm text-warning">
      <span>E-posta adresiniz doğrulanmadı. Bazı işlemler (örn. stüdyoya katılım) doğrulama gerektirir.</span>
      <div className="flex shrink-0 items-center gap-3">
        {msg && <span className="text-xs">{msg}</span>}
        <button onClick={resend} disabled={busy} className="font-medium underline underline-offset-2 disabled:opacity-50">
          {busy ? "Gönderiliyor…" : "Tekrar gönder"}
        </button>
        <button onClick={() => reloadMe()} className="font-medium underline underline-offset-2">Doğruladım</button>
      </div>
    </div>
  );
}
