# Pliaforma Mobile

Pliaforma'nın native mobil uygulaması (yönetici-önce; Sprint 54 temeli). Expo SDK 57 + TypeScript +
Expo Router. Çalışma kuralları için `AGENTS.md` (bu klasör) ve kök `AGENTS.md` §10.2'ye bakın.

## Geliştirme

```bash
npm install
npm run typecheck     # tsc --noEmit
npm start             # Expo dev server (Expo Go veya dev build ile açın)
```

Lokal backend'e bağlanmak için (`.env.example`'ı kopyalayın):

```bash
cp .env.example .env
# EXPO_PUBLIC_API_URL=http://<LAN-IP>:5132  (telefon ve bilgisayar aynı ağda olmalı)
```

`EXPO_PUBLIC_API_URL` verilmezse uygulama `https://pliaforma.com`'a bağlanır. Bu değer public'tir;
`EXPO_PUBLIC_*` içine ASLA secret konulmaz.

## Yapı

- `src/app/(auth)/` — login, MFA challenge, kurtarma kodu, MFA kurulumu, stüdyo seçimi
- `src/app/(app)/` — salt-okunur dashboard + hesap
- `src/auth/` — SecureStore token deposu, bellek-içi oturum, tekil-uçuş refresh, state machine
- `src/api/` — typed API client (`api/auth/mobile/*` + salt-okunur stüdyo uçları)

Store/EAS süreçleri bu sprintte kapsam dışıdır ve her zaman ayrı onay gerektirir.
