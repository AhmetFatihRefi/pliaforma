"use client";

import { useEffect, useId, useRef } from "react";
import { cn } from "@/lib/cn";

const sizeClass = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-2xl",
  xl: "max-w-4xl",
} as const;

const FOCUSABLE = 'a[href], button, input, select, textarea, [tabindex]';

export function Modal({
  open,
  onClose,
  title,
  children,
  size = "md",
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: React.ReactNode;
  size?: keyof typeof sizeClass;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const prevFocusRef = useRef<HTMLElement | null>(null);
  const titleId = useId();

  // onClose çağrı yerlerinde genelde inline fonksiyon → her render'da yeni referans. Focus-trap effect'i
  // buna bağlanırsa her tuş vuruşunda (parent re-render) cleanup+re-run olur ve odağı input'tan çalar.
  // Ref'te tutup effect'i yalnız `open`'a bağlıyoruz (latest-callback pattern) → odak korunur.
  const onCloseRef = useRef(onClose);
  useEffect(() => { onCloseRef.current = onClose; }, [onClose]);

  useEffect(() => {
    if (!open) return;
    // Açılışta odaktaki öğeyi sakla → kapanışta geri ver.
    prevFocusRef.current = document.activeElement as HTMLElement | null;

    const dialog = dialogRef.current;
    const focusables = () =>
      Array.from(dialog?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(
        (el) => el.tabIndex !== -1 && !(el as HTMLButtonElement).disabled && el.getAttribute("aria-hidden") !== "true" && el.offsetParent !== null,
      );

    // İlk odak modal içine (ilk öğe yoksa dialog'un kendisine).
    queueMicrotask(() => (focusables()[0] ?? dialog)?.focus());

    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") { onCloseRef.current(); return; }
      if (e.key !== "Tab") return;
      const active = document.activeElement as HTMLElement | null;
      // Select popover'ı body'ye portal ile render olur; odak orada ise Tab'ı Select yönetir (kapanır).
      if (active?.closest("[data-select-popover]")) return;
      const f = focusables();
      if (f.length === 0) { e.preventDefault(); dialog?.focus(); return; }
      const first = f[0];
      const last = f[f.length - 1];
      if (!dialog?.contains(active)) { e.preventDefault(); first.focus(); return; }
      if (e.shiftKey && active === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && active === last) { e.preventDefault(); first.focus(); }
    };
    document.addEventListener("keydown", onKey, true);
    return () => {
      document.removeEventListener("keydown", onKey, true);
      prevFocusRef.current?.focus?.();
    };
  }, [open]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto p-4 sm:items-center">
      <div className="absolute inset-0 bg-black/30 backdrop-blur-sm" onClick={onClose} />
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={cn("relative my-auto flex max-h-[90vh] w-full flex-col rounded-2xl border border-border bg-surface shadow-xl focus:outline-none", sizeClass[size])}
      >
        <div className="flex items-center justify-between border-b border-border px-6 py-4">
          <h3 id={titleId} className="text-base font-semibold text-foreground">{title}</h3>
          <button
            onClick={onClose}
            aria-label="Kapat"
            className="rounded-lg p-1 text-muted hover:bg-surface-muted hover:text-foreground"
          >
            ✕
          </button>
        </div>
        <div className="overflow-y-auto px-6 py-5">{children}</div>
      </div>
    </div>
  );
}
