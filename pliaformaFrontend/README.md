# Pliaforma Frontend

Pilates stüdyoları için multi-tenant SaaS — frontend. Next.js 16 (App Router) + TypeScript +
Tailwind v4. Sade, ferah, premium UI (Blush/Rose + krem); light/dark tema.

## Hızlı başlangıç
```bash
cp .env.example .env.local   # NEXT_PUBLIC_API_URL ve mock anahtarı
npm install
npm run dev                  # http://localhost:3000
```

## Same-origin `/api`
API'ye **doğrudan gidilmez**; istekler relative `/api/*` ile aynı origin'den gider.
- **Dev:** `next.config.ts` rewrite `/api/*` → `BACKEND_ORIGIN` (varsayılan `http://localhost:5132`).
- **Prod:** nginx `/api/`'ı backend'e yönlendirir (frontend rewrite kullanılmaz).
Refresh cookie bu sayede same-origin (first-party) çalışır; mimari CORS'a bağımlı değildir.

## Ortam değişkenleri
- `NEXT_PUBLIC_API_URL` — **boş** (same-origin, önerilen). Yalnızca doğrudan cross-origin testi için tam URL.
- `BACKEND_ORIGIN` — dev proxy hedefi (sunucu tarafı; client bundle'a sızmaz)
- `NEXT_PUBLIC_USE_MOCK` — `true` ise backend olmadan mock servisle çalışır

## Compose ile tüm stack
```bash
docker compose -f ../docker-compose.dev.yml up --build   # http://localhost:3000
```

## Yapı
- `src/app/(auth)` — login, register-studio, select-studio
- `src/app/(dashboard)` — korumalı panel (Sidebar + Header)
- `src/components/ui` — tasarım sistemi · `src/components/layout` — kabuk
- `src/lib` — api, auth, theme, services, mock, types, tokenStore

## Güvenlik
Access token **bellekte**, refresh token **httpOnly cookie** (backend yönetir). localStorage'da yalnızca
aktif stüdyo id'si tutulur.

## Dokümantasyon
[docs/architecture.md](docs/architecture.md) · [docs/design-system.md](docs/design-system.md).
Backend Auth DTO'ları `src/lib/types.ts`'te birebir yansıtılır (senkron sözleşme).
