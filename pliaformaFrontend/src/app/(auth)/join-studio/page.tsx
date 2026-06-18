"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { membershipService } from "@/lib/studioApi";
import { authService } from "@/lib/services";
import type { MyJoinRequestDto, PendingInvitationDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card, FormField, Input } from "@/components/ui";

/**
 * Stüdyosuz kullanıcı alanı + "Davetlerim ve katılım" ekranı (Sprint 15 → Sprint 17 Rev2).
 *
 * Kayıt niyetine (user.registrationIntent) göre dallanır:
 *  - Instructor → JoinCode formu YOK; "stüdyo sizi davet etmeli" + Davetlerim (hoca davetle katılır).
 *  - Student    → JoinCode formu + Davetlerim.
 *  - Unknown    → nötr seçim ekranı (öğrenci başvurusu / hoca davet bekleme).
 *
 * E-posta doğrulama: görüntülemeyi/başvuruyu ENGELLEMEZ. Yalnız üyelik AKTİFLEŞİRKEN (kabul/onay) gerekir.
 */
export default function JoinStudioPage() {
  const { user, memberships, logout, switchStudio, reloadMe } = useAuth();
  const router = useRouter();

  const intent = user?.registrationIntent ?? "Unknown";
  const [choice, setChoice] = useState<"student" | "instructor" | null>(null);
  const view: "student" | "instructor" | "chooser" =
    intent === "Student" ? "student" : intent === "Instructor" ? "instructor" : (choice ?? "chooser");

  const [joinCode, setJoinCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [requests, setRequests] = useState<MyJoinRequestDto[]>([]);
  const [invitations, setInvitations] = useState<PendingInvitationDto[]>([]);
  const [inviteBusyId, setInviteBusyId] = useState<string | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const invitationsRef = useRef<HTMLDivElement>(null);

  const verified = user?.emailVerified ?? true;
  const hasMembership = memberships.length > 0;

  const loadRequests = useCallback(async () => {
    try { setRequests(await membershipService.myJoinRequests()); } catch { setRequests([]); }
  }, []);
  const loadInvitations = useCallback(async () => {
    try { setInvitations(await membershipService.myInvitations()); } catch { setInvitations([]); }
  }, []);

  useEffect(() => { loadRequests(); loadInvitations(); }, [loadRequests, loadInvitations]);

  // Bildirim derin linki: /join-studio?tab=invitations veya #invitations → Davetlerim bölümüne kaydır.
  useEffect(() => {
    if (typeof window === "undefined") return;
    const params = new URLSearchParams(window.location.search);
    if ((params.get("tab") === "invitations" || window.location.hash === "#invitations") && invitationsRef.current) {
      invitationsRef.current.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [invitations.length]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null); setOkMsg(null); setLoading(true);
    try {
      // Sprint 17: kodla başvuru yalnız Öğrenci rolüyle. Doğrulama başvuruyu engellemez (aktivasyonda gerekir).
      await membershipService.apply(joinCode, "Student");
      setOkMsg("Başvurunuz gönderildi. Stüdyo onayladığında üye olacaksınız.");
      setJoinCode("");
      await loadRequests();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Başvuru gönderilemedi.");
    } finally { setLoading(false); }
  };

  const acceptInvite = async (inv: PendingInvitationDto) => {
    setInviteError(null); setInviteBusyId(inv.membershipId);
    try {
      await membershipService.accept(inv.membershipId);
      await reloadMe();
      try {
        await switchStudio(inv.studioId);
        router.replace("/dashboard");
        return;
      } catch {
        await loadInvitations();
        setOkMsg(`${inv.studioName} üyeliğiniz onaylandı.`);
      }
    } catch (err) {
      // Doğrulanmamışsa backend 403: "Üyeliğin aktifleşmesi için önce e-posta adresi doğrulanmalıdır."
      setInviteError(err instanceof ApiError ? err.message : "Davet kabul edilemedi.");
    } finally { setInviteBusyId(null); }
  };

  const rejectInvite = async (inv: PendingInvitationDto) => {
    setInviteError(null); setInviteBusyId(inv.membershipId);
    try {
      await membershipService.reject(inv.membershipId);
      await loadInvitations();
    } catch (err) {
      setInviteError(err instanceof ApiError ? err.message : "Davet reddedilemedi.");
    } finally { setInviteBusyId(null); }
  };

  const pending = requests.filter((r) => r.status === "Pending");
  const rejected = requests.filter((r) => r.status === "Rejected");
  const roleLabel = (r: string) => (r === "Instructor" ? "Hoca" : r === "StudioAdmin" ? "Yönetici" : "Öğrenci");

  const [resending, setResending] = useState(false);
  const [resendMsg, setResendMsg] = useState<string | null>(null);
  const resendVerification = async () => {
    setResending(true); setResendMsg(null);
    try { await authService.resendVerification(); setResendMsg("Doğrulama e-postası gönderildi."); }
    catch { setResendMsg("Gönderilemedi, lütfen tekrar deneyin."); }
    finally { setResending(false); }
  };

  const title =
    view === "instructor" ? (hasMembership ? "Davetler" : "Davet bekliyorsunuz")
    : view === "student" ? (hasMembership ? "Davetler ve stüdyoya katılım" : "Bir stüdyoya katılın")
    : "Henüz bir stüdyoya bağlı değilsiniz";

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">{title}</h1>
      <p className="mb-6 text-sm text-muted">
        {user?.fullName ? `Merhaba ${user.fullName}. ` : ""}
        {view === "instructor"
          ? "Bir stüdyoya hoca olarak katılmak için stüdyo sahibinin sizi e-posta adresinizle davet etmesi gerekir. Davet geldiğinde aşağıda görünecek."
          : view === "student"
          ? "Stüdyonuzdan aldığınız öğrenci koduyla başvurun ya da size gelen daveti kabul edin."
          : "Nasıl devam etmek istediğinizi seçin."}
      </p>

      {/* E-posta doğrulama: yumuşak bilgilendirme — görüntüleme/başvuruyu engellemez. */}
      {!verified && (
        <div className="mb-5 space-y-2">
          <Alert kind="info">
            E-posta adresiniz henüz doğrulanmadı. Davetleri görebilir ve başvuru yapabilirsiniz; ancak
            <strong> üyeliğin aktifleşmesi (kabul/onay)</strong> için doğrulama gerekir.
          </Alert>
          <div className="flex items-center gap-3">
            <Button variant="secondary" size="sm" loading={resending} onClick={resendVerification}>
              Doğrulama e-postasını tekrar gönder
            </Button>
            {resendMsg && <span className="text-xs text-muted">{resendMsg}</span>}
          </div>
        </div>
      )}

      {/* Nötr seçim ekranı (Unknown intent) */}
      {view === "chooser" && (
        <div className="mb-2 grid gap-3">
          <button
            onClick={() => setChoice("student")}
            className="rounded-xl border border-border bg-surface px-4 py-3 text-left hover:border-primary"
          >
            <p className="text-sm font-medium text-foreground">Öğrenci olarak stüdyoya başvur</p>
            <p className="text-xs text-muted">Stüdyonuzun verdiği katılım koduyla başvurun.</p>
          </button>
          <button
            onClick={() => setChoice("instructor")}
            className="rounded-xl border border-border bg-surface px-4 py-3 text-left hover:border-primary"
          >
            <p className="text-sm font-medium text-foreground">Hoca olarak davet bekliyorum</p>
            <p className="text-xs text-muted">Stüdyo sizi e-posta ile davet eder; davet burada görünür.</p>
          </button>
        </div>
      )}

      {/* Davetlerim — tüm görünümlerde */}
      <div ref={invitationsRef} id="invitations" className="mt-6 scroll-mt-6">
        <h2 className="mb-2 text-sm font-semibold text-foreground">Davetlerim</h2>
        {inviteError && <Alert kind="error">{inviteError}</Alert>}
        {invitations.length === 0 ? (
          <p className="rounded-xl border border-dashed border-border bg-surface px-4 py-3 text-xs text-muted">
            {view === "instructor"
              ? "Henüz bir davetiniz yok. Bir stüdyo sizi davet ettiğinde burada görünecek."
              : "Bekleyen davetiniz yok. Bir stüdyo sizi davet ettiğinde burada görünür."}
          </p>
        ) : (
          <ul className="space-y-2">
            {invitations.map((inv) => (
              <li key={inv.membershipId} className="rounded-xl border border-border bg-surface px-4 py-3">
                <div className="flex items-center justify-between gap-2">
                  <div>
                    <p className="text-sm font-medium text-foreground">{inv.studioName}</p>
                    <p className="text-xs text-muted">{roleLabel(inv.role)} · {inv.invitedAt.slice(0, 10)}</p>
                  </div>
                  <Badge tone="rose">Davet</Badge>
                </div>
                <div className="mt-3 flex items-center gap-2">
                  <Button
                    size="sm"
                    loading={inviteBusyId === inv.membershipId}
                    disabled={inviteBusyId !== null}
                    onClick={() => acceptInvite(inv)}
                  >
                    Kabul et
                  </Button>
                  <Button
                    size="sm"
                    variant="secondary"
                    disabled={inviteBusyId !== null}
                    onClick={() => rejectInvite(inv)}
                  >
                    Reddet
                  </Button>
                </div>
                {!verified && (
                  <p className="mt-2 text-[11px] text-muted">
                    Kabul için önce e-posta adresinizi doğrulamanız gerekir.
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Öğrenci kodu ile başvuru — yalnız student görünümünde */}
      {view === "student" && (
        <form onSubmit={submit} className="mt-6 space-y-4">
          <h2 className="text-sm font-semibold text-foreground">Öğrenci olarak katıl</h2>
          {error && <Alert kind="error">{error}</Alert>}
          {okMsg && <Alert kind="success">{okMsg}</Alert>}

          <FormField label="Katılım kodu" htmlFor="joinCode" hint="Stüdyonuzdan aldığınız öğrenci kodu. Örn. ABCD-EFGH-JK">
            <Input
              id="joinCode"
              value={joinCode}
              onChange={(e) => setJoinCode(e.target.value)}
              placeholder="ABCD-EFGH-JK"
              autoCapitalize="characters"
              required
            />
          </FormField>

          <p className="text-xs text-muted">Hocalar kodla başvuramaz; bir stüdyonun sizi davet etmesi gerekir.</p>

          <Button type="submit" loading={loading} className="w-full">Öğrenci başvurusu gönder</Button>
        </form>
      )}

      {okMsg && view !== "student" && <Alert kind="success">{okMsg}</Alert>}

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

      {/* İleride: stüdyo keşfi / yakındaki stüdyolar / öne çıkan stüdyolar (discovery) buraya gelecek. */}
      <p className="mt-6 rounded-xl border border-dashed border-border bg-surface px-4 py-3 text-[11px] text-muted">
        Yakında: yakınınızdaki ve öne çıkan stüdyoları buradan keşfedip katılabileceksiniz.
      </p>

      <div className="mt-6 flex items-center justify-between text-sm">
        <Link href="/register-studio" className="font-medium text-primary hover:text-primary-hover">
          Kendi stüdyonuzu açın
        </Link>
        <div className="flex items-center gap-4">
          {hasMembership && (
            <button onClick={() => router.replace("/dashboard")} className="text-muted hover:text-foreground">
              Panele dön
            </button>
          )}
          <button onClick={() => { logout(); router.replace("/login"); }} className="text-muted hover:text-foreground">
            Çıkış yap
          </button>
        </div>
      </div>
    </Card>
  );
}
