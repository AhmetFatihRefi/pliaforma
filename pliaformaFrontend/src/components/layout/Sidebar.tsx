"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Brand } from "./Brand";
import { cn } from "@/lib/cn";
import { useAuth } from "@/lib/auth";
import type { StudioRole } from "@/lib/types";

interface NavItem {
  href: string;
  label: string;
  icon: string;
  /** Bu navı görebilecek roller. Boşsa herkese görünür. */
  roles?: StudioRole[];
}

const MANAGER: StudioRole[] = ["Owner", "StudioAdmin"];
const STAFF: StudioRole[] = ["Owner", "StudioAdmin", "Instructor"];

const nav: NavItem[] = [
  { href: "/dashboard", label: "Panel", icon: "▦" },
  { href: "/dashboard/notifications", label: "Bildirimler", icon: "🔔" },
  { href: "/dashboard/schedule", label: "Ders Programı", icon: "◷", roles: STAFF },
  { href: "/dashboard/availability", label: "Müsaitlik", icon: "◴", roles: STAFF },
  { href: "/dashboard/students", label: "Öğrenciler", icon: "☺", roles: STAFF },
  { href: "/dashboard/makeup-credits", label: "Telafi Kredileri", icon: "♺", roles: STAFF },
  { href: "/dashboard/reports", label: "Raporlar", icon: "▣", roles: MANAGER },
  { href: "/dashboard/package-plans", label: "Paket Planları", icon: "▤", roles: MANAGER },
  { href: "/dashboard/payments", label: "Ödemeler", icon: "₺", roles: MANAGER },
  { href: "/dashboard/closures", label: "Kapalı Günler", icon: "⊘", roles: MANAGER },
  { href: "/dashboard/instructors", label: "Hocalar", icon: "✦", roles: MANAGER },
  { href: "/dashboard/class-types", label: "Gruplar", icon: "❀", roles: STAFF },
  { href: "/dashboard/subscription", label: "Abonelik", icon: "◈", roles: ["Owner"] },
  { href: "/dashboard/settings", label: "Ayarlar", icon: "⚙", roles: MANAGER },
  { href: "/dashboard/book-classes", label: "Ders Bul", icon: "✛", roles: ["Student"] },
  { href: "/dashboard/my-bookings", label: "Rezervasyonlarım", icon: "✓", roles: ["Student"] },
  { href: "/dashboard/my-makeup-credits", label: "Telafi Kredilerim", icon: "♺", roles: ["Student"] },
  { href: "/dashboard/my-attendance", label: "Katılım Geçmişim", icon: "✔", roles: ["Student"] },
  { href: "/dashboard/my-packages", label: "Paketlerim", icon: "▤", roles: ["Student"] },
  { href: "/dashboard/my-payments", label: "Ödemelerim", icon: "₺", roles: ["Student"] },
  { href: "/dashboard/my-profile", label: "Profilim", icon: "♡", roles: ["Student"] },
];

export function Sidebar() {
  const pathname = usePathname();
  const { activeStudio, user } = useAuth();
  const role = activeStudio?.role;
  const isPlatformAdmin = user?.globalRole === "PlatformAdmin";

  const visible = nav.filter((item) => !item.roles || (role != null && item.roles.includes(role)));

  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r border-border bg-surface lg:flex">
      <div className="px-6 py-5">
        <Brand />
      </div>
      <nav className="flex-1 space-y-1 px-3">
        {visible.map((item) => {
          const active = pathname === item.href;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors",
                active
                  ? "bg-primary-soft text-primary-hover"
                  : "text-muted hover:bg-surface-muted hover:text-foreground",
              )}
            >
              <span className="text-base">{item.icon}</span>
              {item.label}
            </Link>
          );
        })}
        {isPlatformAdmin && (
          <Link
            href="/platform"
            className="flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium text-primary transition-colors hover:bg-primary-soft"
          >
            <span className="text-base">❖</span>
            Platform Yönetimi
          </Link>
        )}
      </nav>
      <div className="px-6 py-4 text-xs text-muted">Sprint 13 · Abonelik & Lisans</div>
    </aside>
  );
}
