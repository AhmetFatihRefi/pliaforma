"use client";

import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui";
import { ThemeToggle } from "./ThemeToggle";
import { NotificationBell } from "./NotificationBell";

export function Header() {
  const { user, activeStudio, memberships, switchStudio, logout } = useAuth();
  const router = useRouter();

  const onLogout = async () => {
    await logout();
    router.replace("/login");
  };

  return (
    <header className="flex h-16 items-center justify-between border-b border-border bg-surface px-6">
      <div className="flex items-center gap-3">
        <span className="text-sm font-medium text-foreground">
          {activeStudio?.studioName ?? "Stüdyo seçili değil"}
        </span>
        {memberships.length > 1 && (
          <select
            value={activeStudio?.studioId ?? ""}
            onChange={(e) => switchStudio(e.target.value)}
            className="h-8 rounded-lg border border-border bg-surface px-2 text-xs text-foreground focus:border-primary focus:outline-none"
            aria-label="Stüdyo değiştir"
          >
            {memberships.map((m) => (
              <option key={m.studioId} value={m.studioId}>
                {m.studioName} · {m.role}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="flex items-center gap-3">
        <NotificationBell />
        <ThemeToggle />
        <div className="hidden text-right sm:block">
          <p className="text-sm font-medium text-foreground">{user?.fullName}</p>
          <p className="text-xs text-muted">{activeStudio?.role ?? user?.globalRole}</p>
        </div>
        <Button variant="secondary" size="sm" onClick={onLogout}>
          Çıkış
        </Button>
      </div>
    </header>
  );
}
