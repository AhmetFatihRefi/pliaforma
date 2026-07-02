# Pilot Go-Live Checklist — Pliaforma

**Tek GO/NO-GO kaynağı.** Release: ______ (operatör doldurur — o anki `git rev-parse HEAD` / release tag) · Doldurma tarihi: ______ · Karar: **GO / NO-GO**

> **Sprint 47 (Pre-live Pilot Hardening) notu:** bu checklist artık belirli bir tag/commit'e SABİTLENMEZ — §1.1/§1.5
> operatör her doldurduğunda o anki HEAD ile doğrulanır (`ops/restore-drill.sh` son migration'ı repo'dan otomatik
> okur, elle güncellenmesi gerekmez). Sprint 47 kapsamı: pilot UI erişim düzeltmesi, pilot fallback guest-only guard,
> pilot header/line finans izolasyonu, Instructor past-booking guard, iptal edilmiş satırın borç rollup'ından
> çıkarılması. Detay: `sprint47-smoke.sh` + Sprint 47 validation raporu.

> Bu doküman operatör için **tek** go-live karar kaynağıdır; teknik detay için ilgili runbook/spec'lere atıf verir.
> **Lejant:** **[B]** = pilot-öncesi **blocker** (biri bile fail → NO-GO) · **[P]** = post-pilot takip · **[A]** = otomatik doğrulanabilir (smoke/komut) · **[O]** = operatör/manuel.
> Tüm `Date/Owner/Evidence/Status/Notes` alanları **operatör tarafından** doldurulur. **Gerçek secret/domain/cert/key bu dokümana YAZILMAZ.**
> **Kapsam:** yalnız go-live checklist. Bu doküman kapsamında kod/script/migration/workflow **değiştirilmez**.

---

## 1. Version / release state
| # | Check | Tip | Beklenen | Komut / Evidence | Status |
|---|---|---|---|---|---|
|1.1| Release tag/commit | [A] | operatör doldurur (o anki HEAD) | `git rev-parse HEAD` / `git rev-list -n1 <tag>` | ☐ |
|1.2| main = origin/main senkron | [A] | ahead/behind 0/0 | `git rev-list --left-right --count origin/main...HEAD` | ☐ |
|1.3| git status clean | [A] | boş | `git status --porcelain` | ☐ |
|1.4| Eski tag'ler yerinde | [A] | önceki pilot tag'leri taşınmadı/silinmedi | `git ls-remote --tags origin` | ☐ |
|1.5| Son migration | [A] | repo `Migrations/` klasöründeki EN SON dosya ile DB `__EFMigrationsHistory` eşleşir (elle sabit isim YOK) | `ops/restore-drill.sh` §6 (Sprint 47 — otomatik karşılaştırır) | ☐ |
|1.6| has-pending clean | [A] | "No changes…" | bkz. §10 | ☐ |

