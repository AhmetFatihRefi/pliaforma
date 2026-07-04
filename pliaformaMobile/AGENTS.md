# AGENTS.md — Pliaforma Mobile Working Rules

This file complements the repository root `AGENTS.md` (the official working protocol) and MUST NOT
conflict with it. Root `AGENTS.md` §10.2 "Mobile Auth Security Rules" applies verbatim to this app.

## Expo HAS CHANGED

Read the exact versioned docs at https://docs.expo.dev/versions/v57.0.0/ before writing any code.
This project uses Expo SDK 57 / React Native 0.86 / Expo Router (routes live in `src/app/`).

## Token & secret rules (hard requirements)

- Tokens must NEVER be stored in AsyncStorage.
- The refresh token is stored ONLY in `expo-secure-store` (iOS Keychain / Android Keystore).
- The trusted-device token is stored ONLY in `expo-secure-store`.
- All SecureStore access goes through `src/auth/tokenStore.ts` — no other module may call SecureStore.
- The access token lives in memory only (`src/auth/session.ts`); after app restart it is re-obtained
  via the refresh token.
- Passwords are NEVER persisted anywhere, in any form.
- Tokens, secrets, passwords, and auth responses are NEVER logged (no `console.*`, no crash/debug dumps).
- The API base URL is public information, not a secret.
- `EXPO_PUBLIC_*` variables are compiled into the bundle and are readable by anyone —
  NEVER put a secret in them.
- No debug logging against production user data. Screenshots and logs shared anywhere must not
  contain tokens or personal data.

## Auth transport rules

- Web LOGIN/REFRESH transport endpoints are FORBIDDEN in this app — in particular
  `/api/auth/refresh` and `/api/auth/login` (cookie-based transport) must never be called.
- Login, refresh, logout, MFA challenge and recovery-code verification use the mobile transport:
  `api/auth/mobile/*` (body-based, channel-bound tokens).
- Spec-approved shared BEARER endpoints are allowed (they are cookie-free):
  `/api/auth/mfa/setup/start`, `/api/auth/switch-studio`, and `/api/auth/me` if needed.
- No cookies: `credentials: 'include'` and any cookie handling are FORBIDDEN;
  the fetch wrapper pins `credentials: 'omit'` as defense-in-depth.
- Refresh is single-flight; a request retries at most once after refresh; no unbounded 401 retry.
- After rotation the new refresh token is written to SecureStore BEFORE it is used in memory.
- Logout always clears SecureStore (refresh + trusted tokens) and the in-memory session,
  even if the backend call fails.
- Machine-consumed expiry values use the `*Unix` epoch fields (+ JWT `exp`) — never parse the
  offset-less DateTime strings.

## Release rules

- EAS builds require explicit human approval.
- Store submission (App Store / Play Store) is a production deploy: it requires its own explicit
  approval, exactly like a backend deploy.
- Mobile release tags use the `mobile-vX.Y.Z-pilot` prefix; backend/web tags (`vX.Y.Z-pilot`)
  are never reused for mobile releases.
- No commit/push/tag without explicit gatekeeper approval (root `AGENTS.md` §3.3).

## Layout

```text
src/app/        Expo Router routes ((auth) and (app) groups)
src/api/        http.ts (base), client.ts (typed client), types.ts (backend contract mirror)
src/auth/       tokenStore.ts (SecureStore ONLY here), session.ts (memory), refresh.ts, AuthContext.tsx
src/ui/         minimal design primitives + theme tokens (no UI libraries)
```

`src/api/types.ts` mirrors backend DTOs by hand; when the backend contract changes, update it in the
same PR (same rule as `pliaformaFrontend`).
