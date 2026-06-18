"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { ApiError } from "@/lib/api";
import { membershipService } from "@/lib/studioApi";
import { authService } from "@/lib/services";
import type { PendingInvitationDto } from "@/lib/domain-types";
import { Alert, Badge, Button, Card } from "@/components/ui";

/**
 * Stüdyosuz kullanıcı alanı + "Davetlerim" ekranı (Sprint 17 Rev3 — invite-only onboarding).
 *
 * JoinCode / self-apply KALDIRILDI: ne öğrenci ne hoca kodla başvurur. Stüdyoya katılım yalnızca stüdyonun
 * **daveti** ile olur. Bu ekranda yalnız: niyet'e göre bilgilendirme + Davetlerim (kabul/ret) + kendi
 * stüdyonu oluştur + çıkış. RegistrationIntent yalnız metni belirler (davranışa etki etmez).
 *
 * E-posta doğrulama: görüntülemeyi/reddetmeyi engellemez; yalnız üyelik AKTİFLEŞİRKEN (kabul) gerekir.
 */
export default function JoinStudioPage() {
  const { user, memberships, logout, switchStudio, reloadMe } = useAuth();
  const router = useRouter();

  const intent = user?.registrationIntent ?? "Unknown";
  const hasMembership = memberships.length > 0;
  const verified = user?.emailVerified ?? true;

  const [invitations, setInvitations] = useState<PendingInvitationDto[]>([]);
  const [inviteBusyId, setInviteBusyId] = useState<string | null>(null);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [okMsg, setOkMsg] = useState<string | null>(null);
  const invitationsRef = useRef<HTMLDivElement>(null);

  const loadInvitations = useCallback(async () => {
    try { setInvitations(await membershipService.myInvitations()); } catch { setInvitations([]); }
  }, []);

  useEffect(() => { loadInvitations(); }, [loadInvitations]);

  // Bildirim derin linki: /join-studio?tab=invitations veya #invitations → Davetlerim'e kaydır.
  useEffect(() => {
    if (typeof window === "undefined") return;
    const params = new URLSearchParams(window.location.search);
    if ((params.get("tab") === "invitations" || window.location.hash === "#invitations") && invitationsRef.current) {
      invitationsRef.current.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [invitations.length]);

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
    try { await membershipService.reject(inv.membershipId); await loadInvitations(); }
    catch (err) { setInviteError(err instanceof ApiError ? err.message : "Davet reddedilemedi."); }
    finally { setInviteBusyId(null); }
  };

  const roleLabel = (r: string) => (r === "Instructor" ? "Hoca" : r === "StudioAdmin" ? "Yönetici" : "Öğrenci");

  const [resending, setResending] = useState(false);
  const [resendMsg, setResendMsg] = useState<string | null>(null);
  const resendVerification = async () => {
    setResending(true); setResendMsg(null);
    try { await authService.resendVerification(); setResendMsg("Doğrulama e-postası gönderildi."); }
    catch { setResendMsg("Gönderilemedi, lütfen tekrar deneyin."); }
    finally { setResending(false); }
  };

  // Niyet'e göre bilgilendirme metni (yalnız UX — kodla başvuru yok).
  const intentText =
    intent === "Instructor" ? "Bir stüdyoya hoca olarak katılmak için stüdyonun sizi davet etmesi gerekir."
    : intent === "Student" ? "Bir stüdyoya öğrenci olarak katılmak için stüdyonun sizi davet etmesi gerekir."
    : "Bir stüdyoya katılmak için stüdyo tarafından davet edilmeniz gerekir.";

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">
        {hasMembership ? "Davetler" : "Henüz bir stüdyoya bağlı değilsiniz"}
      </h1>
      <p className="mb-2 text-sm text-muted">
        {user?.fullName ? `Merhaba ${user.fullName}. ` : ""}{intentText}
      </p>
      <p className="mb-6 text-sm text-muted">Bir davet geldiğinde aşağıdaki “Davetlerim” bölümünde görünecek.</p>

      {!verified && (
        <div className="mb-5 space-y-2">
          <Alert kind="info">
            E-posta adresiniz henüz doğrulanmadı. Davetleri görebilirsiniz; ancak
            <strong> üyeliğin aktifleşmesi (daveti kabul)</strong> için doğrulama gerekir.
          </Alert>
          <div className="flex items-center gap-3">
            <Button variant="secondary" size="sm" loading={resending} onClick={resendVerification}>
              Doğrulama e-postasını tekrar gönder
            </Button>
            {resendMsg && <span className="text-xs text-muted">{resendMsg}</span>}
          </div>
        </div>
      )}

      {okMsg && <Alert kind="success">{okMsg}</Alert>}

      {/* Davetlerim */}
      <div ref={invitationsRef} id="invitations" className="mt-2 scroll-mt-6">
        <h2 className="mb-2 text-sm font-semibold text-foreground">Davetlerim</h2>
        {inviteError && <Alert kind="error">{inviteError}</Alert>}
        {invitations.length === 0 ? (
          <p className="rounded-xl border border-dashed border-border bg-surface px-4 py-3 text-xs text-muted">
            Bekleyen davetiniz yok. Bir stüdyo sizi davet ettiğinde burada görünecek.
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
                  <Button size="sm" loading={inviteBusyId === inv.membershipId} disabled={inviteBusyId !== null} onClick={() => acceptInvite(inv)}>
                    Kabul et
                  </Button>
                  <Button size="sm" variant="secondary" disabled={inviteBusyId !== null} onClick={() => rejectInvite(inv)}>
                    Reddet
                  </Button>
                </div>
                {!verified && (
                  <p className="mt-2 text-[11px] text-muted">Kabul için önce e-posta adresinizi doğrulamanız gerekir.</p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div className="mt-8 flex items-center justify-between text-sm">
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
