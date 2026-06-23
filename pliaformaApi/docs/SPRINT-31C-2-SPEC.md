# Sprint 31C-2 — CSP Report-Only → Enforce + Nonce + Report-Sink

Statü: **ONAYLANDI — uygulama** · Zemin: 31A→v1.12 · 31B-1→v1.13 · 31B-2→v1.14 · 31-MFA→v1.15 · 31C-1→v1.16 · HEAD `9ef335a`
Tag önerisi (yeşilse): **v1.17.0-pilot** · **Migration YOK** · Tek sprint.

> Amaç: CSP'yi güvenli şekilde Report-Only'den Enforce'a taşımak — **nonce + strict-dynamic** ile `script-src 'unsafe-inline'`
> kaldırılır. Prod ilk deploy **report-only**; canlı doğrulama sonrası **env-flip** ile enforce (rebuild gerekmez).

## Ortam / mekanizma (doğrulandı)
- **Next.js 16.2.9** / React 19, `output: "standalone"` (SSR Node). Next 16'da middleware **`proxy.ts`** olarak yeniden adlandırıldı.
- `headers()` **async** → root layout `async` olmalı: `const nonce = (await headers()).get('x-nonce')`.
- Next, request'teki `Content-Security-Policy` header'ından `'nonce-…'` parse edip framework/sayfa script'lerine **otomatik** uygular. Tek manuel iş: tema `<script>`'ine `nonce`.
- Tek inline script var: [layout.tsx:22](../src/app/layout.tsx) tema flash script'i. `force-static`/`export const dynamic`/`next/script` YOK → dynamic render temiz.
- nonce → tüm sayfalar dinamik (static optimization kapanır; pilot ölçeğinde kabul).

## Onaylanan kararlar (1–9)
1. **Prod ilk deploy `report-only`**; doğrudan enforce YOK. Staging'de `CSP_MODE=enforce` ile full E2E+console gate. Enforce'a geçiş **runtime env + restart** (rebuild yok).
2. **Report-sink: backend `POST /api/csp-report`** (Next route handler YOK). AllowAnonymous, rate-limit, body-size-limit, **DB/SecurityAuditLog YOK**, ILogger ile sanitize/truncate, **204**, invalid body crash yok, token/secret/querystring/fragment loglanmaz.
3. **Sadece `report-uri /api/csp-report`** (report-to/Reporting-Endpoints YOK → 31D).
4. **`connect-src 'self'`** (prod). https: YOK. Dev'de gerekiyorsa isDev dalında `ws:`.
5. **Dev CSP report-only** (enforce yok). Dev'de `'unsafe-eval'` + `ws:` **yalnız isDev dalında**; prod policy'ye taşmaz.
6. **nginx CSP tamamen Next'e devredilir** — `Content-Security-Policy-Report-Only` satırı KALDIRILIR. nginx'te kalanlar: **HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy, Permissions-Policy**. Yanıtta aynı anda iki CSP header **olmayacak**.
7. **`style-src 'self' 'unsafe-inline'` kalır** (style nonce denenmez). **script-src: unsafe-inline YOK, nonce VAR, strict-dynamic VAR**, dev'de unsafe-eval yalnız isDev dalında.
8. **`upgrade-insecure-requests` EKLENMEZ** (HSTS var; mixed-content gözlemlenmeden eklenmez).
9. **Ayrı `tests/e2e/19-csp.spec.ts`** — console/hydration guard; login/register/dashboard/platform/security-audit/mfa-setup/my-profile. Global console-listener tüm suite'e EKLENMEZ (flakiness). 19-csp full E2E içinde koşar.

## Hedef policy
```
default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self';
img-src 'self' data: blob:; font-src 'self' data:; style-src 'self' 'unsafe-inline';
script-src 'self' 'nonce-<nonce>' 'strict-dynamic'  [dev: + 'unsafe-eval'];
connect-src 'self'  [dev: + ws:]; frame-src 'self'; form-action 'self';
report-uri /api/csp-report;
```

## Uygulama kapsamı
**A. Next CSP proxy** — `src/proxy.ts` (YENİ): per-request nonce, `x-nonce` request header, `CSP_MODE` env (report-only|enforce|disabled), mode'a göre **tek** response CSP header, matcher `api`+static hariç, script-src'e nonce, report-uri.
**B. Root layout** — `src/app/layout.tsx`: async + `headers()` ile `x-nonce` oku + tema script'ine `nonce`.
**C. Report sink** — `Controllers/CspReportController.cs` (YENİ): AllowAnonymous, `[EnableRateLimiting(CspReport)]`, body≤8KB, truncate≤2KB, content-type `application/csp-report|reports+json|json`, sanitize (violated-directive / blocked-uri host+path / document-uri path; querystring+fragment atılır; token-pattern yazılmaz) → ILogger Warning, **204**. Rate-limit policy `RateLimitPolicies.CspReport` + `RateLimitingExtensions`.
**D. nginx** — `deploy/nginx.conf`: CSP-Report-Only satırını kaldır; diğer header'lar kalır; çift CSP yok.
**E. Config** — `docker-compose.prod.yml` frontend `CSP_MODE: ${CSP_MODE:-report-only}` + `deploy/.env.example` `CSP_MODE=report-only` (yorum: doğrulama sonrası enforce).
**F. Tests** — `scripts/csp-smoke.sh` (güncelle) + `tests/e2e/19-csp.spec.ts` (YENİ).
**G. Docs** — bu spec + RISK_REGISTER (P1-6 kapanış) + SECURITY_BASELINE_CHECKLIST + SECURITY_GAP_ANALYSIS + deploy CSP_MODE notu.

## Test planı
**csp-smoke:** nginx CSP üretmiyor · Next tek CSP header · report-only modu yalnız `-Report-Only` · enforce modu yalnız `Content-Security-Policy` · iki header aynı anda yok · script-src nonce+strict-dynamic var, unsafe-inline yok · style-src unsafe-inline var · report-uri var · `POST /api/csp-report`→204 · invalid body crash yok · oversize reddedilir · HTML tema script nonce'lu · framework script nonce runtime doğrula.
**E2E:** `CSP_MODE=enforce` ile 19-csp + full E2E; console'da CSP/hydration error → fail; beyaz ekran → fail.
**Regresyon:** backend build 0/0 · tsc 0 · next build 0 · full smoke 38/38 · audit viewer · tenant isolation · MFA · 31B-2 · metrics.

## Rollback
Birincil: frontend `CSP_MODE=report-only` (runtime env) + restart → anında non-blocking. `disabled` → CSP kalkar. nginx fallback gerekirse geri eklenir. Tag öncesi enforce E2E gate. Beyaz ekran → console `Refused to execute` → report-only'ye dön.

## Kapsam dışı
migration · DB write · SecurityAuditLog CSP storage · report-to · CSP export/viewer · backup encryption · CI scans · DB-level append-only · retention purge · product feature.

## Doğrulama şartları
migration yok · has-pending clean · backend 0/0 · tsc 0 · next build 0 · csp-smoke yeşil · 19-csp E2E yeşil · full E2E yeşil · full smoke yeşil · CSP_MODE report-only/enforce/disabled doğrulandı · rollback report-only çalışıyor · gerçek secret/cert/key yok · commit/push/tag yok.
