"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Brand, PliaformaMark } from "./Brand";
import { cn } from "@/lib/cn";
import { useAuth } from "@/lib/auth";
import { dashboardGuide as GUIDE, dashboardNav, filterNavByRole, isNavActive, type NavItem } from "./nav-items";

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

  const visible = filterNavByRole(dashboardNav, role);

  const link = (item: NavItem, opts?: { primary?: boolean }) => {
    const active = isNavActive(pathname, item.href);
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
