import { cn } from "@/lib/cn";

export function Select({ className, children, ...props }: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={cn(
        "w-full h-10 rounded-xl border border-border bg-surface px-3.5 text-sm text-foreground transition-colors focus:border-primary focus:outline-none",
        className,
      )}
      {...props}
    >
      {children}
    </select>
  );
}
