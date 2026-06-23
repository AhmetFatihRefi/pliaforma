# Risk Register — Pliaforma Security & KVKK

Statü: **TASLAK (Sprint 31A)** · Önceliklendirme: P0 (pilot blocker) / P1 (pilot öncesi) / P2 (pilot sonrası)

Her risk: **açıklama · etki · mevcut kanıt · önerilen çözüm · acceptance criteria · kapatılacağı sprint.**
Kanıt referansları HEAD'deki kod incelemesine dayanır.

---

## P0 — Pilot blocker (veri sızıntısı / yetkisiz erişim doğurabilir)

### P0-1 · TLS/HTTPS + HSTS aktif değil
- **Etki:** Trafik düz metin; Secure refresh cookie taşınmaz → oturum/parola yakalanabilir (MITM).
- **Kanıt:** `Program.cs`'te `UseHttpsRedirection` yok; `deploy/nginx.conf` 443 bloğu yorumda; HSTS yok.
- **Çözüm:** Sertifika (certbot) + nginx 443 + HTTP→HTTPS 301 + `Strict-Transport-Security` + `PUBLIC_ORIGIN=https://…`.
- **Acceptance:** `curl -I http://host` → 301; HTTPS yanıtında HSTS header; Secure cookie set ediliyor; smoke yeşil.
- **Sprint:** **31B-1 — config UYGULANDI** (nginx 443+301+HSTS, prod compose 443+cert volume, certs gitignore). Gerçek sertifika + canlı uçtan-uca doğrulama pilot-deploy'da.

### P0-2 · Hassas eylem denetim izi (audit log) yok
- **Etki:** İhlalde "kim neyi gördü/değiştirdi" kanıtlanamaz → KVKK hesap verebilirlik (m.12) ihlali.
- **Kanıt:** `AuditInterceptor` yalnız `CreatedAt/UpdatedAt/IsDeleted` doldurur; güvenlik audit tablosu yok.
- **Çözüm:** Append-only `SecurityAuditLog` (PostgreSQL). Min kapsam: login, failed-login, logout, rol değişikliği,
  ödeme/sözleşme oluştur-değiştir-sil, öğrenci/ödeme export, HealthNotes erişimi. Alanlar: actor, action, entity,
  entityId, studioId, IP, timestamp. Kurallar: update/delete edilmez, soft-delete uygulanmaz, yalnız PlatformAdmin
  (çok sınırlı Owner) okur, PII maskeli, request body/token/parola **asla** yazılmaz. **Retention = 2 yıl** (delil
  değeri); 2 yıl sonrası arşivle/sil (kesin yöntem avukat/owner onayı) — bkz. [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md).
- **Acceptance:** Listelenen eylemler audit'e düşüyor; normal akışta değiştirilemiyor; PII maskeli; 2 yıl retention; smoke ile kanıtlı.
- **Sprint:** **31B-2 — UYGULANDI** (SecurityAuditLog entity + migration; auth event producer'ları: login success/failed,
  account lockout, logout, password reset requested/completed, password changed; append-only/BaseEntity değil;
  PII maskeli `ActorEmailMasked`; Ip/UserAgent; **retention 2 yıl**). Kapsam genişletme (rol/ödeme/sözleşme/export/HealthNotes + viewer) → 31C.

### P0-3 · `/metrics` ve Grafana prod'da dışa açık
- **Etki:** Operasyonel/iç metrik ifşası; Grafana default `admin/admin` + anonymous → yetkisiz erişim.
- **Kanıt:** `Program.cs` `MapMetrics()` anonim; `docker-compose.monitoring.yml` `GF_SECURITY_ADMIN_PASSWORD: admin`,
  `GF_AUTH_ANONYMOUS_ENABLED: true`.
- **Çözüm:** nginx'te `/metrics`'i internal/IP-allowlist'e kısıtla; Grafana güçlü parola + anonymous kapat;
  monitoring portlarını (9090/3001) firewall arkasına al.
