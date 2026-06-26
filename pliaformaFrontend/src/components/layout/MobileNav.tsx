"use client";

// Sprint 40: Mobil/tablet (<lg) navigasyon. Hamburger + soldan açılan drawer.
// Kapanma: Escape · dış tıklama · X · rota değişimi. Açıkken body scroll kilidi.
// Parametrik: zaten role-filtrelenmiş `items` (+ opsiyonel `footer`) alır; dashboard ve platform ortak kullanır.
import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/cn";
import { Brand } from "./Brand";
import { isNavActive, type NavItem } from "./nav-items";

export function MobileNav({ items, footer }: { items: NavItem[]; footer?: React.ReactNode }) {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();

  // Escape ile kapat + açıkken body scroll kilidi.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    window.addEventListener("keydown", onKey);
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      window.removeEventListener("keydown", onKey);
      document.body.style.overflow = prev;
    };
  }, [open]);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label="Menüyü aç"
        aria-expanded={open}
        className="grid size-9 shrink-0 place-items-center rounded-lg text-foreground transition-colors hover:bg-surface-muted lg:hidden"
      >
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden>
          <path d="M3 6h18M3 12h18M3 18h18" />
        </svg>
      </button>

      {open && (
        <div className="fixed inset-0 z-50 lg:hidden">
          <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={() => setOpen(false)} />
          <div
            role="dialog"
            aria-modal="true"
            aria-label="Menü"
            className="absolute inset-y-0 left-0 flex w-72 max-w-[82%] flex-col border-r border-border bg-surface shadow-xl animate-in"
          >
            <div className="flex items-center justify-between border-b border-border px-4 py-4">
              <Brand />
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label="Menüyü kapat"
                className="grid size-8 place-items-center rounded-lg text-muted transition-colors hover:bg-surface-muted hover:text-foreground"
              >
                ✕
              </button>
            </div>

            <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-2">
              {items.map((item) => {
                const active = isNavActive(pathname, item.href);
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={() => setOpen(false)}
                    className={cn(
                      "flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors",
                      active ? "bg-primary-soft text-primary-hover" : "text-muted hover:bg-surface-muted hover:text-foreground",
                    )}
                  >
                    <span className="text-base">{item.icon}</span>
                    <span className="truncate">{item.label}</span>
                  </Link>
                );
              })}
            </nav>

            {footer && <div className="space-y-1 border-t border-border px-3 py-3">{footer}</div>}
          </div>
        </div>
      )}
    </>
  );
}
