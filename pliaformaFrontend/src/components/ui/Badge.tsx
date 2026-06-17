import { cn } from "@/lib/cn";

type Tone = "rose" | "neutral" | "success" | "warning" | "danger";

const tones: Record<Tone, string> = {
  rose: "bg-primary-soft text-primary-hover",
  neutral: "bg-surface-muted text-muted",
  success: "bg-success/15 text-success",
  warning: "bg-warning/15 text-warning",
  danger: "bg-danger/15 text-danger",
};

export function Badge({ tone = "neutral", children }: { tone?: Tone; children: React.ReactNode }) {
  return (
    <span className={cn("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium", tones[tone])}>
      {children}
    </span>
  );
}
