// Sprint 40: Sidebar (masaüstü) ve MobileNav (drawer) ortak nav verisi + aktif-rota yardımcısı.
// Sprint 52: emoji/unicode ikonlar lucide-react bileşenleriyle değiştirildi — farklı glyph metrikleri
// y-ekseninde hiza bozuyordu; tek icon set + sabit boyutlu wrapper (NavIcon) ile tutarlı hiza sağlanır.
import type { LucideIcon } from "lucide-react";
import {
  Activity,
  Banknote,
  Bell,
  BookOpen,
  Building2,
  CalendarCheck,
  CalendarDays,
  CalendarOff,
  CalendarSearch,
  ChartColumn,
  CreditCard,
  Dumbbell,
  FileText,
  Flower2,
  GraduationCap,
  History,
  Hourglass,
  LayoutDashboard,
  RotateCcw,
  Settings,
  ShieldCheck,
  UserRound,
  Users,
} from "lucide-react";
import type { StudioRole } from "@/lib/types";

export interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Bu navı görebilecek roller. Boşsa herkese görünür. */
  roles?: StudioRole[];
}

export const MANAGER: StudioRole[] = ["Owner", "StudioAdmin"];
export const STAFF: StudioRole[] = ["Owner", "StudioAdmin", "Instructor"];

export const dashboardNav: NavItem[] = [
  { href: "/dashboard", label: "Panel", icon: LayoutDashboard },
  { href: "/dashboard/notifications", label: "Bildirimler", icon: Bell },
  { href: "/dashboard/schedule", label: "Ders Programı", icon: CalendarDays, roles: STAFF },
  { href: "/dashboard/find-availability", label: "Boşluk Bul", icon: CalendarSearch, roles: STAFF },
  { href: "/dashboard/students", label: "Öğrenciler", icon: Users, roles: MANAGER },
  { href: "/dashboard/makeup-credits", label: "Telafi Kredileri", icon: RotateCcw, roles: MANAGER },
  { href: "/dashboard/makeup-requests", label: "Telafi Talepleri", icon: Hourglass, roles: STAFF },
  { href: "/dashboard/reports", label: "Raporlar", icon: ChartColumn, roles: MANAGER },
  { href: "/dashboard/payments", label: "Ödemeler", icon: Banknote, roles: MANAGER },
  { href: "/dashboard/closures", label: "Kapalı Günler", icon: CalendarOff, roles: MANAGER },
  { href: "/dashboard/instructors", label: "Hocalar", icon: GraduationCap, roles: MANAGER },
  { href: "/dashboard/class-types", label: "Gruplar", icon: Flower2, roles: MANAGER },
  { href: "/dashboard/equipment", label: "Ekipman", icon: Dumbbell, roles: MANAGER },
  { href: "/dashboard/subscription", label: "Abonelik", icon: CreditCard, roles: ["Owner"] },
  { href: "/dashboard/settings", label: "Ayarlar", icon: Settings, roles: MANAGER },
  { href: "/dashboard/my-bookings", label: "Programım", icon: CalendarCheck, roles: ["Student"] },
  { href: "/dashboard/my-contracts", label: "Sözleşmelerim", icon: FileText, roles: ["Student"] },
  { href: "/dashboard/my-makeup-credits", label: "Telafi Kredilerim", icon: RotateCcw, roles: ["Student"] },
  { href: "/dashboard/my-attendance", label: "Katılım Geçmişim", icon: History, roles: ["Student"] },
  { href: "/dashboard/my-payments", label: "Ödemelerim", icon: Banknote, roles: ["Student"] },
  { href: "/dashboard/my-profile", label: "Profilim", icon: UserRound, roles: ["Student"] },
];

// Rehber en ALTA sabitlenir (herkese görünür).
export const dashboardGuide: NavItem = { href: "/dashboard/guide", label: "Rehber", icon: BookOpen };

// Platform Yönetimi linki (Sidebar alt bölümü + Header mobil drawer footer'ı ortak kullanır).
export const platformAdminLink: NavItem = { href: "/platform", label: "Platform Yönetimi", icon: Building2 };

// Platform (super-admin) nav'ı.
export const platformNav: NavItem[] = [
  { href: "/platform", label: "Genel Bakış", icon: LayoutDashboard },
  { href: "/platform/studios", label: "Stüdyolar", icon: Building2 },
  { href: "/platform/plans", label: "Planlar", icon: CreditCard },
  { href: "/platform/health", label: "Sistem Sağlığı", icon: Activity },
  { href: "/platform/security-audit", label: "Güvenlik Denetimi", icon: ShieldCheck },
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
