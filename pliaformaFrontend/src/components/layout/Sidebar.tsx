"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Brand, PliaformaMark } from "./Brand";
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
  // Sprint 26: Hoca müsaitlik modülü ana üründen çıkarıldı (hoca boş/dolu = ders programından).
  // Sprint 26 ek (v1.1.1): global yönetim ekranları yalnız stüdyo yetkilisine; Instructor kendi programını görür.
  { href: "/dashboard/students", label: "Öğrenciler", icon: "☺", roles: MANAGER },
  { href: "/dashboard/makeup-credits", label: "Telafi Kredileri", icon: "♺", roles: MANAGER },
  // Sprint 27/30: telafi dersi talepleri. Hoca da kendi öğrencisi için talep OLUŞTURUR/takip eder (onay yöneticide).
  { href: "/dashboard/makeup-requests", label: "Telafi Talepleri", icon: "⏳", roles: STAFF },
  { href: "/dashboard/reports", label: "Raporlar", icon: "▣", roles: MANAGER },
  // Sprint 18b: Paket Planları ana akıştan kaldırıldı (paket modeli dormant; onboarding sözleşme tabanlı).
  { href: "/dashboard/payments", label: "Ödemeler", icon: "₺", roles: MANAGER },
  { href: "/dashboard/closures", label: "Kapalı Günler", icon: "⊘", roles: MANAGER },
  { href: "/dashboard/instructors", label: "Hocalar", icon: "✦", roles: MANAGER },
  // Sprint 26 ek: ders tipi/grup tanımı stüdyo yetkilisinin işi (Instructor görmez).
  { href: "/dashboard/class-types", label: "Gruplar", icon: "❀", roles: MANAGER },
  // Sprint 32A: ekipman/envanter havuzu (reformer, mat, vb.) — ders tipine bağlanabilir.
  { href: "/dashboard/equipment", label: "Ekipman", icon: "▥", roles: MANAGER },
  { href: "/dashboard/subscription", label: "Abonelik", icon: "◈", roles: ["Owner"] },
  { href: "/dashboard/settings", label: "Ayarlar", icon: "⚙", roles: MANAGER },
  // Sprint 18b: öğrenci self-booking kaldırıldı ("Ders Bul" yok); öğrenci yalnız programını görür.
  { href: "/dashboard/my-bookings", label: "Programım", icon: "✓", roles: ["Student"] },
  { href: "/dashboard/my-contracts", label: "Sözleşmelerim", icon: "▤", roles: ["Student"] },
  { href: "/dashboard/my-makeup-credits", label: "Telafi Kredilerim", icon: "♺", roles: ["Student"] },
  { href: "/dashboard/my-attendance", label: "Katılım Geçmişim", icon: "✔", roles: ["Student"] },
  { href: "/dashboard/my-payments", label: "Ödemelerim", icon: "₺", roles: ["Student"] },
  { href: "/dashboard/my-profile", label: "Profilim", icon: "♡", roles: ["Student"] },
];

// Sprint 30: Rehber en ALTA sabitlenir (herkese görünür).
const GUIDE: NavItem = { href: "/dashboard/guide", label: "Rehber", icon: "📖" };

const STORAGE_KEY = "pf-sidebar-collapsed";

export function Sidebar() {
  const pathname = usePathname();
  const { activeStudio, user } = useAuth();
  const role = activeStudio?.role;
  const isPlatformAdmin = user?.globalRole === "PlatformAdmin";

  const [collapsed, setCollapsed] = useState(false);
  useEffect(() => { setCollapsed(localStorage.getItem(STORAGE_KEY) === "1"); }, []);
  const toggle = () =>
    setCollapsed((c) => { const n = !c; try { localStorage.setItem(STORAGE_KEY, n ? "1" : "0"); } catch {} return n; });

  const visible = nav.filter((item) => !item.roles || (role != null && item.roles.includes(role)));

  const link = (item: NavItem, opts?: { primary?: boolean }) => {
    const active = pathname === item.href;
    return (
      <Link
        key={item.href}
        href={item.href}
        title={collapsed ? item.label : undefined}
        className={cn(
          "flex items-center gap-3 rounded-xl py-2.5 text-sm font-medium transition-colors",
          collapsed ? "justify-center px-0" : "px-3",
          opts?.primary
            ? "text-primary hover:bg-primary-soft"
            : active
              ? "bg-primary-soft text-primary-hover"
              : "text-muted hover:bg-surface-muted hover:text-foreground",
        )}
      >
        <span className="text-base">{item.icon}</span>
        {!collapsed && <span className="truncate">{item.label}</span>}
      </Link>
    );
  };

  return (
    <aside
      className={cn(
        "hidden shrink-0 flex-col border-r border-border bg-surface transition-[width] duration-200 lg:flex",
        collapsed ? "w-16" : "w-64",
      )}
    >
      {/* Başlık + daralt/genişlet düğmesi */}
      <div className={cn("flex py-4", collapsed ? "flex-col items-center gap-2 px-2" : "items-center justify-between px-4")}>
        {collapsed ? <PliaformaMark className="size-8" /> : <Brand />}
        <button
          type="button"
          onClick={toggle}
          aria-label={collapsed ? "Menüyü genişlet" : "Menüyü daralt"}
          title={collapsed ? "Genişlet" : "Daralt"}
          className="grid size-8 shrink-0 place-items-center rounded-lg text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={cn("transition-transform", collapsed && "rotate-180")}>
            <path d="M15 6l-6 6 6 6" />
          </svg>
        </button>
      </div>

      {/* Ana menü (kaydırılabilir) */}
      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-1">
        {visible.map((item) => link(item))}
      </nav>

      {/* Alt sabit bölüm: Rehber (+ varsa Platform Yönetimi) */}
      <div className="space-y-1 border-t border-border px-3 py-3">
        {link(GUIDE)}
        {isPlatformAdmin && link({ href: "/platform", label: "Platform Yönetimi", icon: "❖" }, { primary: true })}
      </div>

      {!collapsed && <div className="px-4 py-3 text-xs text-muted">Pliaforma · Stüdyo Yönetimi</div>}
    </aside>
  );
}
