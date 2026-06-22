# Sprint 31B-1 — Infra/Config Security Hardening (migration YOK)

Statü: **UYGULANIYOR** · Önceki: Sprint 31A (Evidence Baseline, `v1.12.0-pilot`) + observability feature (`c5e81f2`, tag yok).
Tag önerisi (yeşil olursa): **`v1.13.0-pilot`**. Sonraki: Sprint 31B-2 (Auth/App Hardening, migration var) → `v1.14.0-pilot`.

> Bölme kararı: Sprint 31B ikiye ayrıldı. **31B-1 = infra/config (migration yok, deploy-blocker'lar)**,
> **31B-2 = auth/app (migration var)**. Detaylı analiz: [security/SECURITY_GAP_ANALYSIS.md](security/SECURITY_GAP_ANALYSIS.md),
> [security/RISK_REGISTER.md](security/RISK_REGISTER.md).

## Amaç
Pilot-deploy güvenlik blocker'larını kapatmak — **DB/migration'a dokunmadan**:
- **P0-3** — `/metrics` + Grafana prod exposure'ı kapat
- **P0-1** — TLS/HTTPS + HSTS (prod)
- **P0-4** — prod secret/seed guard (CHANGE_ME/zayıf değer → Production'da fail-fast)

## Kapsam (yapılacaklar)

### A. /metrics + Grafana hardening (P0-3)
- `deploy/nginx.conf`: defansif `location = /metrics { return 404; }` (public sızıntı kesin kapalı).
- `docker-compose.monitoring.yml`: port bind'leri `127.0.0.1:` (loopback); Grafana admin parolası **env zorunlu** (default `admin` kaldırıldı); anonymous **varsayılan kapalı**; embedding **varsayılan kapalı** (env ile açılır).
- `platform/health/page.tsx`: hardcoded `localhost` linkleri **env-bazlı** (`NEXT_PUBLIC_GRAFANA_URL` vb.); env yoksa monitoring kartı **gizli** (prod'da görünmez); **"admin/admin" metni kaldırıldı**.
- Varsayım: **pilot prod'da Prometheus/Grafana ÇALIŞMAYACAK.** Çalıştırılırsa loopback + auth zorunlu.

### B. TLS/HTTPS/HSTS (P0-1)
- `deploy/nginx.conf`: 443 `server` bloğu aktif (ssl + http2); 80 → `301 https`; **HSTS yalnız 443'te** (başta kısa `max-age`).
- Cert path'leri **placeholder** (`/etc/nginx/certs/*.pem`); **gerçek sertifika/gerçek domain repo'ya GİRMEZ.**
- `docker-compose.prod.yml`: `443:443` portu + `./deploy/certs:/etc/nginx/certs:ro` volume açık.
- `PUBLIC_ORIGIN=https://...` + CORS prod origin https → `.env.example`'da dokümante (zaten https).
- Secure refresh cookie HTTPS'te çalışır (env-bazlı, mevcut).
- Certbot/sertifika adımları runbook'a eklendi.

### C. Prod secret / seed guard (P0-4)
- Backend startup guard (Production): `Jwt:Secret` veya `Seed:AdminPassword` "CHANGE_ME" içeriyorsa ya da bilinen zayıf örnekse (`Admin!2345`) **fail-fast**.
  - **Development bozulmaz** (yalnız Production).
  - `Seed:AdminPassword` boşsa → admin tohumlanmaz → guard atlanır (false-positive yok).
- `deploy/.env.example`: uyarılar güçlendirildi; `GF_ADMIN_PASSWORD` örneği (placeholder) eklendi.
- `SECURITY_BASELINE_CHECKLIST.md` P0-4 güncellendi.

### D. Junk cleanup
- `deploy/nginx.conf;C` boş/junk dizini silindi.

### E. Dokümantasyon
- `docs/security/RISK_REGISTER.md`, `SECURITY_BASELINE_CHECKLIST.md`, `SECURITY_GAP_ANALYSIS.md` 31B-1 ile ilgili kısımlar güncel.
- `docs/PILOT-DEPLOY-DRY-RUN.md` TLS/certbot adımları.

## Kapsam DIŞI (31B-2 / 31C / 31D / 31-MFA)
SecurityAuditLog entity/table, migration, login/failed-login audit, parola politikası, hesap kilitleme,
change-password, CSP, MFA, tenant isolation testleri, backup encryption, dependency/secret scan CI, token purge,
anonymization, ürün feature değişiklikleri.

## Migration
**YOK.** Bu sprint DB şemasına dokunmaz. (Guard yalnız konfig okur; entity değişmez.)

## Dosya değişiklikleri
| Dosya | Değişiklik |
|---|---|
| `deploy/nginx.conf` | 443 ssl + 80→301 + HSTS + defansif `/metrics` 404 |
| `docker-compose.prod.yml` | 443 port + cert volume |
| `docker-compose.monitoring.yml` | loopback bind + GF parola zorunlu + anonymous/embedding kapalı |
| `pliaformaFrontend/src/app/(platform)/platform/health/page.tsx` | env-bazlı/dev-only monitoring; admin/admin kaldırıldı |
| `pliaformaFrontend/.env.example` | `NEXT_PUBLIC_*` monitoring (dev-only) örnekleri |
| `pliaformaApi/src/Pliaforma.Infrastructure/DependencyInjection.cs` | Production secret/seed guard |
| `deploy/.env.example` | uyarılar + `GF_ADMIN_PASSWORD` |
| `scripts/metrics-exposure-smoke.sh` | yeni smoke |
| `docs/security/*` + `docs/PILOT-DEPLOY-DRY-RUN.md` | 31B-1 güncellemeleri |
| `deploy/nginx.conf;C` | silindi (junk) |

## Test / doğrulama
- `dotnet build` 0/0 · `tsc --noEmit` 0 · `next build` 0
- `docker compose -f docker-compose.prod.yml config` VALID
- `docker compose -f docker-compose.monitoring.yml config` → `GF_ADMIN_PASSWORD` zorunlu (yoksa hata)
- `scripts/metrics-exposure-smoke.sh` (statik + opsiyonel runtime)

## Kabul kriterleri
- [ ] nginx üzerinden `/metrics` → 404/403; `/health` → 200
- [ ] monitoring portları compose'da `127.0.0.1` bind
- [ ] Grafana default `admin/admin` yok; anonymous varsayılan kapalı
- [ ] `GF_ADMIN_PASSWORD` yoksa monitoring compose net hata verir
- [ ] health page'de "admin/admin" yok; prod'da monitoring linkleri gizli
- [ ] HTTPS'te HSTS; HTTP→HTTPS 301; Secure cookie HTTPS
- [ ] Production'da CHANGE_ME/zayıf JWT_SECRET ve seed admin → backend açılmaz; geçerli env → açılır
- [ ] gerçek sertifika/domain repo'da yok
- [ ] migration yok; build'ler yeşil; dev bozulmadı

## Açık riskler (31B-1 sonrası)
- HSTS sticky: TLS doğrulanmadan uzun `max-age` verilmemeli (başta kısa).
- Prometheus prod scrape tasarımı: pilotta monitoring kapalı varsayıldı; açılırsa internal-network scrape ayrıca tasarlanmalı.
- Gerçek sertifika operatör tarafından sağlanmalı (repo'da yok) → prod nginx certsiz açılmaz (bilinçli).
