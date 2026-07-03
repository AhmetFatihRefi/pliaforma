import type { MetadataRoute } from "next";

// Sprint 52 — SEO baseline. Public landing indexlenebilir; app içi / auth / panel rotaları crawl edilmez.
// Not: Disallow tek başına "index'e girmez" garantisi değildir — proxy.ts aynı rotalara X-Robots-Tag:
// noindex header'ı da koyar (crawl edilirse de index'e girmez; iki katman birbirini tamamlar).
export default function robots(): MetadataRoute.Robots {
  return {
    rules: [
      {
        userAgent: "*",
        allow: "/",
        disallow: [
          "/api/",
          "/dashboard",
          "/platform",
          "/login",
          "/register",
          "/register-studio",
          "/join-studio",
          "/select-studio",
          "/mfa-setup",
          "/verify-email",
          "/reset-password",
          "/forgot-password",
          "/check-email",
        ],
      },
    ],
    sitemap: "https://pliaforma.com/sitemap.xml",
  };
}
