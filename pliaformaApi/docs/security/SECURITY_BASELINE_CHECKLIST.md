# Güvenlik Temel Checklist'i (Pilot Öncesi Go/No-Go) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Kullanım: pilot canlıya almadan önce her madde işaretlenmeli.
Statü: ✅ hazır · ⏳ ilgili sprintte · ❌ açık.

> İlgili dokümanlar: [RISK_REGISTER.md](RISK_REGISTER.md), [../PILOT-CHECKLIST.md](../PILOT-CHECKLIST.md),
> [../RELEASE-CHECKLIST.md](../RELEASE-CHECKLIST.md), [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md).

---

## Taşıma & web
- [ ] **HTTPS aktif** (nginx 443 + sertifika) — ⏳31B / **P0-1**
- [ ] **HTTP→HTTPS 301 redirect** — ⏳31B / P0-1
- [ ] **HSTS header** aktif — ⏳31B / P0-1
- [ ] CSP header aktif (nonce) — ⏳31B / P1-6
- [x] X-Frame-Options / X-Content-Type-Options / Referrer-Policy / Permissions-Policy (nginx) — ✅
- [x] CORS allowlist (prod `https://` origin) — ⏳ prod env doğrulaması / P0-4
- [x] Rate limit auth uçlarında aktif (prod 10/dk) — ✅
- [x] `client_max_body_size` (nginx 10m) — ✅
- [x] Swagger prod'da kapalı — ✅

## Secrets & prod konfig
- [ ] **Prod `JWT_SECRET` gerçek + ≥32 byte** (örnek değil) — ⏳31B / **P0-4**
- [ ] **Seed admin parolası değişmiş** (örnek `Admin!2345` değil) — ⏳31B / P0-4
- [x] Repoda gerçek secret yok / `.gitignore` kapsamlı — ✅
- [ ] Prod SMTP kimlikleri ayarlı (fail-fast) — ⏳ prod env
- [x] JWT secret fail-fast (<32B → açılmaz) — ✅

## İzleme & maruziyet
- [ ] **`/metrics` dışarı kapalı** (nginx internal kısıt) — ⏳31B / **P0-3**
- [ ] **Grafana default parola yok + anonymous kapalı** — ⏳31B / P0-3
- [ ] Monitoring portları (9090/3001) firewall arkasında — ⏳31B / P0-3
- [x] `/health` anonim (kabul edilebilir) — ✅

## Kimlik & oturum
- [x] Parola BCrypt hash — ✅
- [ ] **Parola politikası** (min 10–12 + kompleksite) — ⏳31B / P1-2
- [ ] **Hesap kilitleme** (brute-force) — ⏳31B / P1-1
- [ ] MFA (Owner/StudioAdmin) — ⏳ **Sprint 31-MFA (ayrı mini-sprint)** / P1-10 (31B'ye dahil DEĞİL)
- [x] Refresh token hash + rotasyon + revoke — ✅
- [x] Refresh cookie HttpOnly/SameSite=Strict/Secure(prod)/Path — ✅
- [x] Email verification + password reset güvenli — ✅

## Yetki & tenant
- [x] Tüm controller `[Authorize]` + rol bazlı — ✅
- [x] Handler tenant filtresi (cross-tenant 404) — ✅
- [ ] **Tenant isolation + object-level otomatik regresyon testi geçti** — ⏳31C / P1-9
- [x] `security-audit-smoke.sh` (21 kontrol) geçti — ✅ (mevcut)

## Audit & loglama
- [ ] **SecurityAuditLog minimum hazır** (login/rol/ödeme/sözleşme/export/sağlık erişimi; append-only, PII maskeli, **retention 2 yıl**) — ⏳31B tasarım / 31C uygulama / **P0-2**
- [ ] Failed-login audit'e düşüyor — ⏳31B/C / P0-2
- [x] Prod log PII/token sızdırmıyor (dev LogEmailSender prod'da kapalı) — ✅

## Yedek & süreklilik
- [ ] **Backup restore testi yapıldı** (tarihli kanıt) — ⏳ pilot öncesi / [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md)
- [ ] Backup şifreli + off-site — ⏳31D / P1-7
- [x] Otomatik yedek + retention (cron örneği) — ✅ (mevcut)

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
- [ ] **Dependency scan geçti** (npm audit + dotnet vulnerable) — ⏳31D / P1-CI
- [ ] **Secret scan geçti** (gitleaks/trufflehog) — ⏳31D / P1-CI

---

## Go/No-Go kararı
**Pilot GO** için en az tüm **P0** maddeleri (HTTPS/HSTS, /metrics+Grafana, prod secret/seed, audit log minimum)
ve işaretli **pilot-öncesi P1** maddeleri (parola politikası, hesap kilitleme, login audit, restore testi,
tenant isolation testi) **tamamlanmış** olmalıdır. HealthNotes pilotta toplanmıyorsa açık rıza maddesi N/A olur.

| Karar | Tarih | Onaylayan (TBD) |
|---|---|---|
| GO / NO-GO | | |
