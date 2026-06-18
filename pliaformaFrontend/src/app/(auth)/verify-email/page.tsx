"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { authService } from "@/lib/services";
import { ApiError } from "@/lib/api";
import { Alert, Button, Card } from "@/components/ui";

/** Sprint 16: e-posta doğrulama. Linkteki token ile /api/auth/verify-email çağrılır. */
export default function VerifyEmailPage() {
  const [status, setStatus] = useState<"loading" | "ok" | "error">("loading");
  const [msg, setMsg] = useState("");

  useEffect(() => {
    const token = new URLSearchParams(window.location.search).get("token");
    if (!token) {
      setStatus("error");
      setMsg("Geçersiz doğrulama bağlantısı.");
      return;
    }
    authService
      .verifyEmail(token)
      .then(() => setStatus("ok"))
      .catch((e) => {
        setStatus("error");
        setMsg(e instanceof ApiError ? e.message : "Doğrulama başarısız.");
      });
  }, []);

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">E-posta doğrulama</h1>
      {status === "loading" && <p className="text-sm text-muted">Doğrulanıyor…</p>}
      {status === "ok" && (
        <div className="space-y-4">
          <Alert kind="success">E-posta adresiniz doğrulandı. Artık tüm özellikleri kullanabilirsiniz.</Alert>
          <Link href="/login"><Button className="w-full">Giriş yap</Button></Link>
        </div>
      )}
      {status === "error" && (
        <div className="space-y-4">
          <Alert kind="error">{msg}</Alert>
          <p className="text-sm text-muted">Bağlantı geçersiz veya süresi dolmuş olabilir. Giriş yapıp yeni bir doğrulama e-postası isteyebilirsiniz.</p>
          <Link href="/login"><Button variant="secondary" className="w-full">Giriş yap</Button></Link>
        </div>
      )}
    </Card>
  );
}
