import type { MetadataRoute } from "next";

// Sprint 52 — SEO baseline. YALNIZ public rotalar listelenir; login/panel/auth gibi private rotalar
// bilinçli olarak YOKTUR (robots.ts disallow + proxy X-Robots-Tag noindex ile tutarlı).
export default function sitemap(): MetadataRoute.Sitemap {
  return [
    {
      url: "https://pliaforma.com",
      changeFrequency: "monthly",
      priority: 1,
    },
  ];
}
