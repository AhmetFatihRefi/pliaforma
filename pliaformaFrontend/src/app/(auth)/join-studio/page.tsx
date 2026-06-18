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
 * Stüdyosuz kullanıcı alanı + "Davetlerim ve katılım" ekranı (Sprint 15 → Sprint 17).
 *
 * İKİ AMAÇ:
 *  (A) Stüdyosuz kullanıcı buraya otomatik yönlenir (dashboard layout guard) → bilinçli empty-state.
 *  (B) Aktif üyeliği OLAN kullanıcı da (bildirim/menü linkiyle) buraya gelebilir; yeni bir stüdyodan davet
 *      alabilir. Bu yüzden sayfa memberships sayısına göre kullanıcıyı GERİ ATMAZ.
 *
 * Sprint 17:
 *  - "Davetlerim": stüdyonun davet ettiği üyelikler (kabul/ret). Kabul için e-posta doğrulaması şart.
 *  - JoinCode başvurusu yalnız ÖĞRENCİ içindir (hoca yalnız davetle katılır).
 *  - İleride bu alan discovery/marketplace'e (yakındaki/öne çıkan stüdyolar) evrilebilir — şimdilik yalnız not.
 */
export default function JoinStudioPage() {
  const { user, memberships, logout, switchStudio, reloadMe } = useAuth();
  const router = useRouter();

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

  const loadRequests = useCallback(async () => {
    try {
      setRequests(await membershipService.myJoinRequests());
    } catch {
      setRequests([]);
    }
  }, []);

  const loadInvitations = useCallback(async () => {
    try {
      setInvitations(await membershipService.myInvitations());
    } catch {
      setInvitations([]);
    }
  }, []);

  useEffect(() => {
    loadRequests();
    loadInvitations();
  }, [loadRequests, loadInvitations]);

  // Bildirim derin linki: /join-studio?tab=invitations veya #invitations → Davetlerim bölümüne kaydır.
  useEffect(() => {
    if (typeof window === "undefined") return;
    const params = new URLSearchParams(window.location.search);
    const wantsInvites = params.get("tab") === "invitations" || window.location.hash === "#invitations";
    if (wantsInvites && invitationsRef.current) {
      invitationsRef.current.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }, [invitations.length]);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setOkMsg(null);
    setLoading(true);
    try {
      // Sprint 17: kodla başvuru yalnız Öğrenci rolüyle yapılır.
      await membershipService.apply(joinCode, "Student");
      setOkMsg("Başvurunuz gönderildi. Stüdyo onayladığında üye olacaksınız.");
      setJoinCode("");
      await loadRequests();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Başvuru gönderilemedi.");
    } finally {
      setLoading(false);
    }
  };

  const acceptInvite = async (inv: PendingInvitationDto) => {
    setInviteError(null);
    setInviteBusyId(inv.membershipId);
    try {
      await membershipService.accept(inv.membershipId);
      // Davet edilen stüdyoyu aktif yapıp panele geç (stüdyosuz kullanıcı için ilk stüdyosu; üyeyse yeni stüdyo).
      await reloadMe();
      try {
        await switchStudio(inv.studioId);
        router.replace("/dashboard");
        return;
      } catch {
        // Switch başarısızsa en azından durumu tazele + davetleri yenile.
        await loadInvitations();
        setOkMsg(`${inv.studioName} üyeliğiniz onaylandı.`);
      }
    } catch (err) {
      setInviteError(err instanceof ApiError ? err.message : "Davet kabul edilemedi.");
    } finally {
      setInviteBusyId(null);
    }
  };

  const rejectInvite = async (inv: PendingInvitationDto) => {
    setInviteError(null);
    setInviteBusyId(inv.membershipId);
    try {
      await membershipService.reject(inv.membershipId);
      await loadInvitations();
    } catch (err) {
      setInviteError(err instanceof ApiError ? err.message : "Davet reddedilemedi.");
    } finally {
      setInviteBusyId(null);
    }
  };

  const pending = requests.filter((r) => r.status === "Pending");
  const rejected = requests.filter((r) => r.status === "Rejected");
  const roleLabel = (r: string) => (r === "Instructor" ? "Hoca" : r === "StudioAdmin" ? "Yönetici" : "Öğrenci");

  const [resending, setResending] = useState(false);
  const [resendMsg, setResendMsg] = useState<string | null>(null);
  const resendVerification = async () => {
    setResending(true);
    setResendMsg(null);
    try {
      await authService.resendVerification();
      setResendMsg("Doğrulama e-postası gönderildi.");
    } catch {
      setResendMsg("Gönderilemedi, lütfen tekrar deneyin.");
    } finally {
      setResending(false);
    }
  };

  const hasMembership = memberships.length > 0;

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">
        {hasMembership ? "Davetler ve stüdyoya katılım" : "Henüz bir stüdyoya bağlı değilsiniz"}
      </h1>
      <p className="mb-6 text-sm text-muted">
        {user?.fullName ? `Merhaba ${user.fullName}. ` : ""}
        {hasMembership
          ? "Size gelen davetleri görebilir, yeni bir stüdyoya öğrenci olarak başvurabilirsiniz."
          : "Bir davetinizi kabul edin, öğrenci kodunuzla başvurun ya da kendi stüdyonuzu açın."}
      </p>

      {!verified && (
        <div className="mb-4 space-y-2">
          <Alert kind="warning">
            Bir stüdyoya katılmak veya daveti kabul etmek için önce e-posta adresinizi doğrulayın. Gelen
            kutunuzdaki doğrulama bağlantısına tıklayın.
          </Alert>
          <div className="flex items-center gap-3">
            <Button variant="secondary" size="sm" loading={resending} onClick={resendVerification}>
              Doğrulama e-postasını tekrar gönder
            </Button>
            {resendMsg && <span className="text-xs text-muted">{resendMsg}</span>}
          </div>
        </div>
      )}

      {/* Davetlerim */}
      <div ref={invitationsRef} id="invitations" className="mb-6 scroll-mt-6">
        <h2 className="mb-2 text-sm font-semibold text-foreground">Davetlerim</h2>
        {inviteError && <Alert kind="error">{inviteError}</Alert>}
        {invitations.length === 0 ? (
          <p className="rounded-xl border border-dashed border-border bg-surface px-4 py-3 text-xs text-muted">
            Bekleyen davetiniz yok. Bir stüdyo sizi davet ettiğinde burada görünür.
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
                    disabled={!verified || inviteBusyId !== null}
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
                    Kabul etmek için önce e-posta adresinizi doğrulayın.
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Öğrenci kodu ile başvuru */}
      <form onSubmit={submit} className="space-y-4">
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
            disabled={!verified}
            required
          />
        </FormField>

        <p className="text-xs text-muted">Hocalar kodla başvuramaz; bir stüdyonun sizi davet etmesi gerekir.</p>

        <Button type="submit" loading={loading} disabled={!verified} className="w-full">
          Öğrenci başvurusu gönder
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
