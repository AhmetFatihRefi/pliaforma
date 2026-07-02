"use client";

import Link from "next/link";
import { useRole } from "@/lib/useRole";
import { useAuth } from "@/lib/auth";
import type { StudioRole } from "@/lib/types";

// Rolüne göre "neler yapabilirsiniz" rehberi. Kullanıcı yalnız KENDİ yapabildiklerini görür; her madde
// ilgili sayfaya bağlanır. UX: rol rozeti + alanlara göre kartlar + net, kısa cümleler.

const ROLE_LABEL: Record<StudioRole, string> = {
  Owner: "Stüdyo Sahibi",
  StudioAdmin: "Stüdyo Yöneticisi",
  Instructor: "Hoca",
  Student: "Öğrenci",
};

const ROLE_INTRO: Record<StudioRole, string> = {
  Owner: "Stüdyonuzun tüm yönetimine sahipsiniz: program, öğrenciler, ödemeler, telafi, raporlar ve abonelik.",
  StudioAdmin: "Stüdyoyu yönetirsiniz: program, öğrenciler, ödemeler, telafi ve raporlar.",
  Instructor: "Kendi ders programınızı yürütür, derslerinizi işler ve telafi talebi oluşturursunuz.",
  Student: "Programınızı, sözleşmelerinizi, telafi haklarınızı, katılımınızı ve ödemelerinizi görürsünüz.",
};

interface Item {
  text: string;
  roles: StudioRole[];
  href?: string;
}
interface Section {
  title: string;
  icon: string;
  items: Item[];
}

const ALL: StudioRole[] = ["Owner", "StudioAdmin", "Instructor", "Student"];
const MANAGER: StudioRole[] = ["Owner", "StudioAdmin"];
const STAFF: StudioRole[] = ["Owner", "StudioAdmin", "Instructor"];

const SECTIONS: Section[] = [
  {
    title: "Ders Programı",
    icon: "◷",
    items: [
      { text: "Haftalık takvimde tüm dersleri görün; yeni ders veya “Toplu ders” oluşturun.", roles: MANAGER, href: "/dashboard/schedule" },
      { text: "Dersi sürükle-bırak ile başka gün/saate taşıyın — bir hoca aynı anda iki ders veremez, çakışma otomatik engellenir.", roles: MANAGER, href: "/dashboard/schedule" },
      { text: "Derse öğrenci ekleyin/çıkarın; ders tipine ve tercih gününe uyan öğrenciler ile telafi hakkı olanlar listelenir.", roles: MANAGER, href: "/dashboard/schedule" },
      { text: "“Bugünü onayla” ile günün başlamış derslerini topluca işleyin; tek tek de onaylayabilirsiniz.", roles: MANAGER, href: "/dashboard/schedule" },
      { text: "Erteleyin, iptal edin, “işlenmedi” (telafi üretir) veya “fiilî hoca” atayın; derse not ekleyin; grup düzenini topluca değiştirin.", roles: MANAGER, href: "/dashboard/schedule" },
      { text: "Kendi haftalık programınızı görün; ders detayını ve kayıtlı öğrencileri inceleyin.", roles: ["Instructor"], href: "/dashboard/schedule" },
      { text: "Kendi derslerinizi onaylayın ve yoklama alın.", roles: ["Instructor"], href: "/dashboard/schedule" },
    ],
  },
  {
    title: "Öğrenciler & Sözleşmeler",
    icon: "☺",
    items: [
      { text: "Öğrenci ekleyin; iletişim, sağlık ve seviye bilgilerini tutun.", roles: MANAGER, href: "/dashboard/students" },
      { text: "Sözleşme ve satır (paket) oluşturun: ders tipi, ders sayısı, fiyat, son ödeme tarihi ve tercih gün/saatleri.", roles: MANAGER, href: "/dashboard/students" },
      { text: "Öğrencinin borç özetini ve katılım geçmişini görün.", roles: MANAGER, href: "/dashboard/students" },
      { text: "Öğrenciyi kullanıcıya dönüştürün; kendi öğrenci paneline giriş yapabilsin.", roles: MANAGER, href: "/dashboard/students" },
    ],
  },
  {
    title: "Ödemeler & Borç",
    icon: "₺",
    items: [
      { text: "Öğrenci bazlı borçları görün: kalan ödeme, gecikmiş borç ve son ödeme tarihi özet olarak gelir; tıklayınca detaylanır.", roles: MANAGER, href: "/dashboard/payments" },
      { text: "Her borcun hangi sözleşmeye ait olduğunu görün; önceki sözleşmelerden kalan borçlar da listelenir.", roles: MANAGER, href: "/dashboard/payments" },
      { text: "Bir sözleşme satırına bağlı alınan ödeme kaydı oluşturun (parça parça ödeme yapılabilir); yanlışsa iptal edin. Kalan/gecikmiş borç son ödeme tarihinden hesaplanır.", roles: MANAGER, href: "/dashboard/payments" },
    ],
  },
  {
    title: "Telafi (Kredi & Talep)",
    icon: "♺",
    items: [
      { text: "Telafi kredisi verin/geri alın; öğrenci bazlı kazanılan/kullanılan/kullanılabilir özetini görün.", roles: MANAGER, href: "/dashboard/makeup-credits" },
      { text: "Telafi dersi talebi oluşturun: öğrenci + uygun satır + saat. Talep onaya gider, saat bloklanır.", roles: STAFF, href: "/dashboard/makeup-requests" },
      { text: "Talepleri onaylayın (gerçek telafi dersi oluşur), düzenleyin veya reddedin.", roles: MANAGER, href: "/dashboard/makeup-requests" },
      { text: "Kendi taleplerinizi takip edin; onaylanmadan önce geri çekebilirsiniz.", roles: ["Instructor"], href: "/dashboard/makeup-requests" },
    ],
  },
  {
    title: "Raporlar",
    icon: "▣",
    items: [
      { text: "Ödeme raporu: tahsil edilen, bekleyen ve geciken tutarlar.", roles: MANAGER, href: "/dashboard/reports" },
      { text: "Hoca iş hacmi: aya göre planlanan, işlenen, yaklaşan, hoca kaynaklı iptal ve katılım — maaş hesabınızı kolaylaştırır.", roles: MANAGER, href: "/dashboard/reports" },
    ],
  },
  {
    title: "Stüdyo Ayarları",
    icon: "⚙",
    items: [
      { text: "Hocaları davet edin ve yönetin (rol ve ders tipi atama).", roles: MANAGER, href: "/dashboard/instructors" },
      { text: "Ders gruplarını (tiplerini) tanımlayın.", roles: MANAGER, href: "/dashboard/class-types" },
      { text: "Kapalı günleri (tatil/izin) belirleyin; o günlere ders düşmez.", roles: MANAGER, href: "/dashboard/closures" },
      { text: "Stüdyo bilgilerini ve ayarlarını düzenleyin.", roles: MANAGER, href: "/dashboard/settings" },
      { text: "Abonelik ve faturalandırmayı yönetin.", roles: ["Owner"], href: "/dashboard/subscription" },
    ],
  },
  {
    title: "Kendi Sayfalarınız",
    icon: "♡",
    items: [
      { text: "Programım: yaklaşan derslerinizi görün; gerektiğinde rezervasyonunuzu iptal edin.", roles: ["Student"], href: "/dashboard/my-bookings" },
      { text: "Sözleşmelerim: paketleriniz, kalan ders haklarınız ve planlanan dersleriniz.", roles: ["Student"], href: "/dashboard/my-contracts" },
      { text: "Telafi Kredilerim: telafi haklarınız ve kullanımları.", roles: ["Student"], href: "/dashboard/my-makeup-credits" },
      { text: "Katılım Geçmişim: geçmiş derslerdeki katılımınız.", roles: ["Student"], href: "/dashboard/my-attendance" },
      { text: "Ödemelerim: borç ve ödeme durumunuz.", roles: ["Student"], href: "/dashboard/my-payments" },
      { text: "Profilim: bilgilerinizi güncelleyin.", roles: ["Student"], href: "/dashboard/my-profile" },
    ],
  },
  {
    title: "Her Zaman",
    icon: "🔔",
    items: [
      { text: "Panel: günün özeti ve dikkat gerektirenler.", roles: ALL, href: "/dashboard" },
      { text: "Bildirimler: yaklaşan dersler, ödemeler ve önemli aksiyonlar.", roles: ALL, href: "/dashboard/notifications" },
    ],
  },
];

