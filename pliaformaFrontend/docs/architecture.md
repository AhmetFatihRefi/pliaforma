# Frontend Mimarisi

Next.js 16 (App Router) + TypeScript + Tailwind v4. Harici state kütüphanesi yok — React Context +
hook'lar yeterli.

## Klasör yapısı
```
src/
├── app/
│   ├── (auth)/        login, register-studio, select-studio (ortak ortalı layout)
│   ├── (dashboard)/   korumalı layout (Sidebar+Header) + dashboard
│   ├── layout.tsx     ThemeProvider + AuthProvider + flash önleyici script
│   └── page.tsx       auth durumuna göre yönlendirir
├── components/
│   ├── ui/            tasarım sistemi primitive'leri
│   └── layout/        Sidebar, Header, ThemeToggle, Brand, FullscreenLoader
└── lib/
    ├── api.ts         fetch wrapper (envelope + single-flight refresh)
    ├── tokenStore.ts  access token (BELLEK) + aktif stüdyo id (localStorage)
    ├── services.ts    authService (gerçek) ↔ mock anahtarı
    ├── mock.ts        backend hazır değilken durumlu mock
    ├── auth.tsx       AuthProvider + useAuth
    ├── theme.tsx      ThemeProvider + useTheme (light/dark)
    └── types.ts       backend Auth DTO'larının BİREBİR karşılığı
```

## State & veri
- **AuthProvider:** oturum durumu (user/activeStudio/memberships/loading). İlk yüklemede `/me` çağrısı
  (gerekirse 401→refresh→retry) ile oturum geri yüklenir.
- **ThemeProvider:** light/dark, localStorage + sistem tercihi; flash önleyici inline script.
- **Token:** access token **bellekte** (XSS yüzeyi küçük), refresh token **httpOnly cookie** (JS görmez).

## Backend senkron sözleşmesi
`lib/types.ts` backend `AuthResponse`/`MeResult`/DTO'larını birebir yansıtır. Backend sözleşmesi
değişirse `types.ts` + ilgili `services.ts` çağrıları **aynı PR'da** güncellenir.

## Mock modu
`NEXT_PUBLIC_USE_MOCK=true` iken `services.ts` gerçek API yerine `mock.ts`'e düşer (imzalar birebir).
Backend gelince flag `false` yapılır.
