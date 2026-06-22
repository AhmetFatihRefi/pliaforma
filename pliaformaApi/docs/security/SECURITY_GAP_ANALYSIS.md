# Güvenlik Gap Analizi — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Kaynak: kod incelemesi (HEAD) + [../SECURITY-REVIEW.md](../SECURITY-REVIEW.md)

Her satır: **Mevcut durum (kanıtlı) → Eksik → Öneri → Risk ID**. Risk ID'leri [RISK_REGISTER.md](RISK_REGISTER.md)'a bağlanır.
Statü sembolleri: ✅ yeterli · ⚠️ kısmi · ❌ yok.

---

## 1. Erişim & yetkilendirme

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| Role-based authorization | Tüm controller `[Authorize]`; rol bazlı `[Authorize(Roles=...)]`; PlatformAdmin policy | ✅ | — | Mevcut; regresyon testine bağla | — |
| Tenant isolation | Handler'da `Id==x && StudioId==currentUser.StudioId`; body'den StudioId güvenilmiyor; MembershipGuard | ✅ | Otomatik regresyon suite yok (yalnız smoke) | Cross-tenant + object-level testleri CI'a | P1-9 |
| Object-level authz | Instructor kendi seansı/öğrencisi; Student kendi verisi; cross-user notification 404 | ✅ | Formal test matrisi | Test matrisi + audit | P1-9 |
| Yetki matrisi (kim hangi veri) | Kodda dağınık | ⚠️ | Yazılı yetki matrisi | KVKK tedbir dosyasına matris | P1-4 |

## 2. Hesap & kimlik güvenliği

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| Parola saklama | BCrypt (`PasswordHasher`) | ✅ | — | — | — |
| Parola politikası | Min 8, kompleksite yok | ⚠️ | Kompleksite/yaygın parola reddi | Min 10–12 + kompleksite | P1-2 |
| Brute-force | Auth uçlarında IP rate limit (prod 10/dk) | ⚠️ | Hesap kilitleme yok | N hatada kilit + üstel bekleme | P1-1 |
| MFA | Yok | ❌ | Owner/StudioAdmin için MFA | TOTP — Owner/StudioAdmin P1, diğerleri P2 | P1-10 |
| JWT | HMAC-SHA256, secret<32B fail-fast, 15 dk | ✅ | — | — | — |
| Refresh token | Sadece SHA-256 hash, rotasyonlu, revoke | ✅ | Reuse detection yok | Rotasyon sonrası eski token alarmı | P2 |
| Refresh cookie | HttpOnly+SameSite=Strict+Secure(prod)+Path=/api/auth | ✅ | — | — | — |
| Email verification | `Auth:RequireEmailVerification` (prod default true), 48s token | ✅ | — | — | — |
| Password reset | CSPRNG token, sadece hash, tek-kullanım, 60dk, cooldown, reset'te tüm refresh revoke | ✅ | — | — | — |

## 3. Ağ / taşıma / web hardening

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| TLS/HTTPS | nginx 443 ssl + 80→301 **aktif** (31B-1); prod compose 443+cert volume | 🟡 | Gerçek cert + canlı doğrulama (pilot-deploy) | Operatör certbot ile cert sağlar | **P0-1** |
| HSTS | nginx 443'te `Strict-Transport-Security` (kısa max-age) **aktif** (31B-1) | ✅ | TLS doğrulanınca max-age uzat | — | **P0-1** |
| CSP | Yok (Next.js inline tema scripti nedeniyle bilinçli ertelenmiş) | ❌ | CSP | nonce/hash ile | P1-6 |
| X-Frame-Options / X-Content-Type-Options / Referrer-Policy / Permissions-Policy | nginx'te set | ✅ | — | — | — |
| CORS | `Cors:Origins` allowlist; AllowCredentials | ✅ | Prod origin doğrulaması | Prod env'de `https://` origin | P0-4 |
| Rate limiting | Auth uçları IP bazlı | ✅ | Global limit yok (kabul edilebilir) | İhtiyaca göre genişlet | — |
| Request body limit | nginx `client_max_body_size 10m` | ✅ | Kestrel explicit limit yok (default 30MB) | İsteğe bağlı Kestrel limiti | P2 |
| Exception leakage | Sabit mesaj; stack trace yalnız sunucu logu | ✅ | — | — | — |
| Swagger | Yalnız Development | ✅ | — | — | — |
| `/metrics` | `MapMetrics()` anonim ama nginx'te **defansif `location = /metrics` → 404** (31B-1) | ✅ | — (Prometheus internal/loopback'ten okur) | — | **P0-3** |
| `/health` | Anonim | ⚠️ | (kabul edilebilir) | Ağ izolasyonu doğrula | P0-3 |
| Dosya upload | Yok (`IFormFile` yok) | ✅ | — | İleride eklenirse ayrı denetim | — |

