"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

export type ToastTone = "success" | "error" | "info" | "warning";

interface ToastItem {
  id: number;
  tone: ToastTone;
  message: string;
  title?: string;
  durationMs: number;
}

export interface ToastApi {
  show: (message: string, opts?: { tone?: ToastTone; title?: string; durationMs?: number }) => void;
  success: (message: string, title?: string) => void;
  error: (message: string, title?: string) => void;
  info: (message: string, title?: string) => void;
  warning: (message: string, title?: string) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within a ToastProvider");
  return ctx;
}

let seq = 0;

const TONE: Record<ToastTone, { accent: string; icon: string; iconWrap: string }> = {
  success: { accent: "bg-success", icon: "✓", iconWrap: "bg-success/15 text-success" },
  error: { accent: "bg-danger", icon: "!", iconWrap: "bg-danger/15 text-danger" },
  warning: { accent: "bg-warning", icon: "!", iconWrap: "bg-warning/15 text-warning" },
  info: { accent: "bg-primary", icon: "i", iconWrap: "bg-primary/15 text-primary" },
};

const DEFAULT_TITLE: Record<ToastTone, string> = {
  success: "Tamam",
  error: "Hata",
  warning: "Uyarı",
  info: "Bilgi",
};

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([]);

  const remove = useCallback((id: number) => setItems((xs) => xs.filter((x) => x.id !== id)), []);

  const show = useCallback<ToastApi["show"]>((message, opts) => {
    const tone = opts?.tone ?? "info";
    const id = ++seq;
    const durationMs = opts?.durationMs ?? (tone === "error" ? 6000 : 4000);
    setItems((xs) => [...xs, { id, tone, message, title: opts?.title, durationMs }]);
  }, []);

  const api = useMemo<ToastApi>(() => ({
    show,
    success: (m, t) => show(m, { tone: "success", title: t }),
    error: (m, t) => show(m, { tone: "error", title: t }),
    info: (m, t) => show(m, { tone: "info", title: t }),
    warning: (m, t) => show(m, { tone: "warning", title: t }),
  }), [show]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="pointer-events-none fixed bottom-4 right-4 z-[70] flex w-[min(92vw,23rem)] flex-col gap-2">
        {items.map((t) => (
          <ToastCard key={t.id} item={t} onClose={() => remove(t.id)} />
        ))}
      </div>
    </ToastContext.Provider>
  );
}

function ToastCard({ item, onClose }: { item: ToastItem; onClose: () => void }) {
  const [shown, setShown] = useState(false);
  const tone = TONE[item.tone];

  useEffect(() => {
    const raf = requestAnimationFrame(() => setShown(true));
    const timer = setTimeout(onClose, item.durationMs);
    return () => { cancelAnimationFrame(raf); clearTimeout(timer); };
  }, [item.durationMs, onClose]);

  return (
    <div
      role="status"
      aria-live="polite"
      className={`pointer-events-auto flex items-stretch overflow-hidden rounded-xl border border-border bg-surface shadow-lg shadow-black/5 transition-all duration-200 ${shown ? "translate-x-0 opacity-100" : "translate-x-3 opacity-0"}`}
    >
      <div className={`w-1 shrink-0 ${tone.accent}`} />
      <div className="flex flex-1 items-start gap-3 px-3 py-3">
        <span className={`mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full text-sm font-bold ${tone.iconWrap}`}>{tone.icon}</span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-foreground">{item.title ?? DEFAULT_TITLE[item.tone]}</p>
          <p className="mt-0.5 break-words text-sm text-muted">{item.message}</p>
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Kapat"
          className="-mr-1 -mt-1 shrink-0 rounded-md px-1.5 py-0.5 text-lg leading-none text-muted transition hover:bg-surface-muted hover:text-foreground"
        >
          ×
        </button>
      </div>
    </div>
  );
}
