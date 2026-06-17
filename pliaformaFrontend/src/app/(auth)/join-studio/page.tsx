"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { membershipService } from "@/lib/studioApi";
import type { MyJoinRequestDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input, Select } from "@/components/ui";

/**
 * Sprint 15: stüdyosuz kullanıcının katılım ekranı. JoinCode + rol (Öğrenci/Hoca) ile başvuru gönderir;
 * kendi bekleyen/reddedilen başvurularını görür. Onaylanınca üye olur (sonra giriş/yenilemede dashboard).
 */
export default function JoinStudioPage() {
  const { user, activeStudio, memberships, logout } = useAuth();
  const router = useRouter();
  const [joinCode, setJoinCode] = useState("");
  const [role, setRole] = useState<"Student" | "Instructor">("Student");
  const [error, setError] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<MyJoinRequestDto[]>([]);

  // Aktif üyeliği varsa burada işi yok → dashboard.
  useEffect(() => {
    if (activeStudio) router.replace("/dashboard");
    else if (memberships.length > 1) router.replace("/select-studio");
  }, [activeStudio, memberships.length, router]);

  const loadRequests = useCallback(async () => {
    try {
      setRequests(await membershipService.myJoinRequests());
    } catch {
      setRequests([]);
    }
  }, []);

  useEffect(() => {
    loadRequests();
  }, [loadRequests]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setOkMsg(null);
    setLoading(true);
    try {
      await membershipService.apply(joinCode, role);
      setOkMsg("Başvurunuz gönderildi. Stüdyo onayladığında üye olacaksınız.");
      setJoinCode("");
      await loadRequests();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Başvuru gönderilemedi.");
    } finally {
      setLoading(false);
    }
  };

  const pending = requests.filter((r) => r.status === "Pending");
  const rejected = requests.filter((r) => r.status === "Rejected");
  const roleLabel = (r: string) => (r === "Instructor" ? "Hoca" : "Öğrenci");

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Bir stüdyoya katılın</h1>
      <p className="mb-6 text-sm text-muted">
        {user?.fullName ? `Merhaba ${user.fullName}. ` : ""}Stüdyonuzdan aldığınız katılım kodunu girin.
      </p>

      <form onSubmit={submit} className="space-y-4">
        {error && <Alert kind="error">{error}</Alert>}
        {okMsg && <Alert kind="success">{okMsg}</Alert>}

        <FormField label="Katılım kodu" htmlFor="joinCode" hint="Örn. ABCD-EFGH-JK">
          <Input
            id="joinCode"
            value={joinCode}
            onChange={(e) => setJoinCode(e.target.value)}
            placeholder="ABCD-EFGH-JK"
            autoCapitalize="characters"
            required
          />
        </FormField>

        <FormField label="Hangi rolle katılıyorsunuz?" htmlFor="role">
          <Select id="role" value={role} onChange={(e) => setRole(e.target.value as "Student" | "Instructor")}>
            <option value="Student">Öğrenci olarak katıl</option>
            <option value="Instructor">Hoca olarak katıl</option>
          </Select>
        </FormField>

        <Button type="submit" loading={loading} className="w-full">
          Başvuru gönder
        </Button>
      </form>

      {pending.length > 0 && (
        <div className="mt-6">
          <h2 className="mb-2 text-sm font-semibold text-foreground">Bekleyen başvurularım</h2>
          <ul className="space-y-2">
            {pending.map((r) => (
              <li key={r.membershipId} className="flex items-center justify-between rounded-xl border border-border bg-surface px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-foreground">{r.studioName}</p>
                  <p className="text-xs text-muted">{roleLabel(r.requestedRole)} · {r.requestedAt.slice(0, 10)}</p>
                </div>
                <Badge tone="warning">Onay bekliyor</Badge>
              </li>
            ))}
          </ul>
        </div>
      )}

      {rejected.length > 0 && (
        <div className="mt-6">
          <h2 className="mb-2 text-sm font-semibold text-foreground">Reddedilen başvurularım</h2>
          <ul className="space-y-2">
            {rejected.map((r) => (
              <li key={r.membershipId} className="flex items-center justify-between rounded-xl border border-border bg-surface px-4 py-3">
                <div>
                  <p className="text-sm font-medium text-foreground">{r.studioName}</p>
                  <p className="text-xs text-muted">{roleLabel(r.requestedRole)} · {(r.rejectedAt ?? r.requestedAt).slice(0, 10)}</p>
                </div>
                <Badge tone="neutral">Reddedildi</Badge>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-xs text-muted">Reddedilen bir stüdyoya yeniden başvurabilirsiniz.</p>
        </div>
      )}

      <div className="mt-6 flex items-center justify-between text-sm">
        <Link href="/register-studio" className="font-medium text-primary hover:text-primary-hover">
          Kendi stüdyonuzu açın
        </Link>
        <button onClick={() => { logout(); router.replace("/login"); }} className="text-muted hover:text-foreground">
          Çıkış yap
        </button>
      </div>
    </Card>
  );
}
