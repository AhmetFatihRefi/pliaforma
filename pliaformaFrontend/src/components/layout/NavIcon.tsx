import type { LucideIcon } from "lucide-react";

/**
 * Sprint 52 — nav ikonları için tek tip, sabit boyutlu wrapper. Emoji/unicode karışımının yarattığı
 * y-ekseni hiza kaymasını bitirir: her ikon aynı kutuda (size-5), aynı çizim boyutunda (18px) ve
 * flex-center ile hizalanır; shrink-0 uzun label'larda ikonun ezilmesini önler.
 */
export function NavIcon({ icon: Icon }: { icon: LucideIcon }) {
  return (
    <span className="flex size-5 shrink-0 items-center justify-center">
      <Icon className="size-[18px]" strokeWidth={1.8} aria-hidden />
    </span>
  );
}