export default function GuidePage() {
  const { role } = useRole();
  const { user } = useAuth();
  if (!role) return null;

  const sections = SECTIONS
    .map((s) => ({ ...s, items: s.items.filter((i) => i.roles.includes(role)) }))
    .filter((s) => s.items.length > 0);

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      {/* Üst kart: rol + giriş */}
      <div className="rounded-2xl border border-border bg-gradient-to-br from-primary-soft/70 to-surface p-6">
        <div className="flex flex-wrap items-center gap-3">
          <span className="inline-flex items-center gap-2 rounded-full bg-primary px-3 py-1 text-sm font-bold text-primary-foreground">
            {role && ROLE_LABEL[role]}
          </span>
          {user?.fullName && <span className="text-sm text-muted">Merhaba, {user.fullName}</span>}
        </div>
        <h1 className="mt-3 text-2xl font-semibold text-foreground">Neler yapabilirsiniz?</h1>
        <p className="mt-1 max-w-2xl text-sm text-muted">{role && ROLE_INTRO[role]}</p>
        {(role === "Owner" || role === "StudioAdmin") && (
          <p className="mt-3 rounded-xl border border-border bg-surface px-3 py-2 text-sm text-muted">
            <span className="font-medium text-foreground">Aynı zamanda ders veriyorsanız:</span> Ders Programı’nda size atanan dersleri de
            görür, onaylar ve yoklama alırsınız — yönetim yetkileriniz aynen devam eder.
          </p>
        )}
      </div>

      {/* Alanlara göre yapabilecekler */}
      {sections.map((s) => (
        <div key={s.title} className="rounded-2xl border border-border bg-surface p-5">
          <div className="mb-3 flex items-center gap-2.5">
            <span className="grid size-9 place-items-center rounded-xl bg-primary-soft text-lg text-primary">{s.icon}</span>
            <h2 className="text-lg font-semibold text-foreground">{s.title}</h2>
          </div>
          <ul className="space-y-2.5">
            {s.items.map((it, i) => (
              <li key={i} className="flex items-start gap-3">
                <span className="mt-0.5 grid size-5 shrink-0 place-items-center rounded-full bg-success/15 text-success">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round"><path d="M20 6 9 17l-5-5" /></svg>
                </span>
                <span className="flex-1 text-sm leading-relaxed text-foreground">
                  {it.text}
                  {it.href && (
                    <Link href={it.href} className="ml-2 whitespace-nowrap text-xs font-semibold text-primary hover:underline">Aç →</Link>
                  )}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ))}
    </div>
  );
}