## 2. Production env / config **[B]**
| # | Check | Beklenen | Not | Status |
|---|---|---|---|---|
|2.1| `ASPNETCORE_ENVIRONMENT` | `Production` | prod guard'larını aktive eder | ☐ |
|2.2| `Jwt:Secret` | gerçek, güçlü (≥32B), repo'da değil | rotasyon = tüm token geçersiz | ☐ |
|2.3| `MFA_ENCRYPTION_KEY` | gerçek, güçlü, **ayrı yedekli** | kayıp → MFA re-enroll | ☐ |
|2.4| `BACKUP_ENCRYPTION_KEY` | gerçek, güçlü, **DB yedeğiyle aynı yerde DEĞİL** | recovery secret seti | ☐ |
|2.5| `GF_ADMIN_PASSWORD` | gerçek, güçlü | Grafana | ☐ |
|2.6| `CSP_MODE` | **`report-only`** (ilk deploy) | enforce geçiş → §7 | ☐ |
|2.7| `AuditPurge:Enabled` / `:DryRun` | `Enabled=true` + **`DryRun=true`** | gerçek purge → §7 | ☐ |
|2.8| `ConnectionStrings:DefaultConnection` | gerçek prod DB | dev `Password=postgres` DEĞİL | ☐ |
|2.9| Public origin / FE-BE URL | gerçek domain, same-origin | nginx proxy | ☐ |
|2.10| CORS + cookie `Secure` | prod `https://` origin; Secure/HttpOnly/SameSite | HTTPS zorunlu | ☐ |
|2.11| Seed admin / weak-secret guard | prod startup `CHANGE_ME`/`Admin!2345` reddediyor | 31B-1 | ☐ |
|2.12| `.env.example` placeholder | gerçek değer repo'da **yok** | gitleaks teyit | ☐ |
|2.13| `Pilot:SimplePeopleMode` (`PILOT_SIMPLE_PEOPLE_MODE`) | bilinçli karar: pilot stüdyo sade-mod (hoca ad-soyad guest ekler) kullanacaksa `true`; kullanmayacaksa **varsayılan `false`** | Sprint 46/47 — kapalıyken hoca guest-create/minimal-liste/booking 403 alır (regresyon DEĞİL, beklenen) | ☐ |
|2.14| `Registration:StudioRegistrationEnabled` (`STUDIO_REGISTRATION_ENABLED`) **[B]** | tek-salon pilot: **`false`** (dışarıdan yeni stüdyo YOK; register-studio → 403). Compose verilmezse zaten false. | Sprint 49 — `curl .../api/auth/registration-status` → `false`; register-studio POST → 403. İlk owner: DEPLOY-DRY-RUN §10. Login/davet-kabul ETKİLENMEZ. | ☐ |

