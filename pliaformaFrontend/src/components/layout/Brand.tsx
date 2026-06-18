import { cn } from "@/lib/cn";

/** Pliaforma kelime markası + özgün "P" monogramı. */
export function Brand({ className }: { className?: string }) {
  return (
    <div className={cn("flex items-center gap-2", className)}>
      <PliaformaMark className="size-7" />
      <span className="text-lg font-semibold tracking-tight text-foreground">
        Plia<span className="text-primary">forma</span>
      </span>
    </div>
  );
}

/** Tema duyarlı logo işareti (gül kurusu degrade — açık/koyu temaya uyar). */
export function PliaformaMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" className={className} fill="none" aria-hidden="true">
      <defs>
        <linearGradient id="pliaforma-mark" x1="4" y1="4" x2="44" y2="44" gradientUnits="userSpaceOnUse">
          <stop stopColor="var(--ring)" />
          <stop offset="1" stopColor="var(--primary-hover)" />
        </linearGradient>
      </defs>
      <rect x="1" y="1" width="46" height="46" rx="14" fill="url(#pliaforma-mark)" />
      <path
        d="M18 35V14.5c0-.55.45-1 1-1h6.2c5 0 8.3 2.9 8.3 7.3 0 4.4-3.3 7.3-8.3 7.3H22"
        fill="none"
        stroke="#fff"
        strokeWidth="4.1"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="14.6" cy="33.9" r="2.5" fill="#fff" fillOpacity="0.92" />
    </svg>
  );
}
