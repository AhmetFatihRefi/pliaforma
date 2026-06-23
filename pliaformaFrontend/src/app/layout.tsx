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

export const metadata: Metadata = {
  title: "Pliaforma — Pilates Stüdyo Yönetimi",
  description: "Pilates stüdyoları için sade, modern ve premium yönetim platformu.",
};

export default async function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  // Sprint 31C-2: CSP nonce (proxy.ts üretir). Tema flash script'i tek inline script → nonce ile izinli.
  // Next framework script'lerine nonce'u OTOMATİK uygular; manuel iş yalnız bu inline script.
  const nonce = (await headers()).get("x-nonce") ?? undefined;
  return (
    <html lang="tr" className={`${sans.variable} h-full`} suppressHydrationWarning>
      <head>
        <script nonce={nonce} dangerouslySetInnerHTML={{ __html: themeInitScript }} />
      </head>
      <body className="min-h-full font-sans">
        <ThemeProvider>
          <AuthProvider>{children}</AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