## 3. TLS / domain / nginx **[B]**
| # | Check | Beklenen | Status |
|---|---|---|---|
|3.1| Gerçek domain DNS | A/AAAA prod IP'ye | ☐ |
|3.2| SSL cert volume | `deploy/certs/{fullchain,privkey}.pem` (repo'da değil) | ☐ |
|3.3| HTTP→HTTPS 301 | aktif | ☐ |
|3.4| HSTS başlangıç | `max-age=300` (kısa) | ☐ |
|3.5| HSTS uzatma | `31536000` → TLS uçtan uca doğrulanınca **ayrı karar [P]** | ☐ |
|3.6| Refresh cookie | Secure + HttpOnly + SameSite | ☐ |
|3.7| nginx CSP üretmiyor | CSP Next/proxy tarafında (çift header yok) | ☐ |
|3.8| `/metrics` public değil | nginx 404 | ☐ |
|3.9| backend portu public değil | yalnız nginx internal | ☐ |

## 4. Monitoring
| # | Check | Tip | Status |
|---|---|---|---|
|4.1| Prometheus/Grafana public değil | [B] | ☐ |
|4.2| Grafana anonymous kapalı | [B] | ☐ |
|4.3| Grafana admin parolası değişmiş | [B] | ☐ |
|4.4| loopback/internal binding doğru | [O] | ☐ |
|4.5| `/metrics` nginx üzerinden 404 | [A] | ☐ |
|4.6| `/health` çalışıyor | [A] | ☐ |

> **Sprint 47 karar notu:** `monitoring/prometheus.yml` şu an yalnız DEV backend'i (`host.docker.internal:5132`)
> hedefler; `docker-compose.monitoring.yml` başlığı "PROD'da bu yığın ÇALIŞTIRILMAZ (varsayım)" der. **Pilot boyunca
> Prometheus/Grafana KULLANILMAYACAK** — bilinçli karar, prod scrape hedefi eklemek [P] post-pilot işidir. Yerine:
> (a) `/health` — operatör günlük elle veya harici ücretsiz bir uptime servisiyle (`https://<domain>/health`) izler,
> (b) `docker compose -f docker-compose.prod.yml logs -f backend` ile hata/exception taraması (bkz.
> [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md) §4 günlük sağlık kontrolleri).

## 5. Database / migration **[B]**
| # | Check | Tip | Status |
|---|---|---|---|
|5.1| Migration apply (startup) | [A] | ☐ |
|5.2| Son migration repo ile eşleşiyor (bkz. §1.5) | [A] | ☐ |
|5.3| DB trigger + function mevcut | [A] | ☐ |
|5.4| has-pending clean | [A] | ☐ |
|5.5| append-only normal UPDATE/DELETE blocked | [A] | ☐ |
|5.6| purge bypass yalnız controlled GUC path | [A] | ☐ |
|5.7| **Deploy öncesi DB yedeği alındı** | [B][O] | ☐ |

## 6. Backup / restore **[B]**
| # | Check | Tip | Status |
|---|---|---|---|
|6.1| `BACKUP_ENCRYPTION_KEY` var | [O] | ☐ |
|6.2| prod backup şifreli `.sql.gz.gpg` üretiyor | [A] | ☐ |
|6.3| wrong-key restore fail | [A] | ☐ |
|6.4| correct-key restore success | [A] | ☐ |
|6.5| **restore-drill tarihi + sonucu kayıtlı** | [O] | ☐ |
|6.6| backup key DB yedeğiyle **ayrı** yerde | [O] | ☐ |
|6.7| off-site yoksa **risk notu** | [P] | ☐ |

## 7. Security runtime
| # | Check | Tip | Status |
|---|---|---|---|
|7.1| Owner/StudioAdmin MFA zorunlu | [A] | ☐ |
|7.2| register-studio → MFA setup flow | [A] | ☐ |
|7.3| login MFA challenge | [A] | ☐ |
|7.4| recovery code saklama uyarısı (UI) | [O] | ☐ |
|7.5| account lockout | [A] | ☐ |
|7.6| password policy | [A] | ☐ |
|7.7| change-password refresh revoke | [A] | ☐ |
|7.8| **CSP report-only canlı gözlem** (konsol + `/api/csp-report`) | [O] | ☐ |
|7.9| **CSP_MODE=enforce geçiş şartı:** report-only'de ihlal yok + enforce E2E yeşil → env-flip + restart | [P][O] | ☐ |
|7.10| **AuditPurge gerçek purge geçiş şartı:** DryRun gözlem (silinecek sayı makul) → `DryRun=false` | [P][O] | ☐ |

## 8. CI / repository
| # | Check | Tip | Status |
|---|---|---|---|
|8.1| **Actions security workflow §1.1'deki commit üzerinde yeşil** | **[B][O]** | ☐ |
|8.2| gitleaks secret-scan geçti | [B][O-UI] | ☐ |
|8.3| advisory bulguları notlandı | [O] | ☐ |
|8.4| npm moderate (postcss/next) ayrı bakım işi notlandı | [P] | ☐ |
|8.5| gerçek secret/key/cert yok | [A] | ☐ |

> **§8 NOT (önemli):** Bot/Claude **private repo Actions UI'sını GÖREMEZ** (gh unauth → API 404). gitleaks gate, CI ile **birebir aynı** pinned container + config + komutla **local docker'da temiz** doğrulandı (yardımcı/deterministik kanıt). **Ancak final teyit operatör tarafından GitHub Actions UI'da yapılır ve §8.1 yine pilot blocker'dır** (yeşil görülmeden GO verilmez).

## 9. KVKK / legal / ops **[B — operasyonel/legal]**
| # | Check | Tip | Status |
|---|---|---|---|
|9.1| `DATA_PROCESSORS.md` TBD sağlayıcılar dolduruldu | [B][O] | ☐ |
|9.2| Incident contact rolleri (IRP §1 TBD) dolduruldu | [B][O] | ☐ |
|9.3| KVKK aydınlatma/açık rıza metinleri **avukat incelemesi** | **[B][O] — teknik değil, operasyonel/legal blocker** | ☐ |
|9.4| **HealthNotes aktif toplanıyor mu?** | varsayılan **HAYIR** | ☐ |
|9.5| HealthNotes toplanıyorsa: **açık rıza + erişim sınırı + audit + metinler OLMADAN GO YOK** | [B][O] | ☐ |
|9.6| Incident tabletop drill tarihi + sonucu kayıtlı | [B][O] | ☐ |
|9.7| 72h breach runbook operatörce biliniyor | [O] | ☐ |

> Atıf: [DATA_PROCESSORS.md](security/DATA_PROCESSORS.md) · [INCIDENT_RESPONSE_PLAN.md](security/INCIDENT_RESPONSE_PLAN.md) · [BREACH_NOTIFICATION_RUNBOOK.md](security/BREACH_NOTIFICATION_RUNBOOK.md) · [INCIDENT_TABLETOP_DRILL.md](security/INCIDENT_TABLETOP_DRILL.md) · [DATA_INVENTORY.md](security/DATA_INVENTORY.md).

> **Sprint 47 beklenti notu — bildirimler:** `LoggingNotificationSender` prod'da da Email/Sms/WhatsApp kanallarını
> **gerçekten göndermez** (no-op; `Status=Failed` olarak loglanır). Pilot boyunca TÜM ders/ödeme hatırlatmaları
> yalnız **uygulama içi (in-app)** bildirimdir — e-posta/SMS entegrasyonu bu sprint kapsamında YAZILMADI. Stüdyo
> sahibiyle bu beklenti go-live öncesi netleştirilmeli (bkz. [PILOT-RUNBOOK.md](PILOT-RUNBOOK.md) §4).

## 10. Pilot smoke **[B][O]**

### 10a. Otomatik doğrulama (staging/izole stack — pilot ÖNCESİ)
> İzole stack deseni (örnek): dev-compose override ile backend:5232 / frontend:3100 / postgres:5433. `<BASE>`=`http://localhost:5232`.
> MFA smoke'ları `Mfa__Enabled=true`; full suite + E2E `Mfa__Enabled=false` stack'inde koşar.

```bash
# Backend build (0/0)
cd pliaformaApi && dotnet build src/Pliaforma.Api/Pliaforma.Api.csproj -c Debug -o c:/tmp/plia-build --nologo
# has-pending clean
dotnet ef migrations has-pending-model-changes --project src/Pliaforma.Persistence --startup-project src/Pliaforma.Persistence
# Frontend tsc + next build
cd ../pliaformaFrontend && npx tsc --noEmit && npm run build

# Full smoke 38/38  (pliaformaApi/)
cd ../pliaformaApi && bash scripts/run-smokes.sh <BASE>
# Sprint 32a+ suite'leri (43/45/46/47) run-smokes.sh KAPSAMI DIŞI — özel ön koşulları var, ayrı çalıştırılır:
bash scripts/sprint43-smoke.sh <BASE>                    # move — Postgres + node gerekir
bash scripts/sprint45-smoke.sh <BASE>                    # finans (tek son ödeme tarihi)
bash scripts/sprint46-smoke.sh <BASE>                    # pilot mode — Pilot__SimplePeopleMode=true stack'te KOŞ
bash scripts/sprint46-smoke.sh <BASE>                    # AYNI script Pilot__SimplePeopleMode=false stack'te TEKRAR KOŞ
bash scripts/sprint47-smoke.sh <BASE>                    # Sprint 47 pilot guard'ları — ikisi de false/true stack'te
bash scripts/sprint47-legacy-installment-check.sh        # salt-okunur dry-run (backfill gerekiyor mu?)
bash scripts/sprint48-smoke.sh <BASE>                    # Sprint 48 security defense-in-depth — false/true stack'te
bash scripts/sprint49-smoke.sh <BASE>                    # Sprint 49 registration lockdown — ON stack'te (true) VE
bash scripts/sprint49-smoke.sh <BASE>                    #   AYRICA STUDIO_REGISTRATION_ENABLED=false stack'te TEKRAR
# MFA smoke (Mfa__Enabled=true stack)
bash scripts/mfa-setup-smoke.sh <BASE>; bash scripts/mfa-login-smoke.sh <BASE>
bash scripts/mfa-recovery-code-smoke.sh <BASE>
PG_CONTAINER=<pg> PGUSER=postgres PGDB=pliaforma ADMIN_EMAIL=<admin> ADMIN_PW=<pw> bash scripts/mfa-audit-smoke.sh <BASE>
# metrics-exposure + CSP
bash scripts/metrics-exposure-smoke.sh
CSP_FE_BASE=http://localhost:3100 CSP_API_BASE=<BASE> CSP_EXPECT=enforce bash scripts/csp-smoke.sh   # enforce doğrulama (staging)
# audit retention purge + DB append-only (worker enabled stack)
PG_CONTAINER=<pg> WORKER_CHECK=1 PF_LOG_CONTAINER=<backend> bash scripts/audit-purge-smoke.sh
# backup encryption + restore drill
PG_CONTAINER=<pg> POSTGRES_DB=pliaforma POSTGRES_USER=postgres bash scripts/backup-encryption-smoke.sh
PG_CONTAINER=<pg> POSTGRES_DB=pliaforma POSTGRES_USER=postgres bash ops/restore-drill.sh
# CI güvenlik (local docker eşdeğeri)
bash scripts/ci-security-smoke.sh ; bash scripts/security-scan.sh

# Full E2E 66  (pliaformaFrontend/, staging)
cd ../pliaformaFrontend && E2E_BASE_URL=http://localhost:3100 npm run test:e2e
```
**GitHub Actions UI kontrolü:** §1.1'deki commit üzerinde security workflow run'ı **yeşil mi** — operatör GitHub Actions UI'da teyit eder (§8.1, blocker).

### 10b. Canlı manuel smoke (gerçek domain üzerinden — pilot SIRASINDA)
> **UYARI: Otomatik smoke TEK BAŞINA YETMEZ.** Pilot canlıda, **gerçek domain (HTTPS) üzerinden** aşağıdaki akış manuel doğrulanır:
Owner register/login → **MFA enroll** → studio create/select → student create → contract line create → preferred slots →
payment create/mark-paid → class session create → regular booking → complete/unconfirm → makeup credit/request/booking →
**audit viewer** (PlatformAdmin) → **CSP report endpoint** (204) → **backup run** (şifreli `.sql.gz.gpg`) → **`/health`** → **`/metrics` public DEĞİL** (404).

### 10c. Tek-salon pilot deployment smoke (Sprint 49 — gerçek domain, sade-mod pilot akışı) **[B][O]**
Bu tek-salon pilotun uçtan uca çalıştığını canlı domainde sırayla doğrula (hepsi HTTPS, `pliaforma.com`):
1. [ ] **registration lockdown**: `/api/auth/registration-status` → `false`; `/register-studio` sayfası "Yeni stüdyo kaydı kapalı" gösterir; POST `/api/auth/register-studio` → **403**.
2. [ ] **login**: pilot Owner giriş (MFA challenge) → `/dashboard`.
3. [ ] **owner/studio**: dashboard özet kartları + stüdyo bağlamı doğru.
4. [ ] **pilot guest student**: (Owner davetli) Instructor giriş → sade-mod ad-soyad ile **guest öğrenci** ekler (Pilot mode ON).
5. [ ] **booking**: guest öğrenciyi bir ders seansına **regular booking** ile ekler (200).
6. [ ] **attendance**: seansta yoklama → complete / gelmedi; instructor "Onayla".
7. [ ] **move**: grup düzeninde öğrenciyi başka seansa **taşı** (transactional).
8. [ ] **payment**: (gerçek öğrenci sözleşmesi varsa) ödeme oluştur/mark-paid; pilot guest'te finans **gizli** kalır (borç/ödeme yüzeyi yok).
9. [ ] **dashboard**: Owner dashboard + `/platform/overview` (pending/failed bildirim, gecikmiş ödeme) tutarlı.
10. [ ] **backup**: `ops/backup-postgres.sh` → şifreli `.sql.gz.gpg` üretti.
11. [ ] **restore drill**: `ops/restore-drill.sh` → tüm kontroller GEÇTİ (wrong/correct key + izole restore).
> Not: ilk Owner "kısa pencere" ile oluşturulur (DEPLOY-DRY-RUN §10); smoke sırasında kayıt yine **kapalı** olmalı.

## 11. Rollback
| # | Senaryo | Aksiyon | Not | Status |
|---|---|---|---|---|
|11.1| Uygulama geri alma | önceki tag/commit'e checkout + redeploy | hızlı | ☐ |
|11.2| DB geri yükleme | `ops/restore-postgres.sh` (şifreli/plain) | önce yedek | ☐ |
|11.3| CSP sorunu | `CSP_MODE=report-only` veya `disabled` + restart | **rebuild gerekmez** | ☐ |
|11.4| Purge sorunu | `AuditPurge:Enabled=false` (veya `DryRun=true`) | anında durur | ☐ |
|11.5| Backup encryption | eski `.sql.gz` restore **geri uyumlu** | fallback | ☐ |
|11.6| Migration rollback | `Sprint31D_AuditAppendOnly` Down trigger'ı düşürür (veri migration'ı değil → güvenli) | **prod'da ancak yedek + bakım penceresiyle** | ☐ |
|11.7| **HSTS DİKKAT** | sticky'dir; `max-age` kısa tutuldu | **uzatmadan TLS doğrula; geri alma tarayıcıda gecikir** | ☐ |

