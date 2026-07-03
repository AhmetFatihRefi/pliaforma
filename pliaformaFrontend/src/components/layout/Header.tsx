"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui";
import { ThemeToggle } from "./ThemeToggle";
import { NotificationBell } from "./NotificationBell";
import { MobileNav } from "./MobileNav";
import { NavIcon } from "./NavIcon";
import { dashboardGuide, dashboardNav, filterNavByRole, platformAdminLink } from "./nav-items";
import { studioRoleLabels, globalRoleLabels } from "@/lib/domain-types";

export function Header() {
  const { user, activeStudio, memberships, switchStudio, logout } = useAuth();
  const router = useRouter();
  const role = activeStudio?.role;
  const isPlatformAdmin = user?.globalRole === "PlatformAdmin";

  const onLogout = async () => {
    await logout();
    router.replace("/login");
  };

  // Mobil drawer öğeleri: rol-filtreli nav + rehber (masaüstü sidebar ile aynı set).
  const mobileItems = [...filterNavByRole(dashboardNav, role), dashboardGuide];
  const roleLabel = activeStudio
    ? (role ? studioRoleLabels[role] : "")
    : (user?.globalRole ? globalRoleLabels[user.globalRole] : "");

  return (
    <header className="flex h-16 items-center justify-between gap-2 border-b border-border bg-surface px-4 sm:px-6">
      <div className="flex min-w-0 items-center gap-2 sm:gap-3">
        <MobileNav
          items={mobileItems}
          footer={isPlatformAdmin && (
            <Link href={platformAdminLink.href} className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-primary transition-colors hover:bg-primary-soft">
              <NavIcon icon={platformAdminLink.icon} />
              <span className="truncate">{platformAdminLink.label}</span>
            </Link>
          )}
        />
        <span className="truncate text-sm font-medium text-foreground">
          {activeStudio?.studioName ?? "Stüdyo seçili değil"}
        </span>
        {memberships.length > 1 && (
          <select
            value={activeStudio?.studioId ?? ""}
            onChange={(e) => switchStudio(e.target.value)}
            className="h-8 max-w-[40vw] shrink rounded-lg border border-border bg-surface px-2 text-xs text-foreground focus:border-primary focus:outline-none sm:max-w-none"
            aria-label="Stüdyo değiştir"
          >
            {memberships.map((m) => (
              <option key={m.studioId} value={m.studioId}>
                {m.studioName} · {studioRoleLabels[m.role]}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="flex shrink-0 items-center gap-2 sm:gap-3">
        <NotificationBell />
        <ThemeToggle />
        <div className="hidden text-right sm:block">
          <p className="text-sm font-medium text-foreground">{user?.fullName}</p>
          <p className="text-xs text-muted">{roleLabel}</p>
        </div>
        <Button variant="secondary" size="sm" onClick={onLogout}>
          Çıkış
        </Button>
      </div>
    </header>
  );
}