## 4. Loglama, audit, izlenebilirlik

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| PII/token log sızıntısı | Prod JSON log; body/parola/token loglanmaz | ✅ | Dev `LogEmailSender` token loglar | Prod log toplama bu kaynağı dışla | P1-7-log |
| **Güvenlik audit log** | **YOK** — `AuditInterceptor` yalnız timestamp/soft-delete | ❌ | Kim neyi gördü/değiştirdi | Append-only `SecurityAuditLog` | **P0-2** |
| Login/failed-login audit | Yok | ❌ | — | Auth olayları audit | P0-2 |
| Rol değişikliği audit | Yok | ❌ | — | Membership rol değişimi audit | P0-2 |
| Ödeme/sözleşme/export audit | Yok | ❌ | — | Hassas eylem audit | P0-2 |

## 5. Saklama, silme, yedek

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| Soft delete | Tüm entity'lerde | ✅ | Hard-delete/anonimleştirme yok | Retention + anonimleştirme akışı | P1-3 |
| Retention politikası | Yok | ❌ | Saklama takvimi | Politika + token purge job | P1-3 |
| Token/session temizliği | Yok | ❌ | Süresi dolan token purge | Zamanlı temizlik | P1-3 |
| Backup | `ops/backup-postgres.sh` + restore + cron örneği | ✅ | Şifreleme yok, off-site manuel | AES/PGP + off-site + drill kanıtı | P1-7 |
| Restore drill | Dokümante | ✅ | Periyodik kanıt | 3 ayda bir drill kaydı | P1-7 |

## 6. Secrets, dependency, CI, prod hardening

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| Secret hijyeni | Repoda gerçek secret yok; `.gitignore` kapsamlı; fail-fast | ✅ | — | — | — |
| Seed admin / JWT secret | **Production fail-fast guard** (CHANGE_ME/Admin!2345 → açılmaz) (31B-1) + JWT ≥32 byte fail-fast | ✅ | Operatör gerçek değer girer | — | **P0-4** |
| Dependency scan | Yok (CI yok); `npm audit` 2 orta (dev dep) | ❌ | — | npm audit + `dotnet list package --vulnerable` | P1-CI |
| Secret scan (CI) | Yok | ❌ | — | gitleaks/trufflehog | P1-CI |
| SAST | Yok | ❌ | — | CodeQL/Semgrep | P2 |
| Container scan | Yok | ❌ | — | Trivy | P2 |
| SBOM | Yok | ❌ | — | CycloneDX | P2 |
| Grafana | **env-zorunlu parola** (default admin yok) + anonymous/embedding varsayılan kapalı + `127.0.0.1` loopback bind (31B-1) | ✅ | Pilotta yığın kapalı varsayımı | — | **P0-3** |
| Docker prod | nginx tek giriş, non-root, healthcheck, restart policy | ✅ | — | — | — |

## 7. Idari / KVKK süreç

| Konu | Mevcut | Statü | Eksik | Öneri | Risk |
|---|---|---|---|---|---|
| Veri envanteri | Yoktu → bu sprintte | ⚠️→✅ | — | [DATA_INVENTORY.md](DATA_INVENTORY.md) | P1-4 |
| İhlal müdahale planı | Yok | ❌ | — | [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) | P1-5 |
| 72 saat bildirim akışı | Yok | ❌ | — | [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) | P1-5 |
| Veri işleyenler listesi | Yok | ❌ | — | [DATA_PROCESSORS.md](DATA_PROCESSORS.md) (TBD'ler dahil) | P1-5 |
| Aydınlatma / açık rıza / çerez | Yok | ❌ | Hukuki metin (avukat) | İhtiyaç analizi + avukat onayı | P1-8 |

---

## Özet

**Güçlü:** kimlik/token mimarisi, tenant izolasyonu, hata yönetimi, secret hijyeni, backup prosedürü, web başlıkları (nginx).
**En kritik boşluklar:** (1) güvenlik audit log yok, (2) TLS/HSTS aktif değil, (3) /metrics+Grafana prod açık,
(4) retention/silme politikası yok, (5) KVKK idari kanıt dosyaları yok (bu sprintte kapanıyor), (6) CI güvenlik taraması yok.
