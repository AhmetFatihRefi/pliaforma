"use client";

import { Children, isValidElement, useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { cn } from "@/lib/cn";

/* ---------------------------------------------------------------- types */

interface NormalizedOption {
  value: string;
  label: React.ReactNode;
  /** Düz metin etiket — arama ve type-ahead için. */
  text: string;
  disabled?: boolean;
}

export interface SelectOption {
  value: string;
  label: React.ReactNode;
  disabled?: boolean;
}

type CommonProps = {
  /** <option> children yerine veri ile besleme (isteğe bağlı). */
  options?: SelectOption[];
  children?: React.ReactNode;
  className?: string;
  id?: string;
  name?: string;
  disabled?: boolean;
  required?: boolean;
  placeholder?: string;
  /** Seçimi sıfırlayan X butonu göster. */
  clearable?: boolean;
  /** Üstte arama kutusu. Belirtilmezse 8+ seçenekte otomatik açılır. */
  searchable?: boolean;
  "aria-label"?: string;
};

type SingleProps = CommonProps & {
  multiple?: false;
  value?: string;
  /** Native uyumlu — e.target.value çalışır. */
  onChange?: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  onValueChange?: (value: string) => void;
};

type MultiProps = CommonProps & {
  multiple: true;
  value?: string[];
  onValueChange?: (value: string[]) => void;
  onChange?: never;
};

export type SelectProps = SingleProps | MultiProps;

/* ------------------------------------------------------------- helpers */

function flattenText(node: React.ReactNode): string {
  if (node == null || node === false || node === true) return "";
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(flattenText).join("");
  if (isValidElement(node)) return flattenText((node.props as { children?: React.ReactNode }).children);
  return "";
}

function useOptions(children: React.ReactNode, options?: SelectOption[]): NormalizedOption[] {
  return useMemo(() => {
    if (options) {
      return options.map((o) => ({ value: o.value, label: o.label, text: flattenText(o.label), disabled: o.disabled }));
    }
    const out: NormalizedOption[] = [];
    Children.forEach(children, (child) => {
      if (!isValidElement(child)) return;
      const props = child.props as { value?: string | number; children?: React.ReactNode; disabled?: boolean };
      const value = props.value != null ? String(props.value) : flattenText(props.children);
      out.push({ value, label: props.children, text: flattenText(props.children), disabled: props.disabled });
    });
    return out;
  }, [children, options]);
}

/* ----------------------------------------------------------------- icons */

function ChevronIcon({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 20 20"
      className={cn("size-4 shrink-0 text-muted transition-transform duration-200", open && "rotate-180")}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="M6 8l4 4 4-4" />
    </svg>
  );
}

function XIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 20 20" className={cn("size-3.5", className)} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden>
      <path d="M5 5l10 10M15 5L5 15" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 20 20" className="size-4 shrink-0 text-primary" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M4 10l4 4 8-9" />
    </svg>
  );
}

/* --------------------------------------------------------------- popover */

interface PopoverProps {
  anchor: HTMLElement | null;
  onClose: () => void;
  children: React.ReactNode;
}

function Popover({ anchor, onClose, children }: PopoverProps) {
  const ref = useRef<HTMLDivElement>(null);
  const [style, setStyle] = useState<React.CSSProperties>({ visibility: "hidden" });

  const place = useCallback(() => {
    if (!anchor) return;
    const r = anchor.getBoundingClientRect();
    const gap = 6;
    const desired = ref.current?.offsetHeight ?? 280;
    const below = window.innerHeight - r.bottom;
    const openUp = below < Math.min(desired, 280) + gap && r.top > below;
    setStyle({
      position: "fixed",
      left: r.left,
      width: r.width,
      maxHeight: Math.max(160, (openUp ? r.top : below) - gap - 8),
      ...(openUp ? { bottom: window.innerHeight - r.top + gap } : { top: r.bottom + gap }),
      visibility: "visible",
    });
  }, [anchor]);

  useEffect(() => {
    place();
    const onScroll = () => place();
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", onScroll);
    return () => {
      window.removeEventListener("scroll", onScroll, true);
      window.removeEventListener("resize", onScroll);
    };
  }, [place]);

  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (ref.current?.contains(e.target as Node)) return;
      if (anchor?.contains(e.target as Node)) return;
      onClose();
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [anchor, onClose]);

  return createPortal(
    <div
      ref={ref}
      style={style}
      data-select-popover=""
      className="z-50 flex flex-col overflow-hidden rounded-xl border border-border bg-surface shadow-lg shadow-black/5 ring-1 ring-black/5 animate-in"
    >
      {children}
    </div>,
    document.body,
  );
}