- **Acceptance:** Dışarıdan `/metrics` ve `:3001` 403/erişilemez; Grafana default cred yok.
- **Sprint:** **31B-1 — UYGULANDI** (nginx defansif `/metrics`→404; monitoring portları `127.0.0.1` bind; Grafana parolası env-zorunlu + anonymous/embedding varsayılan kapalı; health page `admin/admin` kaldırıldı + monitoring env-bazlı/prod'da gizli). Smoke: `scripts/metrics-exposure-smoke.sh`.

### P0-4 · Prod secret / seed admin sertleştirme doğrulaması
- **Etki:** Zayıf/örnek admin parolası veya zayıf JWT secret → tam erişim/oturum üretimi.
- **Kanıt:** `deploy/.env.example` `SEED_ADMIN_PASSWORD=Admin!2345`; JWT secret env zorunlu (fail-fast var).
- **Çözüm:** Prod'da rastgele güçlü `JWT_SECRET` (≥32B) + benzersiz admin parola; ilk girişte değiştirme; CORS origin `https://`.
- **Acceptance:** Prod env checklist doğrulanmış ([SECURITY_BASELINE_CHECKLIST.md](SECURITY_BASELINE_CHECKLIST.md)); örnek değer yok.
- **Sprint:** **31B-1 — UYGULANDI** (Production'da `Jwt:Secret`/`Seed:AdminPassword` "CHANGE_ME"/"Admin!2345" içeriyorsa fail-fast; `.env.example` uyarıları güçlendirildi). Development bozulmaz; boş seed → guard atlanır.

---

## P1 — Pilot öncesi yapılmalı

### P1-1 · Hesap kilitleme / brute-force sertleştirme
- **Etki:** Dağıtık brute-force ile hesap ele geçirme (IP rate limit tek başına zayıf).
- **Kanıt:** Yalnız auth uçları IP rate limit; hesap kilidi yok.
- **Çözüm:** N başarısız denemede geçici kilit + üstel bekleme; olay audit'e.
- **Acceptance:** 5 hatadan sonra hesap geçici kilit; kilit/açılma audit'e düşüyor.
- **Sprint:** **31B-2 — UYGULANDI** (5 başarısız → 15dk kilit; 423; başarılı login + reset/change → sayaç sıfırlar; AuthSettings configurable; lockout audit).

### P1-2 · Parola politikası zayıf
- **Etki:** Zayıf parolalar tahmin/sözlük saldırısına açık.
- **Kanıt:** Register/Reset validatörlerinde min 8, kompleksite yok.
- **Çözüm:** Min 10–12 + kompleksite + yaygın parola reddi.
- **Acceptance:** Zayıf parola register/reset'te 400.
- **Sprint:** **31B-2 — UYGULANDI** (shared `PasswordRules`: min 10 + 4 sınıf + yaygın parola reddi; Register/RegisterStudio/Reset/Change ortak; mevcut kullanıcı login'de zorlanmıyor). + ChangePassword endpoint.

### P1-3 · Saklama & silme politikası + token purge yok
- **Etki:** Veri minimizasyonu/saklama sınırı ihlali; süresiz veri birikimi.
- **Kanıt:** Yalnız soft-delete; hard-delete/anonimleştirme/purge yok.
- **Çözüm:** [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md) + süresi dolan AuthToken/RefreshToken purge job.
- **Acceptance:** Politika onaylı; purge job süresi dolan token'ları temizliyor.
- **Sprint:** 31A (politika) → **31D-1 — UYGULANDI** (in-app `AuditRetentionPurgeBackgroundService`: SecurityAuditLog 2yr + AuthToken/RefreshToken expired/revoked+tampon **fiziksel hard-delete**; `AuditPurge:Enabled/DryRun/...` config, ilk deploy DryRun=true; **DB-level append-only trigger** SecurityAuditLogs'ta — UPDATE her zaman, DELETE yalnız purge GUC `app.audit_purge='on'` ile; migration `Sprint31D_AuditAppendOnly`). Business-data anonimleştirme + Notification purge → post-pilot.

### P1-4 · Veri envanteri + yetki matrisi + KVKK tedbir kanıtı
- **Etki:** KVKK kanıtı eksik.
- **Kanıt:** Bu sprint öncesi yoktu.
- **Çözüm:** [DATA_INVENTORY.md](DATA_INVENTORY.md), [KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md](KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md).
- **Acceptance:** Dosyalar mevcut, gözden geçirilmiş; yetki matrisi net.
- **Sprint:** 31A ✅

### P1-5 · İhlal müdahale + 72 saat bildirim + veri işleyenler
- **Etki:** İhlalde süreç/şablon yok → 72 saat kaçırma riski.
- **Çözüm:** [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md), [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md), [DATA_PROCESSORS.md](DATA_PROCESSORS.md).
- **Acceptance:** Planlar mevcut; sorumlu/iletişim atanmış (bazıları TBD).
- **Sprint:** 31A ✅ (doküman) / 31D (tabletop drill)

### P1-6 · CSP header yok
- **Etki:** XSS/clickjacking yüzeyi geniş.
- **Kanıt:** nginx'te CSP yok (bilinçli ertelenmiş).
- **Çözüm:** Next.js inline tema scripti için nonce/hash ile CSP.
- **Acceptance:** Prod'da CSP aktif; uygulama bozulmuyor; konsol CSP ihlali yok.
- **Sprint:** **31C-2 — ENFORCE + NONCE UYGULANDI** (Next 16 `proxy.ts` per-request nonce + `strict-dynamic`; `script-src 'unsafe-inline'` kaldırıldı; nginx CSP'yi Next'e devretti → çift header yok; report-sink `POST /api/csp-report` salt-log; `CSP_MODE=report-only|enforce|disabled` runtime env). Prod ilk deploy **report-only** → canlı doğrulama → env-flip **enforce**. (31B-2: Report-Only baseline.) report-to → 31D.

### P1-7 · Backup şifreleme + off-site + drill kanıtı
- **Etki:** Şifresiz yedek çalınırsa toplu PII ifşası.
- **Kanıt:** `ops/backup-postgres.sh` gzip; şifreleme yok; off-site manuel.
- **Çözüm:** AES/PGP şifreli yedek + off-site + tarihli restore drill kaydı.
- **Acceptance:** Şifreli yedek üretiliyor; restore drill çıktısı kayıtlı.
- **Sprint:** 31D

### P1-8 · Özel nitelikli veri (HealthNotes) + serbest metin sızıntısı
- **Etki:** Sağlık verisi yetkisiz erişim/ifşası (KVKK m.6 ağır sonuç).
- **Kanıt:** `StudentProfile.HealthNotes` + çok sayıda serbest `Notes` alanı.
- **Çözüm:** Veri minimizasyonu (pilotta toplama); tutulacaksa erişimi Owner/StudioAdmin'e sınırla + audit + UI uyarısı;
  aydınlatma/açık rıza ihtiyacı dokümante.
- **Acceptance:** HealthNotes pilotta kapalı **veya** erişim sınırlı + audit'li; serbest metin UI uyarısı kararı verilmiş.
- **Sprint:** 31A (risk/karar) → 31B/31C (teknik)

### P1-9 · Tenant isolation / object-level otomatik regresyon testi yok
- **Etki:** Refactor sonrası sessiz cross-tenant açık.
- **Kanıt:** Yalnız smoke (`security-audit-smoke.sh`); CI yok.
- **Çözüm:** Cross-tenant + object-level test matrisi (her hassas by-id uçta 404).
- **Acceptance:** Testler suite'te; CI'da koşuyor.
- **Sprint:** 31C (test) / 31D (CI)

### P1-10 · MFA yok (Owner/StudioAdmin)
- **Etki:** Yetkili hesap ele geçirilirse tüm tenant verisi.
- **Çözüm:** Owner/StudioAdmin için TOTP MFA (Instructor/Student P2).
- **Acceptance:** Owner/StudioAdmin MFA ile giriş. Not: 31B şişmesin diye MFA 31B'den **ayrıldı**; pilot öncesi
  zorunlu minimum P1-1 (hesap kilitleme) + P1-2 (parola politikası) + login/failed-login audit (P0-2) ile sağlanır.
- **Sprint:** **Sprint 31-MFA — UYGULANDI** (TOTP; hesap-düzeyi zorunlu Owner/StudioAdmin; AES-256-GCM şifreli secret + `Mfa:EncryptionKey` prod fail-fast; stateless challenge token; setup-pending `mfa_pending` claim + API enforcement middleware; 10 recovery code (hash'li, tek kullanım); PlatformAdmin reset; 7 audit event; `Mfa:Enabled` switch prod-true/dev-false). Migration: `Sprint31MFA_TotpMfa`. Instructor/Student MFA → P2-2.

### P1-CI · Secret scan + dependency scan (CI)
- **Etki:** Sızan secret / zafiyetli bağımlılık fark edilmez.
- **Çözüm:** gitleaks/trufflehog + `npm audit` + `dotnet list package --vulnerable` (PR gate).
- **Acceptance:** PR'da taramalar koşuyor; kritik bulguda kırmızı.
- **Sprint:** 31D

---

## P2 — Pilot sonrası

| ID | Risk | Çözüm | Sprint |
|---|---|---|---|
| P2-1 | Refresh token reuse detection yok | Rotasyon sonrası eski token alarmı/iptal | post-pilot |
| P2-2 | MFA Instructor/Student | TOTP yaygınlaştır | post-pilot |
| P2-3 | Oturum yönetimi UI | Aktif oturum gör/iptal | post-pilot |
| P2-4 | SAST / Container scan / SBOM | CodeQL/Semgrep + Trivy + CycloneDX | post-pilot |
| P2-5 | PII UI maskeleme | Telefon/email maskeleme katmanı | post-pilot |
| P2-6 | Tam "unutulma hakkı" anonimleştirme | Anonimleştirme akışı | post-pilot |
| P2-7 | Alerting / SIEM | Prometheus/Grafana alert + SIEM | post-pilot |
| P2-8 | Worker tek-instance kilidi | Distributed lock | post-pilot |
| P2-9 | Kestrel request body limit | Explicit MaxRequestBodySize | post-pilot |

---

## Sprint atama özeti

| Sprint | Riskler |
|---|---|
| **31A** ✅ | P1-3 (politika), P1-4, P1-5 (doküman), P1-8 (risk/karar) |
| **31B-1** ✅ | **P0-1, P0-3, P0-4** (infra/config, migration yok) — uygulandı, doğrulama yeşil |
| **31B-2** ✅ | P0-2 (auth events), P1-1 (lockout), P1-2 (parola politikası + change-password), P1-6 (CSP Report-Only) — uygulandı (migration: Sprint31B2_AuthHardeningAndAuditLog) |
| **31C-1** ✅ | P0-2 (kapsam genişletme: rol/ödeme/sözleşme/export/HealthNotes + viewer), P1-9 (tenant isolation test) — uygulandı (migration yok) |
| **31C-2** ✅ | P1-6 (CSP enforce + nonce + report-sink) — uygulandı (migration yok; prod report-only→enforce env-flip) |
| **31D-1** ✅ | P1-3 (retention purge + DB-level append-only trigger) — uygulandı (migration: Sprint31D_AuditAppendOnly) |
| **31D-2** | P1-7 (backup encryption + restore drill) |
| **31D-3** | P1-CI (gitleaks blocking + npm/dotnet/Trivy/SBOM advisory), P1-9, incident tabletop |
| **31-MFA** ✅ (ayrı mini-sprint) | P1-10 (Owner/StudioAdmin TOTP MFA) — uygulandı (migration: Sprint31MFA_TotpMfa) |
