import { cn } from "@/lib/cn";

const fieldClasses =
  "w-full h-10 rounded-xl border border-border bg-surface px-3.5 text-sm text-foreground placeholder:text-muted transition-colors focus:border-primary focus:outline-none";

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return <input className={cn(fieldClasses, className)} {...props} />;
}

export function Textarea({ className, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea className={cn(fieldClasses, "h-auto min-h-24 py-2.5", className)} {...props} />;
}