/* ---------------------------------------------------------------- Select */

export function Select(props: SelectProps) {
  const {
    options,
    children,
    className,
    id,
    name,
    disabled,
    required,
    placeholder,
    clearable,
    searchable,
    multiple,
  } = props;

  const opts = useOptions(children, options);
  const reactId = useId();
  const listId = `${id ?? reactId}-listbox`;

  const selected = useMemo<string[]>(() => {
    if (multiple) return Array.isArray(props.value) ? props.value : [];
    return props.value ? [String(props.value)] : [];
  }, [multiple, props.value]);

  const mirrorRef = useRef<HTMLSelectElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const [triggerEl, setTriggerEl] = useState<HTMLButtonElement | null>(null);

  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);

  const showSearch = searchable ?? opts.length > 8;

  const visible = useMemo(() => {
    if (!showSearch || !query.trim()) return opts;
    const q = query.toLowerCase();
    return opts.filter((o) => o.text.toLowerCase().includes(q));
  }, [opts, query, showSearch]);

  const emitSingle = useCallback(
    (next: string) => {
      (props as SingleProps).onValueChange?.(next);
      const onChange = (props as SingleProps).onChange;
      if (onChange && mirrorRef.current) {
        mirrorRef.current.value = next;
        onChange({ target: mirrorRef.current, currentTarget: mirrorRef.current } as React.ChangeEvent<HTMLSelectElement>);
      }
    },
    [props],
  );

  const emitMulti = useCallback(
    (next: string[]) => (props as MultiProps).onValueChange?.(next),
    [props],
  );

  const openMenu = useCallback(() => {
    const idx = opts.findIndex((o) => selected.includes(o.value));
    setActive(idx >= 0 ? idx : 0);
    setOpen(true);
  }, [opts, selected]);

  const closeMenu = useCallback(() => {
    setOpen(false);
    setQuery("");
  }, []);

  const choose = useCallback(
    (value: string) => {
      if (multiple) {
        const set = new Set(selected);
        if (set.has(value)) set.delete(value);
        else set.add(value);
        emitMulti([...set]);
      } else {
        emitSingle(value);
        closeMenu();
      }
    },
    [multiple, selected, emitMulti, emitSingle, closeMenu],
  );

  const clear = useCallback(
    (e: React.MouseEvent) => {
      e.stopPropagation();
      if (multiple) emitMulti([]);
      else emitSingle("");
    },
    [multiple, emitMulti, emitSingle],
  );

  // Açılınca arama kutusuna odaklan (yalnızca DOM yan etkisi).
  useEffect(() => {
    if (open && showSearch) queueMicrotask(() => searchRef.current?.focus());
  }, [open, showSearch]);

  // Aktif öğeyi görünür alana kaydır.
  useEffect(() => {
    if (!open) return;
    listRef.current?.querySelector('[data-active="true"]')?.scrollIntoView({ block: "nearest" });
  }, [active, open]);

  const moveActive = useCallback(
    (dir: 1 | -1) => {
      setActive((cur) => {
        const n = visible.length;
        if (!n) return cur;
        let i = cur;
        for (let step = 0; step < n; step++) {
          i = (i + dir + n) % n;
          if (!visible[i]?.disabled) return i;
        }
        return cur;
      });
    },
    [visible],
  );

  const onTriggerKey = (e: React.KeyboardEvent) => {
    if (disabled) return;
    if (!open) {
      if (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        openMenu();
      }
      return;
    }
    switch (e.key) {
      case "ArrowDown":
        e.preventDefault();
        moveActive(1);
        break;
      case "ArrowUp":
        e.preventDefault();
        moveActive(-1);
        break;
      case "Home":
        e.preventDefault();
        setActive(0);
        break;
      case "End":
        e.preventDefault();
        setActive(visible.length - 1);
        break;
      case "Enter":
      case " ": {
        if (e.key === " " && showSearch) break; // boşluk aramaya yazılsın
        e.preventDefault();
        const o = visible[active];
        if (o && !o.disabled) choose(o.value);
        break;
      }
      case "Escape":
        e.preventDefault();
        closeMenu();
        triggerEl?.focus();
        break;
      case "Tab":
        closeMenu();
        break;
    }
  };

  const selectedOpts = opts.filter((o) => selected.includes(o.value) && o.value !== "");
  const hasValue = selectedOpts.length > 0;
  const showClear = clearable && hasValue && !disabled;

  return (
    <div className={cn("relative", className)}>
      {/* Form gönderimi / required doğrulama / e.target.value için gizli native select */}
      <select
        ref={mirrorRef}
        id={id}
        name={name}
        required={required}
        disabled={disabled}
        multiple={multiple}
        value={multiple ? selected : selected[0] ?? ""}
        onChange={() => {}}
        tabIndex={-1}
        aria-hidden
        className="sr-only"
      >
        {!multiple && <option value="" />}
        {opts.map((o) => (
          <option key={o.value} value={o.value}>
            {o.text}
          </option>
        ))}
      </select>

      <button
        ref={setTriggerEl}
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        disabled={disabled}
        onClick={() => {
          if (disabled) return;
          if (open) closeMenu();
          else openMenu();
        }}
        onKeyDown={onTriggerKey}
        className={cn(
          "flex w-full min-h-10 items-center gap-2 rounded-xl border bg-surface px-3.5 py-1.5 text-left text-sm text-foreground transition-colors",
          "hover:border-muted/60 focus:outline-none focus-visible:border-primary focus-visible:ring-2 focus-visible:ring-ring/40",
          open ? "border-primary ring-2 ring-ring/40" : "border-border",
          disabled && "cursor-not-allowed opacity-60",
        )}
      >
        <span className="flex min-w-0 flex-1 flex-wrap items-center gap-1.5">
          {!hasValue && <span className="text-muted">{placeholder ?? "Seçiniz"}</span>}

          {multiple
            ? selectedOpts.map((o) => (
                <span
                  key={o.value}
                  className="inline-flex items-center gap-1 rounded-lg bg-primary-soft px-2 py-0.5 text-xs font-medium text-foreground"
                >
                  {o.label}
                  <span
                    role="button"
                    tabIndex={-1}
                    aria-label="Kaldır"
                    onClick={(e) => {
                      e.stopPropagation();
                      choose(o.value);
                    }}
                    className="text-muted hover:text-danger"
                  >
                    <XIcon className="size-3" />
                  </span>
                </span>
              ))
            : hasValue && <span className="truncate">{selectedOpts[0]?.label}</span>}
        </span>

        {showClear && (
          <span
            role="button"
            tabIndex={-1}
            aria-label="Temizle"
            onClick={clear}
            className="grid size-5 shrink-0 place-items-center rounded-md text-muted transition-colors hover:bg-surface-muted hover:text-danger"
          >
            <XIcon />
          </span>
        )}
        <ChevronIcon open={open} />
      </button>

      {open && (
        <Popover anchor={triggerEl} onClose={closeMenu}>
          {showSearch && (
            <div className="border-b border-border p-2">
              <input
                ref={searchRef}
                value={query}
                onChange={(e) => {
                  setQuery(e.target.value);
                  setActive(0);
                }}
                onKeyDown={onTriggerKey}
                placeholder="Ara…"
                className="w-full rounded-lg bg-surface-muted px-2.5 py-1.5 text-sm text-foreground placeholder:text-muted focus:outline-none"
              />
            </div>
          )}

          <ul ref={listRef} id={listId} role="listbox" aria-multiselectable={multiple} className="overflow-y-auto py-1">
            {visible.length === 0 && <li className="px-3 py-2 text-sm text-muted">Sonuç yok</li>}
            {visible.map((o, i) => {
              const isSel = selected.includes(o.value);
              const isActive = i === active;
              const isPlaceholder = o.value === "";
              return (
                <li
                  key={o.value || `__ph-${i}`}
                  role="option"
                  aria-selected={isSel}
                  aria-disabled={o.disabled}
                  data-active={isActive}
                  onMouseEnter={() => setActive(i)}
                  onClick={() => !o.disabled && choose(o.value)}
                  className={cn(
                    "mx-1 flex cursor-pointer items-center gap-2 rounded-lg px-2.5 py-2 text-sm",
                    isActive && !o.disabled && "bg-primary-soft",
                    isSel && "font-medium text-foreground",
                    isPlaceholder && "text-muted",
                    o.disabled && "cursor-not-allowed opacity-40",
                  )}
                >
                  <span className="min-w-0 flex-1 truncate">{o.label}</span>
                  {isSel && !isPlaceholder && <CheckIcon />}
                </li>
              );
            })}
          </ul>
        </Popover>
      )}
    </div>
  );
}
