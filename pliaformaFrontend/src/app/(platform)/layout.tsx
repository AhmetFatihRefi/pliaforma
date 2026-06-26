"use client";

import { useEffect } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { cn } from "@/lib/cn";
import { Brand } from "@/components/layout/Brand";
import { ThemeToggle } from "@/components/layout/ThemeToggle";
import { MobileNav } from "@/components/layout/MobileNav";
import { platformNav as nav, isNavActive } from "@/components/layout/nav-items";
import { Button, ConfirmProvider, ToastProvider } from "@/components/ui";
import { FullscreenLoader } from "@/components/layout/FullscreenLoader";

export default function PlatformLayout({ children }: { children: React.ReactNode }) {
  const { user, loading, logout, mfaSetupRequired } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isPlatformAdmin = user?.globalRole === "PlatformAdmin";

  useEffect(() => {
    if (loading) return;
    if (!user) router.replace("/login");
    else if (mfaSetupRequired) router.replace("/mfa-setup"); // Sprint 31-MFA
    else if (!isPlatformAdmin) router.replace("/dashboard");
  }, [loading, user, mfaSetupRequired, isPlatformAdmin, router]);

  if (loading || !user || mfaSetupRequired || !isPlatformAdmin) return <FullscreenLoader />;

  const onLogout = async () => {
    await logout();
    router.replace("/login");
  };

  return (
    <ToastProvider>
      <ConfirmProvider>
        <div className="flex min-h-dvh bg-background">
      <aside className="hidden w-64 shrink-0 flex-col border-r border-border bg-surface lg:flex">
        <div className="px-6 py-5">
          <Brand />
          <p className="mt-1 text-xs font-medium uppercase tracking-wide text-primary">Platform Yönetimi</p>
        </div>
        <nav className="flex-1 space-y-1 px-3">
          {nav.map((item) => {
            const active = isNavActive(pathname, item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors",
                  active ? "bg-primary-soft text-primary-hover" : "text-muted hover:bg-surface-muted hover:text-foreground",
                )}
              >
                <span className="text-base">{item.icon}</span>
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="px-6 py-4 text-xs text-muted">Platform Yönetimi</div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-16 items-center justify-between gap-2 border-b border-border bg-surface px-4 sm:px-6">
          <div className="flex min-w-0 items-center gap-2">
            <MobileNav items={nav} />
            <span className="truncate text-sm font-medium text-foreground">Platform Yönetim Paneli</span>
          </div>
          <div className="flex shrink-0 items-center gap-3">
            <ThemeToggle />
            <div className="hidden text-right sm:block">
              <p className="text-sm font-medium text-foreground">{user.fullName}</p>
              <p className="text-xs text-muted">Platform Admin</p>
            </div>
            <Button variant="secondary" size="sm" onClick={onLogout}>Çıkış</Button>
          </div>
        </header>
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
        </div>
      </ConfirmProvider>
    </ToastProvider>
  );
}
