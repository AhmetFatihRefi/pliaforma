// Sprint 40: Sidebar (masaüstü) ve MobileNav (drawer) ortak nav verisi + aktif-rota yardımcısı.
// Full AppShell rewrite YOK — yalnız veri + helper paylaşımı.
import type { StudioRole } from "@/lib/types";

export interface NavItem {
  href: string;
  label: string;
  icon: string;
  /** Bu navı görebilecek roller. Boşsa herkese görünür. */
  roles?: StudioRole[];
}

export const MANAGER: StudioRole[] = ["Owner", "StudioAdmin"];
export const STAFF: StudioRole[] = ["Owner", "StudioAdmin", "Instructor"];

export const dashboardNav: NavItem[] = [
  { href: "/dashboard", label: "Panel", icon: "▦" },
  { href: "/dashboard/notifications", label: "Bildirimler", icon: "🔔" },
  { href: "/dashboard/schedule", label: "Ders Programı", icon: "◷", roles: STAFF },
  { href: "/dashboard/find-availability", label: "Boşluk Bul", icon: "⌖", roles: STAFF },
  { href: "/dashboard/students", label: "Öğrenciler", icon: "☺", roles: MANAGER },
  { href: "/dashboard/makeup-credits", label: "Telafi Kredileri", icon: "♺", roles: MANAGER },
  { href: "/dashboard/makeup-requests", label: "Telafi Talepleri", icon: "⏳", roles: STAFF },
  { href: "/dashboard/reports", label: "Raporlar", icon: "▣", roles: MANAGER },
  { href: "/dashboard/payments", label: "Ödemeler", icon: "₺", roles: MANAGER },
  { href: "/dashboard/closures", label: "Kapalı Günler", icon: "⊘", roles: MANAGER },
  { href: "/dashboard/instructors", label: "Hocalar", icon: "✦", roles: MANAGER },
  { href: "/dashboard/class-types", label: "Gruplar", icon: "❀", roles: MANAGER },
  { href: "/dashboard/equipment", label: "Ekipman", icon: "▥", roles: MANAGER },
  { href: "/dashboard/subscription", label: "Abonelik", icon: "◈", roles: ["Owner"] },
  { href: "/dashboard/settings", label: "Ayarlar", icon: "⚙", roles: MANAGER },
  { href: "/dashboard/my-bookings", label: "Programım", icon: "✓", roles: ["Student"] },
  { href: "/dashboard/my-contracts", label: "Sözleşmelerim", icon: "▤", roles: ["Student"] },
  { href: "/dashboard/my-makeup-credits", label: "Telafi Kredilerim", icon: "♺", roles: ["Student"] },
  { href: "/dashboard/my-attendance", label: "Katılım Geçmişim", icon: "✔", roles: ["Student"] },
  { href: "/dashboard/my-payments", label: "Ödemelerim", icon: "₺", roles: ["Student"] },
  { href: "/dashboard/my-profile", label: "Profilim", icon: "♡", roles: ["Student"] },
];

// Rehber en ALTA sabitlenir (herkese görünür).
export const dashboardGuide: NavItem = { href: "/dashboard/guide", label: "Rehber", icon: "📖" };

// Platform (super-admin) nav'ı.
export const platformNav: NavItem[] = [
  { href: "/platform", label: "Genel Bakış", icon: "▦" },
  { href: "/platform/studios", label: "Stüdyolar", icon: "❖" },
  { href: "/platform/plans", label: "Planlar", icon: "◈" },
  { href: "/platform/health", label: "Sistem Sağlığı", icon: "✚" },
  { href: "/platform/security-audit", label: "Güvenlik Denetimi", icon: "🛡" },
];

/** Role göre görünür nav öğeleri. */
export function filterNavByRole(items: NavItem[], role: StudioRole | undefined): NavItem[] {
  return items.filter((item) => !item.roles || (role != null && item.roles.includes(role)));
}

/**
 * Aktif-rota: index rotaları (/dashboard, /platform) için TAM eşleşme; diğerleri için
 * tam eşleşme veya alt-rota (href + "/") öneki — böylece yanlış parent aktif görünmez.
 */
export function isNavActive(pathname: string, href: string): boolean {
  if (href === "/dashboard" || href === "/platform") return pathname === href;
  return pathname === href || pathname.startsWith(href + "/");
}
