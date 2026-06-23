# Sprint 31D-2 — Backup Encryption + Restore Drill

Statü: **ONAYLANDI — uygulama** · Zemin: …31D-1→v1.18 · HEAD `d58ada6` · Tag (yeşilse): **v1.19.0-pilot**
**App kodu YOK · Migration YOK** — yalnız ops scriptleri + config + docs. Tek sprint.

> Hedef: DB backup'ı **şifreli** hale getirmek + **restore drill** ile yedeğin gerçekten geri yüklenebilirliğini kanıtlamak.

## Doğrulanan ortam (karar gerekçesi)
- **`openssl enc -aes-256-gcm` → "AEAD ciphers not supported"** (openssl enc authenticated şifreleme YAPAMAZ; CBC bütünlük vermez) → **RED**.
- **gpg 2.2.41 host'ta mevcut**; batch/loopback round-trip doğrulandı: AES256 encrypt → binary (plaintext yok), doğru parola decrypt, **yanlış parola exit 2** (MDC bütünlük) → **SEÇİLDİ**.
- **age kurulu DEĞİL** (her host'a binary kurulmalı) → kullanılmayacak.

## Onaylanan kararlar
1. **Tool: gpg symmetric AES256.** age/openssl YOK. Runbook: "backup host'unda gpg kurulu olmalı; yoksa script net hata".
2. **Dev'de key yoksa → uyarı + plain `.sql.gz`**; **test/smoke şifreli yolu throwaway key ile doğrular**; **prod'da plain YASAK**.
3. **Prod / `REQUIRE_BACKUP_ENCRYPTION=1` + key yoksa → fail-fast** (şifresiz prod yedeği üretilmez).
4. **Backward compat:** `*.sql.gz.gpg` → gpg decrypt→gunzip→psql; `*.sql.gz` → gunzip→psql; bilinmeyen uzantı → net hata.
5. **Key rotation 31D-2 dışı** (post-pilot) — runbook notu: eski yedekler için eski key saklanır; key kaybı = o yedek kalıcı erişilemez.
6. **Restore drill yarı-otomatik:** DB-level otomatik; app-health/smoke subset runbook'ta **opsiyonel/manuel** (script zorunlu kılmaz).
7. **Off-site bu sprint dışı** — runbook notu ("şifreli off-site kopya önerilir; post-pilot/31E").
8. **Passphrase: `--passphrase-fd 3` + process substitution** (`3< <(printf '%s' "$KEY")`). argv'ye düşmez, temp key dosyası yok, `set -x`/loglarda key görünmez. `#!/usr/bin/env bash`.

## A. `ops/backup-postgres.sh`
- `BACKUP_ENCRYPTION_KEY` varsa: `pg_dump --clean --if-exists | gzip | gpg --symmetric --cipher-algo AES256 --batch --yes --pinentry-mode loopback --passphrase-fd 3 -o <db>_<UTC>.sql.gz.gpg` (key fd 3'ten).
- Key yoksa: **prod / `REQUIRE_BACKUP_ENCRYPTION=1` → fail-fast**; **dev → uyarı + `.sql.gz`** (plain).
- gpg yoksa (key verilmişken) → net hata + exit.
- Retention rotasyonu **`.sql.gz` + `.sql.gz.gpg`** kapsar. Log: `ENCRYPTED (gpg AES256)` / `PLAIN (dev)`; **gerçek key loglanmaz**.

## B. `ops/restore-postgres.sh`
- Uzantıya göre: `*.sql.gz.gpg` → `gpg -d (passphrase-fd 3) | gunzip | psql`; `*.sql.gz` → eski yol; başka → net hata.
- Şifreli + key yok → açık hata. **Yanlış key → gpg fail → restore durur** (ON_ERROR_STOP korunur). Tablo sayımı korunur. Migration-history kontrolü opsiyonel (drill'de zorunlu).

## C. `ops/restore-drill.sh` (YENİ, yarı-otomatik)
throwaway key ile: backup al → **şifreli doğrula** (içerik düz gunzip ile okunamaz; gpg magic) → **wrong-key decrypt FAIL** → **correct-key decrypt OK** → **temp DB** (`pliaforma_drill_<ts>`) restore → doğrula → **temp DB drop**.
Doğrulamalar: backup oluştu · şifreli · wrong-key fail · correct-key ok · temp restore ok · **`__EFMigrationsHistory` son = `Sprint31D_AuditAppendOnly`** · kritik tablo sayımları · **SecurityAuditLogs sayımı** · **UserMfaSettings/MfaRecoveryCodes varlık/sayım**. App-health/smoke = runbook'ta manuel.

## D. `scripts/backup-encryption-smoke.sh` (YENİ)
gpg var mı · encrypted backup üretildi · `.sql.gz.gpg` oluştu · plain SQL aranıp bulunamadı · wrong-key restore fail · correct-key restore success · restore-drill success · eski `.sql.gz` restore hâlâ çalışıyor · retention rotasyonu bozulmadı · gerçek key repo'ya girmedi.

## E. Config
`deploy/.env.example`: `BACKUP_ENCRYPTION_KEY=CHANGE_ME_strong_backup_passphrase` (placeholder). `docker-compose.prod.yml`: backup env notu (backup script compose dışında host'tan koşar; not olarak eklenir). **Gerçek key yazılmaz.**

## F. Docs
`BACKUP_AND_RESTORE_RUNBOOK.md` · `SECURITY_BASELINE_CHECKLIST` (P1-7) · `RISK_REGISTER` (P1-7) · (gerekirse `DATA_INVENTORY`) · bu spec. Açıkça: backup key DB yedeğiyle aynı yerde tutulMAZ · key kaybı = encrypted backup kaybı · **JWT_SECRET/MFA_ENCRYPTION_KEY/BACKUP_ENCRYPTION_KEY birlikte kritik recovery secret seti** · düzenli restore drill · off-site şifreli kopya post-pilot önerilir · gpg gereklidir.

## G. Kapsam dışı
CI scans · incident tabletop · DB append-only/retention değişikliği · CSP report-to · off-site implementation · product feature · gerçek secret/key.

## H. Doğrulama
`bash -n` (değişen scriptler) · gpg version · backup-encryption-smoke · restore-drill · wrong-key fail · correct-key success · eski `.sql.gz` restore regresyon · retention rotasyon · has-pending clean · backend 0/0 (app kodu değişmese de raporla) · git status · gerçek key/secret/cert yok taraması. Full smoke/E2E **gerekmez** (app davranışı değişmez); backup/restore smoke yeşil zorunlu.

## Rapor (bitişte)
değişen dosyalar · tool kararı + gpg kanıtı · encrypted backup kanıtı · wrong-key fail · correct-key restore · restore-drill · plain restore (geri uyum) · retention rotasyon · gerçek key sızmadı · docs/runbook · açık riskler · commit/push/tag yapılmadı.
