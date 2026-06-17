import type { NextConfig } from "next";

// Same-origin mimari: tarayıcı API'ye DOĞRUDAN gitmez. Tüm `/api/*` (ve `/health`) istekleri
// aynı origin'den gider. Development'ta Next.js bunları backend'e proxy'ler; production'da
// bu proxy'yi nginx üstlenir (frontend rewrite'ı prod'da kullanılmaz, zararsızdır).
const backendOrigin = process.env.BACKEND_ORIGIN ?? "http://localhost:5132";

const nextConfig: NextConfig = {
  // Docker prod imajı için (ince standalone çıktı).
  output: "standalone",
  async rewrites() {
    return [
      { source: "/api/:path*", destination: `${backendOrigin}/api/:path*` },
      { source: "/health", destination: `${backendOrigin}/health` },
    ];
  },
};

export default nextConfig;
