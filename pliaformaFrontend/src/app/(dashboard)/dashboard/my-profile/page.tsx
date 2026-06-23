"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { studentService } from "@/lib/studioApi";
import { authService } from "@/lib/services";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { errorMessage } from "@/lib/errors";
import { type StudentDto, studentSourceLabels, studentStatusLabels } from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

function Row({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="flex justify-between gap-4 border-b border-border py-2 last:border-0">
      <span className="text-sm text-muted">{label}</span>
      <span className="text-sm font-medium text-foreground">{value || "—"}</span>
    </div>
  );
}

// Backend ile aynı politika (asıl doğrulama backend'de; bu yalnız erken/kullanıcı dostu kontrol).
function clientPolicyError(pw: string): string | null {
  if (pw.length < 10) return "Parola en az 10 karakter olmalı.";
  if (!/[A-Z]/.test(pw)) return "Parola en az bir büyük harf içermeli.";
  if (!/[a-z]/.test(pw)) return "Parola en az bir küçük harf içermeli.";
  if (!/[0-9]/.test(pw)) return "Parola en az bir rakam içermeli.";
  if (!/[^A-Za-z0-9]/.test(pw)) return "Parola en az bir özel karakter içermeli.";
  return null;
}

function firstFieldError(e: unknown): string {
  if (e instanceof ApiError && e.errors) {
    const first = Object.values(e.errors)[0]?.[0];
    if (first) return first;
  }
  return errorMessage(e);
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  return (
    <label className="block space-y-1">
      <span className="text-sm text-muted">{label}</span>
      <input
        type="password"
        autoComplete="off"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm text-foreground outline-none focus:border-foreground/40"
      />
    </label>
  );
}

function ChangePasswordCard() {
  const { logout } = useAuth();
  const router = useRouter();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setError(null);
    if (!current) return setError("Mevcut parolanızı girin.");
    const policy = clientPolicyError(next);
    if (policy) return setError(policy);
    if (next !== confirm) return setError("Yeni parolalar eşleşmiyor.");

    setBusy(true);
    try {
      await authService.changePassword(current, next);
      setDone(true);
      // Backend tüm refresh token'ları revoke etti → tekrar giriş gerekiyor.
      setTimeout(async () => {
        await logout();
        router.push("/login");
      }, 1800);
    } catch (e) {
      setError(firstFieldError(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <h2 className="mb-3 text-lg font-semibold text-foreground">Parola değiştir</h2>
      {done ? (
        <Alert kind="success">Parolanız güncellendi. Oturumlarınız yenilendi; giriş ekranına yönlendiriliyorsunuz…</Alert>
      ) : (
        <div className="space-y-3">
          {error && <Alert kind="error">{error}</Alert>}
          <Field label="Mevcut parola" value={current} onChange={setCurrent} />
          <Field label="Yeni parola" value={next} onChange={setNext} />
          <Field label="Yeni parola (tekrar)" value={confirm} onChange={setConfirm} />
          <p className="text-xs text-muted">
            En az 10 karakter; büyük/küçük harf, rakam ve özel karakter içermeli. Başarılı değişimde tüm
            oturumlar kapatılır ve tekrar giriş yapmanız gerekir.
          </p>
          <Button onClick={submit} disabled={busy}>{busy ? "Güncelleniyor…" : "Parolayı güncelle"}</Button>
        </div>
      )}
    </Card>
  );
}

function MfaStatusCard() {
  const { user } = useAuth();
  const [remaining, setRemaining] = useState<number | null>(null);

  useEffect(() => {
    if (!user?.mfaEnabled) return;
    authService.me().then((me) => setRemaining(me.mfaRecoveryCodesRemaining ?? 0)).catch(() => {});
  }, [user?.mfaEnabled]);

  return (
    <Card>
      <h2 className="mb-2 text-lg font-semibold text-foreground">İki adımlı doğrulama (MFA)</h2>
      {user?.mfaEnabled ? (
        <div className="space-y-1 text-sm">
          <p className="flex items-center gap-2 text-foreground">Durum: <Badge tone="success">Aktif</Badge></p>
          {remaining !== null && <p className="text-muted">Kalan kurtarma kodu: <span className="font-medium text-foreground">{remaining}</span></p>}
          <p className="pt-1 text-xs text-muted">MFA&apos;yı sıfırlamak için platform yöneticinizle iletişime geçin (self-servis kapatma yoktur).</p>
        </div>
      ) : (
        <p className="text-sm text-muted">MFA bu hesap için aktif değil.</p>
      )}
    </Card>
  );
}

export default function MyProfilePage() {
  const [profile, setProfile] = useState<StudentDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setProfile(await studentService.me());
      } catch (e) {
        setError(e instanceof ApiError && e.status === 404 ? "Bu stüdyoda bir öğrenci profiliniz yok." : "Profil yüklenemedi.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <h1 className="text-2xl font-semibold text-foreground">Profilim</h1>
      {error && <Alert kind="info">{error}</Alert>}
      {profile && (
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-semibold text-foreground">{profile.fullName}</h2>
            <Badge tone={profile.status === "Active" ? "success" : "neutral"}>{studentStatusLabels[profile.status]}</Badge>
          </div>
          <Row label="Telefon" value={profile.phone} />
          <Row label="E-posta" value={profile.email} />
          <Row label="Kaynak" value={studentSourceLabels[profile.source]} />
          <Row label="Doğum tarihi" value={profile.birthDate} />
          <Row label="Acil durum kişisi" value={profile.emergencyContactName} />
          <Row label="Acil durum telefonu" value={profile.emergencyContactPhone} />
          {profile.healthNotes && <Row label="Sağlık notu" value={profile.healthNotes} />}
          {profile.notes && <Row label="Not" value={profile.notes} />}
        </Card>
      )}

      {/* MFA durumu + parola değiştirme tüm oturum açmış kullanıcılar için. */}
      <MfaStatusCard />
      <ChangePasswordCard />
    </div>
  );
}
