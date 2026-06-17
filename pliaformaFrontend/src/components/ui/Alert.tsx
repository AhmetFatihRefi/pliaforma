import { cn } from "@/lib/cn";

type Kind = "error" | "success" | "info" | "warning";

const kinds: Record<Kind, string> = {
  error: "bg-danger/10 text-danger border-danger/20",
  success: "bg-success/10 text-success border-success/20",
  info: "bg-primary-soft text-primary-hover border-primary/20",
  warning: "bg-warning/10 text-warning border-warning/20",
};

export function Alert({ kind = "info", children }: { kind?: Kind; children: React.ReactNode }) {
  return (
    <div className={cn("rounded-xl border px-4 py-3 text-sm", kinds[kind])} role="alert">
      {children}
    </div>
  );
}
