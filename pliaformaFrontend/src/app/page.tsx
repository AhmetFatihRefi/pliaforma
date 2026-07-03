import type { Metadata } from "next";
import { LandingPage } from "@/components/landing/LandingPage";

/* ------------------------------------------------------------------ */
/*  Public landing (route: /) — Sprint 52                              */
/*  Server component: metadata + JSON-LD burada (client'ta export      */
/*  edilemez). UI, client bileşen LandingPage'dedir; ilk HTML'de       */
/*  gerçek içerik SSR ile gelir (SEO). Oturumlu kullanıcı client'ta    */
/*  /dashboard'a yönlendirilir.                                        */
/* ------------------------------------------------------------------ */

export const metadata: Metadata = {
  title: "Pliaforma — Pilates Stüdyo Yönetim Uygulaması",
  description:
    "Pliaforma, pilates stüdyolarının ders programı, öğrenci takibi, telafi süreci ve ödeme yönetimini " +
    "tek panelden yönetmesi için geliştirilen modern bir stüdyo yönetim uygulamasıdır.",
  alternates: { canonical: "/" },
  openGraph: {
    type: "website",
    url: "/",
    siteName: "Pliaforma",
    title: "Pliaforma — Pilates Stüdyo Yönetim Uygulaması",
    description:
      "Ders programı, öğrenci takibi, telafi süreci ve ödeme yönetimi — pilates stüdyonuz için tek panelde.",
    locale: "tr_TR",
  },
  twitter: {
    card: "summary_large_image",
    title: "Pliaforma — Pilates Stüdyo Yönetim Uygulaması",
    description:
      "Ders programı, öğrenci takibi, telafi süreci ve ödeme yönetimi — pilates stüdyonuz için tek panelde.",
  },
};

// Minimal SoftwareApplication JSON-LD — yalnız doğrulanabilir bilgiler; rating/fiyat/kullanıcı sayısı
// gibi kanıtlanmamış iddia YOK. (type=application/ld+json inert'tir; CSP script-src'ten etkilenmez.)
const JSON_LD = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "Pliaforma",
  url: "https://pliaforma.com",
  applicationCategory: "BusinessApplication",
  operatingSystem: "Web",
  inLanguage: "tr",
  description:
    "Pilates stüdyoları için ders programı, öğrenci takibi, telafi süreci ve ödeme yönetimini tek panelden " +
    "sunan stüdyo yönetim uygulaması.",
} as const;

export default function Page() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(JSON_LD) }}
      />
      <LandingPage />
    </>
  );
}
