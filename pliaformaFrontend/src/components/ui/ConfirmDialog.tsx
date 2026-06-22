"use client";

import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { Button } from "./Button";

// Özgün, tema-uyumlu onay modalı + promise tabanlı useConfirm. window.confirm yerine kullanılır.
export type ConfirmTone = "default" | "danger";

export interface ConfirmOptions {
  title: string;
  message?: React.ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
}

type ConfirmFn = (opts: ConfirmOptions) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmFn | null>(null);

export function ConfirmProvider({ children }: { children: React.ReactNode }) {
  const [opts, setOpts] = useState<ConfirmOptions | null>(null);
  const resolverRef = useRef<((ok: boolean) => void) | null>(null);

  const confirm = useCallback<ConfirmFn>(
    (o) =>
      new Promise<boolean>((resolve) => {
        resolverRef.current = resolve;
        setOpts(o);
      }),
    [],
  );

  const settle = useCallback((ok: boolean) => {
    resolverRef.current?.(ok);
    resolverRef.current = null;
    setOpts(null);
  }, []);

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {opts && <ConfirmDialogView opts={opts} onSettle={settle} />}
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const ctx = useContext(ConfirmContext);
  if (!ctx) throw new Error("useConfirm, <ConfirmProvider> içinde kullanılmalı.");
  return ctx;
}

function ConfirmDialogView({ opts, onSettle }: { opts: ConfirmOptions; onSettle: (ok: boolean) => void }) {
  const danger = opts.tone === "danger";
  const [shown, setShown] = useState(false);

  useEffect(() => {
    setShown(true);
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") onSettle(false);
      if (e.key === "Enter") onSettle(true);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onSettle]);

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div
        className={`absolute inset-0 bg-black/40 backdrop-blur-sm transition-opacity duration-200 ${shown ? "opacity-100" : "opacity-0"}`}
        onClick={() => onSettle(false)}
      />
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-title"
        className={`relative w-full max-w-sm overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl transition-all duration-200 ${
          shown ? "scale-100 opacity-100" : "scale-95 opacity-0"
        }`}
      >
        {/* Üst aksanlı şerit — tema rengi (danger → kırmızı, default → dusty rose). */}
        <div className={`h-1.5 w-full ${danger ? "bg-danger" : "bg-primary"}`} />
        <div className="p-6">
          <div className="flex items-start gap-4">
            <div
              className={`flex size-11 shrink-0 items-center justify-center rounded-full ${
                danger ? "bg-danger/12 text-danger" : "bg-primary-soft text-primary"
              }`}
              aria-hidden
            >
              {danger ? (
                <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0Z" />
                  <line x1="12" y1="9" x2="12" y2="13" />
                  <line x1="12" y1="17" x2="12.01" y2="17" />
                </svg>
              ) : (
                <svg viewBox="0 0 24 24" className="size-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <circle cx="12" cy="12" r="9" />
                  <path d="M12 8v4" />
                  <path d="M12 16h.01" />
                </svg>
              )}
            </div>
            <div className="min-w-0 flex-1">
              <h3 id="confirm-title" className="text-base font-semibold text-foreground">{opts.title}</h3>
              {opts.message && <div className="mt-1.5 text-sm leading-relaxed text-muted">{opts.message}</div>}
            </div>
          </div>
          <div className="mt-6 flex justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={() => onSettle(false)}>
              {opts.cancelLabel ?? "Vazgeç"}
            </Button>
            <Button
              autoFocus
              size="sm"
              variant={danger ? "danger" : "primary"}
              onClick={() => onSettle(true)}
            >
              {opts.confirmLabel ?? "Onayla"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
