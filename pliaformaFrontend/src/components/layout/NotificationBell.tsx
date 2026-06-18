"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { notificationService } from "@/lib/studioApi";
import { type NotificationDto, notificationTypeLabels } from "@/lib/domain-types";

const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const [unread, setUnread] = useState(0);
  const [items, setItems] = useState<NotificationDto[]>([]);
  const ref = useRef<HTMLDivElement>(null);
  const router = useRouter();

  const loadCount = useCallback(() => {
    notificationService.unreadCount().then(setUnread).catch(() => {});
  }, []);

  useEffect(() => {
    loadCount();
    const t = setInterval(loadCount, 60000);
    return () => clearInterval(t);
  }, [loadCount]);

  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);

  const toggle = async () => {
    const next = !open;
    setOpen(next);
    if (next) {
      try { setItems(await notificationService.mine()); } catch { setItems([]); }
    }
  };

  const markRead = async (n: NotificationDto) => {
    if (!n.isRead) {
      try {
        await notificationService.markRead(n.id);
        setItems((xs) => xs.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
        loadCount();
      } catch { /* yut */ }
    }
    // Davet bildirimi → Davetlerim bölümüne götür (Sprint 17 derin linki).
    if (n.type === "MembershipInvite") {
      setOpen(false);
      router.push("/join-studio?tab=invitations");
    }
  };

  const markAll = async () => {
    try {
      await notificationService.markAllRead();
      setItems((xs) => xs.map((x) => ({ ...x, isRead: true })));
      setUnread(0);
    } catch { /* yut */ }
  };

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={toggle}
        className="relative flex size-9 items-center justify-center rounded-xl text-foreground hover:bg-surface-muted"
        aria-label="Bildirimler"
      >
        <span className="text-lg">🔔</span>
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex min-w-4 items-center justify-center rounded-full bg-danger px-1 text-[10px] font-semibold text-white">
            {unread > 9 ? "9+" : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-20 mt-2 w-80 overflow-hidden rounded-xl border border-border bg-surface shadow-lg">
          <div className="flex items-center justify-between border-b border-border px-3 py-2">
            <span className="text-sm font-semibold text-foreground">Bildirimler</span>
            <button onClick={markAll} className="text-xs text-primary hover:underline">Tümünü okundu yap</button>
          </div>
          <div className="max-h-80 overflow-y-auto">
            {items.length === 0 ? (
              <p className="px-3 py-6 text-center text-sm text-muted">Bildirim yok.</p>
            ) : (
              items.slice(0, 8).map((n) => (
                <button
                  key={n.id}
                  onClick={() => markRead(n)}
                  className={`block w-full border-b border-border px-3 py-2 text-left last:border-0 hover:bg-surface-muted ${n.isRead ? "" : "bg-primary-soft/40"}`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="text-xs font-medium text-foreground">{notificationTypeLabels[n.type]}</span>
                    {!n.isRead && <span className="size-2 shrink-0 rounded-full bg-primary" />}
                  </div>
                  <p className="mt-0.5 text-sm text-foreground">{n.title}</p>
                  <p className="text-xs text-muted">{n.body}</p>
                  <p className="mt-0.5 text-[10px] text-muted">{fmt(n.createdAt)}</p>
                </button>
              ))
            )}
          </div>
          <Link
            href="/dashboard/notifications"
            onClick={() => setOpen(false)}
            className="block border-t border-border px-3 py-2 text-center text-xs font-medium text-primary hover:bg-surface-muted"
          >
            Tümünü gör
          </Link>
        </div>
      )}
    </div>
  );
}
