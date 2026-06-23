# Güvenlik Temel Checklist'i (Pilot Öncesi Go/No-Go) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Kullanım: pilot canlıya almadan önce her madde işaretlenmeli.
Statü: ✅ hazır · ⏳ ilgili sprintte · ❌ açık.

> İlgili dokümanlar: [RISK_REGISTER.md](RISK_REGISTER.md), [../PILOT-CHECKLIST.md](../PILOT-CHECKLIST.md),
> [../RELEASE-CHECKLIST.md](../RELEASE-CHECKLIST.md), [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md).

---

## Taşıma & web
- [ ] **HTTPS aktif** (nginx 443 + sertifika) — 🟡 config hazır (31B-1); gerçek cert + canlı doğrulama pilot-deploy / **P0-1**
- [x] **HTTP→HTTPS 301 redirect** — ✅ nginx config (31B-1) / P0-1
- [x] **HSTS header** aktif — ✅ nginx 443 (kısa max-age; cert sonrası uzat) (31B-1) / P0-1
- [x] **CSP enforce + nonce** — ✅ Next 16 `proxy.ts` (31C-2): per-request nonce + strict-dynamic, script-src unsafe-inline yok, report-sink `/api/csp-report`, `CSP_MODE` runtime; nginx CSP üretmez (çift header yok) / P1-6
- [ ] **Prod `CSP_MODE=enforce` env-flip** — ⏳ ilk deploy report-only; canlı doğrulama sonrası enforce + restart
- [x] X-Frame-Options / X-Content-Type-Options / Referrer-Policy / Permissions-Policy (nginx) — ✅ (frame-ancestors CSP'de de var)
- [x] CORS allowlist (prod `https://` origin) — ⏳ prod env doğrulaması / P0-4
- [x] Rate limit auth uçlarında aktif (prod 10/dk) — ✅
- [x] `client_max_body_size` (nginx 10m) — ✅
- [x] Swagger prod'da kapalı — ✅

## Secrets & prod konfig
- [x] **Prod `JWT_SECRET` gerçek + ≥32 byte** (örnek değil) — ✅ Production fail-fast guard "CHANGE_ME" (31B-1) + mevcut ≥32 byte / **P0-4**
- [x] **Seed admin parolası değişmiş** (örnek `Admin!2345`/`CHANGE_ME` değil) — ✅ Production fail-fast guard (31B-1) / P0-4
- [x] Repoda gerçek secret yok / `.gitignore` kapsamlı — ✅
- [ ] Prod SMTP kimlikleri ayarlı (fail-fast) — ⏳ prod env
- [x] JWT secret fail-fast (<32B → açılmaz) — ✅

## İzleme & maruziyet
- [x] **`/metrics` dışarı kapalı** (nginx defansif `location = /metrics` → 404) — ✅ (31B-1) / **P0-3**
- [x] **Grafana default parola yok + anonymous kapalı** — ✅ env-zorunlu parola + anonymous varsayılan false (31B-1) / P0-3
- [x] Monitoring portları (9090/3001) `127.0.0.1` loopback bind — ✅ (31B-1); pilotta yığın kapalı / P0-3
- [x] Health page `admin/admin` metni yok + monitoring env-bazlı/prod'da gizli — ✅ (31B-1) / P0-3
- [x] `/health` anonim (kabul edilebilir) — ✅

## Kimlik & oturum
- [x] Parola BCrypt hash — ✅
- [x] **Parola politikası** (min 10 + 4 sınıf + yaygın parola reddi; shared rule + change-password) — ✅ (31B-2) / P1-2
- [x] **Hesap kilitleme** (5 başarısız → 15dk; 423; success/reset/change sıfırlar) — ✅ (31B-2) / P1-1
- [x] **MFA (Owner/StudioAdmin) TOTP** — ✅ (Sprint 31-MFA); hesap-düzeyi zorunlu, recovery codes, PlatformAdmin reset, audit / P1-10
- [ ] **Prod `MFA_ENCRYPTION_KEY` gerçek + `MFA_ENABLED=true`** — ⏳ prod env doğrulaması (yoksa/placeholder ise backend açılmaz); anahtar JWT secret gibi yedeklenmeli
- [x] Refresh token hash + rotasyon + revoke — ✅
- [x] Refresh cookie HttpOnly/SameSite=Strict/Secure(prod)/Path — ✅
- [x] Email verification + password reset güvenli — ✅

## Yetki & tenant
- [x] Tüm controller `[Authorize]` + rol bazlı — ✅
- [x] Handler tenant filtresi (cross-tenant 404) — ✅
- [ ] **Tenant isolation + object-level otomatik regresyon testi geçti** — ⏳31C / P1-9
- [x] `security-audit-smoke.sh` (21 kontrol) geçti — ✅ (mevcut)

## Audit & loglama
- [x] **SecurityAuditLog minimum hazır** (auth event'leri; append-only/BaseEntity değil, PII maskeli, IP/UserAgent, **retention 2 yıl**) — ✅ (31B-2); kapsam genişletme (rol/ödeme/sözleşme/export/sağlık) + viewer → 31C / **P0-2**
- [x] Failed-login + lockout + login success + logout + password reset/change audit'e düşüyor — ✅ (31B-2) / P0-2
- [x] Prod log PII/token sızdırmıyor (dev LogEmailSender prod'da kapalı) — ✅

## Yedek & süreklilik
- [ ] **Backup restore testi yapıldı** (tarihli kanıt) — ⏳ pilot öncesi / [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md)
- [x] **Backup şifreli (gpg AES256)** + restore drill — ✅ (31D-2): `ops/backup-postgres.sh` `.sql.gz.gpg`, prod fail-fast, `ops/restore-drill.sh`; off-site → post-pilot / P1-7
- [ ] **Prod `BACKUP_ENCRYPTION_KEY` gerçek + ayrı yedek** — ⏳ prod env (yoksa backup fail-fast); recovery secret seti (JWT/MFA/Backup)
- [x] Otomatik yedek + retention (cron örneği) — ✅ (mevcut)
- [x] **SecurityAuditLog DB-level append-only + 2yr retention purge** — ✅ (31D-1): PostgreSQL trigger (UPDATE her zaman, DELETE yalnız purge GUC ile blocked) + in-app purge worker (AuditPurge config, ilk deploy DryRun=true); AuthToken/RefreshToken fiziksel hard-delete / P1-3. **Not:** trigger normal/kaza app update-delete'ine karşı guard'dır, tam-compromised app'e karşı mutlak değildir.
- [ ] **Prod `AuditPurge:Enabled=true` + gözlem sonrası `DryRun=false`** — ⏳ ilk deploy DryRun=true

## KVKK / idari
- [x] Veri envanteri hazır — ✅ [DATA_INVENTORY.md](DATA_INVENTORY.md)
- [x] Risk register hazır — ✅ [RISK_REGISTER.md](RISK_REGISTER.md)
- [x] İhlal müdahale + 72 saat runbook hazır — ✅
- [x] Saklama/silme politikası taslağı — ✅
- [x] Veri işleyenler listesi (TBD'ler işaretli) — ✅
- [ ] Aydınlatma metni hazır (avukat) — ❌ ⏳
- [ ] Açık rıza (HealthNotes tutulacaksa) — ❌ ⏳ / karar: pilotta toplama
- [ ] Veri sorumlusu/iletişim kişileri atanmış (TBD) — ❌ ⏳ owner

## Dependency & CI
- [x] **Secret scan (gitleaks) CI'da BLOCKING** — ✅ (31D-3): `.github/workflows/security.yml` + `.gitleaks.toml` (baseline temiz, dar allowlist); gerçek secret leak CI'yı kırar / P1-CI
- [x] **Dependency/IaC scan (advisory)** — ✅ (31D-3): npm audit (`--omit=dev`) + dotnet vulnerable (transitive) + trivy fs + CycloneDX SBOM artifact; haftalık cron / P1-CI
- [x] **Incident tabletop drill** — ✅ (31D-3): [INCIDENT_TABLETOP_DRILL.md](INCIDENT_TABLETOP_DRILL.md) ("çalınan şifreli yedek"); düzenli tekrar operatör sorumluluğu / P1-9

---

## Go/No-Go kararı
**Pilot GO** için en az tüm **P0** maddeleri (HTTPS/HSTS, /metrics+Grafana, prod secret/seed, audit log minimum)
ve işaretli **pilot-öncesi P1** maddeleri (parola politikası, hesap kilitleme, login audit, restore testi,
tenant isolation testi) **tamamlanmış** olmalıdır. HealthNotes pilotta toplanmıyorsa açık rıza maddesi N/A olur.

| Karar | Tarih | Onaylayan (TBD) |
|---|---|---|
| GO / NO-GO | | |