---

## 12. GO / NO-GO

### Pilot-öncesi blocker'lar (biri bile fail → **NO-GO**)
- **§2** gerçek prod secret'lar + prod env (Jwt/MFA/Backup/Grafana key'leri, ASPNETCORE_ENVIRONMENT, prod DB, CORS/cookie).
- **§3** TLS/domain/cert + HTTP→HTTPS + secure cookie.
- **§4.1–4.3** monitoring public değil + Grafana anon kapalı + admin parolası değişmiş.
- **§5.7** deploy-öncesi DB yedeği.
- **§6** backup encryption + restore-drill (wrong/correct key + tarihli kayıt).
- **§8.1–8.2** GitHub Actions security workflow §1.1'deki commit üzerinde **yeşil** (operatör UI teyidi) + gitleaks geçti.
- **§9.1–9.3, 9.5, 9.6** DATA_PROCESSORS + IRP rolleri dolu + KVKK metinleri avukat onayı (legal blocker) + HealthNotes rıza/erişim/audit (toplanıyorsa) + tabletop drill kaydı.
- **§2.14** (Sprint 49, tek-salon pilot) `STUDIO_REGISTRATION_ENABLED=false` — dışarıdan yeni stüdyo oluşturma kapalı (register-studio 403); ilk owner "kısa pencere" ile açıldı (DEPLOY-DRY-RUN §10).
- **§10** otomatik smoke yeşil + canlı manuel smoke (gerçek domain) + **§10c tek-salon deployment smoke**.

