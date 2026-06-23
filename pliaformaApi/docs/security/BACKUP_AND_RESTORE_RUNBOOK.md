# Yedekleme & Geri Yükleme Runbook'u (Güvenlik Eki) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · **Bu doküman mevcut runbook'u çoğaltmaz.**

> **Birincil kaynak:** [../BACKUP-RESTORE.md](../BACKUP-RESTORE.md) — yedek alma, geri yükleme, cron örneği,
> restore drill ve `docker compose down -v` uyarısı orada tanımlıdır. Komutlar/scriptler için **oraya bakın.**
> İlgili scriptler: `ops/backup-postgres.sh`, `ops/restore-postgres.sh`. Pilot bağlamı: [../PILOT-RUNBOOK.md](../PILOT-RUNBOOK.md) §5.

Bu doküman yalnızca **KVKK/güvenlik açısından ekleri** ve eksikleri kaydeder.

---

## 1. Mevcut durum (doğrulanan)
- ✅ Otomatik yedek scripti `ops/backup-postgres.sh` (`.sql.gz`), `RETENTION_DAYS` ile rotasyon.
- ✅ Geri yükleme scripti `ops/restore-postgres.sh` (geçici DB'ye restore + tablo doğrulama).
- ✅ Restore drill prosedürü dokümante ([../BACKUP-RESTORE.md](../BACKUP-RESTORE.md)).
- ✅ Migration öncesi elle yedek talimatı; rollback planı.

## 2. Güvenlik eksikleri → sprint bağlama
| Eksik | Etki | Çözüm | Sprint |
|---|---|---|---|
| **Yedek şifreleme** | Yedek çalınırsa **tüm PII** açık | **gpg AES256 ile şifreli `.sql.gz.gpg`** | ✅ **31D-2 / P1-7** |
| **Restore drill kanıtı** | Yedek geçerliliği garanti değil | **`ops/restore-drill.sh`** (yarı-otomatik; düzenli koşulmalı) | ✅ **31D-2** |
| **Off-site otomatik değil** | Sunucu kaybında yedek de kaybolur | Şifreli off-site kopya (sağlayıcı [DATA_PROCESSORS.md](DATA_PROCESSORS.md)) | ⏳ **post-pilot / 31E** |
| **Backup erişim kısıtı** | Yedek dosyalarına yetkisiz erişim | Dosya izinleri + ayrı kimlik; audit | ⏳ post-pilot |

## 2.1 Yedek şifreleme (Sprint 31D-2)
- **Araç: gpg symmetric AES256.** `ops/backup-postgres.sh`, `BACKUP_ENCRYPTION_KEY` verilirse `pg_dump | gzip | gpg`
  ile **`.sql.gz.gpg`** üretir (passphrase `--passphrase-fd 3` ile; argv/loglara DÜŞMEZ). **`gpg` backup host'unda kurulu olmalı**
  (yoksa script net hata verir). `openssl enc -aes-256-gcm` DESTEKLENMEZ (AEAD yok); `age` kurulu değil → gpg seçildi.
- **Prod:** `BACKUP_ENCRYPTION_KEY` zorunlu — yoksa (veya `REQUIRE_BACKUP_ENCRYPTION=1`) backup **fail-fast** (şifresiz prod yedeği üretilmez).
  **Dev:** key yoksa **uyarı + şifresiz `.sql.gz`** (yalnız geliştirme).
- **Restore:** `ops/restore-postgres.sh` `.sql.gz.gpg` (key ile decrypt) ve eski `.sql.gz` (geri uyum) destekler; **yanlış key → restore durur**.
- **Key rotation:** bu sprint dışı (post-pilot). Rotasyonda **eski key saklanmalı** (eski şifreli yedekleri açmak için). **Key kaybı = o şifreli yedek kalıcı erişilemez.**
- **Off-site:** şifreli yedeğin off-site kopyası **önerilir** (post-pilot/31E; bu sprintte uygulanmadı).

## 2.5 Kritik secret'lar — DB yedeği yetmez (Sprint 31-MFA)
- **`Mfa:EncryptionKey`** TOTP secret'larını AES-256-GCM ile şifreler. Anahtar **`.env`'de** (DB yedeğinde DEĞİL).
  → Yalnız DB yedeği TOTP secret'larını **çözemez** (güvenlik açısından doğru). Anahtar **JWT secret gibi kritik**;
  güvenli ve ayrı bir yerde yedeklenmeli. **Anahtar kaybı** → kullanıcılar MFA'yı yeniden kurmak zorunda kalır
  (**recovery code'lar bağımsız hash'li olduğu için çalışmaya devam eder** → kurtarma + yeniden enroll mümkün).
- Anahtar rotasyonu = mevcut secret'lar çözülemez → tüm kullanıcılar yeniden enroll (pilotta önerilmez).
- **Restore tatbikatında** doğrula: `Mfa:EncryptionKey` + `Jwt:Secret` ayrı yedekten geri yüklenmeli; yalnız DB restore yetmez.

### Kritik recovery secret seti (birlikte yedeklenmeli)
`Jwt:Secret` · `Mfa:EncryptionKey` · **`BACKUP_ENCRYPTION_KEY` (31D-2)** — üçü birlikte **kritik recovery secret setidir**.
DB yedeğiyle **AYNI yerde tutulMAZ**; ayrı, güvenli, erişim-kontrollü bir yerde yedeklenir. Restore semantiği:
- **`BACKUP_ENCRYPTION_KEY` kaybı** → o şifreli yedek **açılamaz** (kalıcı kayıp).
- **`Mfa:EncryptionKey` kaybı** → DB restore olsa bile TOTP secret'lar **çözülemez** → kullanıcı yeniden enroll
  (recovery code hash'leri bağımsız → çalışır). Drill satırların VAR olduğunu doğrular, çözülmeyi değil.
- **`Jwt:Secret` değişimi** → mevcut access/refresh token'lar geçersizleşir → yeniden login (**beklenen davranış**).

### Restore drill (Sprint 31D-2) — `ops/restore-drill.sh` (yarı-otomatik)
Throwaway key ile: şifreli yedek al → şifreli doğrula → **yanlış key FAIL** → doğru key OK → **izole temp DB**'ye restore →
`__EFMigrationsHistory` son migration + kritik tablo sayımları + **SecurityAuditLogs** + **UserMfaSettings/MfaRecoveryCodes** doğrula →
temp DB drop. **Düzenli** (öneri: 3 ayda bir, tarihli kayıt §5) koşulmalı. **App-health + smoke subset = opsiyonel/manuel** (script zorunlu kılmaz):
restore sonrası temp DB'ye app'i bağlayıp `/health` + kısa smoke subset koşulabilir.

## 3. Yedek güvenliği prensipleri (hedef)
- Yedekler **şifreli** saklanır (anahtar yedeklerden **ayrı** yönetilir).
- Yedek deposu **erişim kontrollü**; sadece yetkili kimlik erişir.
- Yedek alma/geri yükleme işlemleri **kayıt altına** alınır (kim, ne zaman).
- Geri yükleme testi **prod'a değil** geçici/izole DB'ye yapılır (mevcut prosedür böyle).
- Şifreleme anahtarı kaybı = yedek kaybı → anahtar yönetimi dokümante edilmeli (⏳31D).

## 4. Pilot öncesi minimum (checklist)
- [ ] Son 24 saatte başarılı yedek alındı (`ops/backup-postgres.sh`)
- [ ] Geçici DB'ye **restore testi** yapıldı + tablo/satır doğrulandı (tarih: ____)
- [ ] Yedek dosyaları repo dışında, `.gitignore` kapsamında (`*.sql/*.sql.gz`, `ops/backups/`) — ✅ mevcut
- [ ] (31D) Şifreli + off-site kopya planı onaylı

## 5. Restore drill kayıt şablonu
```
Drill tarihi (UTC):
Yedek dosyası / boyut:
Geçici DB adı:
Doğrulanan tablolar / satır sayıları:
Sonuç (başarılı/başarısız):
Süre:
Yapan:
Notlar:
```

---
**Özet:** Yedek/geri yükleme prosedürü **işlevsel olarak hazır** ([../BACKUP-RESTORE.md](../BACKUP-RESTORE.md)).
KVKK için kalan: **şifreleme + off-site + periyodik drill kanıtı + erişim kısıtı** → hepsi **Sprint 31D / P1-7**.
