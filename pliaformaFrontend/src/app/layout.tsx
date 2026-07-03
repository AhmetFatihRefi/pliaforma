import type { Metadata } from "next";
import { Nunito_Sans } from "next/font/google";
import { headers } from "next/headers";
import "./globals.css";
import { ThemeProvider, themeInitScript } from "@/lib/theme";
import { AuthProvider } from "@/lib/auth";

const sans = Nunito_Sans({
  variable: "--font-sans",
  subsets: ["latin"],
  weight: ["400", "500", "600", "700"],
});

// Sprint 52 — SEO baseline: metadataBase canonical/OG URL'lerinin mutlak tabanıdır (Sprint 49 canonical
// host kararı: https://pliaforma.com). Public sayfalar kendi title/description/canonical'ını override eder
// (bkz. app/page.tsx); private rotalar proxy.ts'in X-Robots-Tag: noindex header'ı ile index dışıdır.
export const metadata: Metadata = {
  metadataBase: new URL("https://pliaforma.com"),
  applicationName: "Pliaforma",
  title: {
    default: "Pliaforma — Pilates Stüdyo Yönetimi",
    template: "%s | Pliaforma",
  },
  description: "Pilates stüdyoları için sade, modern ve premium yönetim uygulaması.",
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  // Sprint 31C-2: CSP nonce (proxy.ts üretir). Tema flash script'i tek inline script → nonce ile izinli.
  // Next framework script'lerine nonce'u OTOMATİK uygular; manuel iş yalnız bu inline script.
  const nonce = (await headers()).get("x-nonce") ?? undefined;
  return (
    <html lang="tr" className={`${sans.variable} h-full`} suppressHydrationWarning>
      <head>
        {/* suppressHydrationWarning: React 19 nonce'u client'ta render etmez (güvenlik) → sunucu nonce'u vs
            boş client nonce'u hydration mismatch üretir. Next 16 "preventing-flash-before-hydration" rehberi de
            inline flash script'e bunu ekler: DOM kazanır, script zaten parse anında çalışmıştır (nonce geçerli). */}
        <script nonce={nonce} suppressHydrationWarning dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className="min-h-full font-sans">
        <ThemeProvider>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