### Post-pilot takip işleri [P]
HSTS `max-age` uzatma · off-site şifreli backup · `CSP_MODE=enforce` env-flip · `AuditPurge DryRun=false` gerçek purge · npm moderate (next/postcss) upgrade · backup key rotation prosedürü · Critical-CVE soft-gate · tabletop düzenli tekrar.

### Açık riskler
- **Actions sonucu bot tarafından görülemez** (private repo + gh unauth) → operatör GitHub UI teyidi şart (§8.1).
- **Gerçek prod secret/cert/DNS bot tarafından doğrulanamaz** → operatör sorumluluğu (§2/§3).
- **Off-site backup yok** (post-pilot) → sunucu+yedek aynı yerde kaybı riski.
- **HealthNotes** toplanırsa KVKK m.6 (özel nitelikli) yükü artar (§9.5).

### Final GO / NO-GO karar kutusu
```
Final Decision:     [ ] GO     [ ] NO-GO
Approved by:        ____________________________
Approval date:      ____________________________
Blocking failures:  ____________________________   (varsa hangi madde(ler))
Accepted risks:     ____________________________   (kabul edilen post-pilot/açık riskler)
Next review date:   ____________________________
Notes / Evidence:   ____________________________
```

---
**Özet:** Teknik güvenlik/KVKK hattı (31A→31D-3, v1.20.0-pilot) tamamlandı; **kalan GO blocker'ları operatör aksiyonlarıdır** (gerçek secret'lar, TLS/domain, monitoring kapatma, backup key + drill kaydı, Actions UI teyidi, KVKK legal + incident rolleri, canlı manuel smoke). Bu checklist eksiksiz **Pass** olmadan **pilot canlıya alınmaz**.
