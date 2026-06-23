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

## 2. Güvenlik eksikleri (bu sprintte tespit) → sprint bağlama
| Eksik | Etki | Çözüm | Sprint |
|---|---|---|---|
| **Yedek şifreleme yok** (düz `.sql.gz`) | Yedek çalınırsa **tüm PII** açık | AES-256 / PGP ile şifrele | ⏳31D / P1-7 |
| **Off-site otomatik değil** | Sunucu kaybında yedek de kaybolur | Şifreli off-site kopya (sağlayıcı [DATA_PROCESSORS.md](DATA_PROCESSORS.md)) | ⏳31D / P1-7 |
| **Periyodik restore drill kanıtı** | Yedek geçerliliği garanti değil | 3 ayda bir tarihli drill kaydı | ⏳31D |
| **Backup erişim kısıtı** | Yedek dosyalarına yetkisiz erişim | Dosya izinleri + ayrı kimlik; audit | ⏳31D |

## 2.5 Kritik secret'lar — DB yedeği yetmez (Sprint 31-MFA)
- **`Mfa:EncryptionKey`** TOTP secret'larını AES-256-GCM ile şifreler. Anahtar **`.env`'de** (DB yedeğinde DEĞİL).
  → Yalnız DB yedeği TOTP secret'larını **çözemez** (güvenlik açısından doğru). Anahtar **JWT secret gibi kritik**;
  güvenli ve ayrı bir yerde yedeklenmeli. **Anahtar kaybı** → kullanıcılar MFA'yı yeniden kurmak zorunda kalır
  (**recovery code'lar bağımsız hash'li olduğu için çalışmaya devam eder** → kurtarma + yeniden enroll mümkün).
- Anahtar rotasyonu = mevcut secret'lar çözülemez → tüm kullanıcılar yeniden enroll (pilotta önerilmez).
- **Restore tatbikatında** doğrula: `Mfa:EncryptionKey` + `Jwt:Secret` ayrı yedekten geri yüklenmeli; yalnız DB restore yetmez.

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
