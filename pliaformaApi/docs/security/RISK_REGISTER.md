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
- **Sprint:** 31B

### P0-2 · Hassas eylem denetim izi (audit log) yok
- **Etki:** İhlalde "kim neyi gördü/değiştirdi" kanıtlanamaz → KVKK hesap verebilirlik (m.12) ihlali.
- **Kanıt:** `AuditInterceptor` yalnız `CreatedAt/UpdatedAt/IsDeleted` doldurur; güvenlik audit tablosu yok.
- **Çözüm:** Append-only `SecurityAuditLog` (PostgreSQL). Min kapsam: login, failed-login, logout, rol değişikliği,
  ödeme/sözleşme oluştur-değiştir-sil, öğrenci/ödeme export, HealthNotes erişimi. Alanlar: actor, action, entity,
  entityId, studioId, IP, timestamp. Kurallar: update/delete edilmez, soft-delete uygulanmaz, yalnız PlatformAdmin
  (çok sınırlı Owner) okur, PII maskeli, request body/token/parola **asla** yazılmaz. **Retention = 2 yıl** (delil
  değeri); 2 yıl sonrası arşivle/sil (kesin yöntem avukat/owner onayı) — bkz. [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md).
- **Acceptance:** Listelenen eylemler audit'e düşüyor; normal akışta değiştirilemiyor; PII maskeli; 2 yıl retention; smoke ile kanıtlı.
- **Sprint:** 31B (minimum tasarım) → 31C (uygulama + kapsam)

### P0-3 · `/metrics` ve Grafana prod'da dışa açık
- **Etki:** Operasyonel/iç metrik ifşası; Grafana default `admin/admin` + anonymous → yetkisiz erişim.
- **Kanıt:** `Program.cs` `MapMetrics()` anonim; `docker-compose.monitoring.yml` `GF_SECURITY_ADMIN_PASSWORD: admin`,
  `GF_AUTH_ANONYMOUS_ENABLED: true`.
- **Çözüm:** nginx'te `/metrics`'i internal/IP-allowlist'e kısıtla; Grafana güçlü parola + anonymous kapat;
  monitoring portlarını (9090/3001) firewall arkasına al.
- **Acceptance:** Dışarıdan `/metrics` ve `:3001` 403/erişilemez; Grafana default cred yok.
- **Sprint:** 31B

### P0-4 · Prod secret / seed admin sertleştirme doğrulaması
- **Etki:** Zayıf/örnek admin parolası veya zayıf JWT secret → tam erişim/oturum üretimi.
- **Kanıt:** `deploy/.env.example` `SEED_ADMIN_PASSWORD=Admin!2345`; JWT secret env zorunlu (fail-fast var).
- **Çözüm:** Prod'da rastgele güçlü `JWT_SECRET` (≥32B) + benzersiz admin parola; ilk girişte değiştirme; CORS origin `https://`.
- **Acceptance:** Prod env checklist doğrulanmış ([SECURITY_BASELINE_CHECKLIST.md](SECURITY_BASELINE_CHECKLIST.md)); örnek değer yok.
- **Sprint:** 31B

---

## P1 — Pilot öncesi yapılmalı

### P1-1 · Hesap kilitleme / brute-force sertleştirme
- **Etki:** Dağıtık brute-force ile hesap ele geçirme (IP rate limit tek başına zayıf).
- **Kanıt:** Yalnız auth uçları IP rate limit; hesap kilidi yok.
- **Çözüm:** N başarısız denemede geçici kilit + üstel bekleme; olay audit'e.
- **Acceptance:** 5 hatadan sonra hesap geçici kilit; kilit/açılma audit'e düşüyor.
- **Sprint:** 31B

### P1-2 · Parola politikası zayıf
- **Etki:** Zayıf parolalar tahmin/sözlük saldırısına açık.
- **Kanıt:** Register/Reset validatörlerinde min 8, kompleksite yok.
- **Çözüm:** Min 10–12 + kompleksite + yaygın parola reddi.
- **Acceptance:** Zayıf parola register/reset'te 400.
- **Sprint:** 31B

### P1-3 · Saklama & silme politikası + token purge yok
- **Etki:** Veri minimizasyonu/saklama sınırı ihlali; süresiz veri birikimi.
- **Kanıt:** Yalnız soft-delete; hard-delete/anonimleştirme/purge yok.
- **Çözüm:** [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md) + süresi dolan AuthToken/RefreshToken purge job.
- **Acceptance:** Politika onaylı; purge job süresi dolan token'ları temizliyor.
- **Sprint:** 31A (politika) → 31D (purge job)

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
- **Sprint:** 31B

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
- **Sprint:** **Sprint 31-MFA (ayrı mini-sprint) — Owner/Admin MFA**

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
| **31B** | **P0-1, P0-3, P0-4**, P0-2 (tasarım), P1-1, P1-2, P1-6 |
| **31C** | P0-2 (uygulama), P1-8 (teknik), P1-9 (test) |
| **31D** | P1-3 (purge), P1-7, P1-9 (CI), P1-CI, P2-4 |
| **31-MFA** (ayrı mini-sprint) | P1-10 (Owner/StudioAdmin TOTP MFA) |
