import { cn } from "@/lib/cn";

/** Pliaforma kelime markası + yumuşak gül noktası. */
export function Brand({ className }: { className?: string }) {
  return (
    <div className={cn("flex items-center gap-2", className)}>
      <span className="size-7 rounded-xl bg-gradient-to-br from-primary to-primary-hover shadow-sm" />
      <span className="text-lg font-semibold tracking-tight text-foreground">
        Plia<span className="text-primary">forma</span>
      </span>
    </div>
  );
}
