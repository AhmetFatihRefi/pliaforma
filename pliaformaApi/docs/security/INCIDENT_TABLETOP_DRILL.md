# Incident Tabletop Drill — Pliaforma (Sprint 31D-3)

Statü: **TASLAK — masa başı tatbikat senaryosu.** Bu doküman mevcut planları **icra eder**, kopyalamaz.
> Birincil kaynaklar (atıf): [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) (IRP) · [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) ·
> [DATA_INVENTORY.md](DATA_INVENTORY.md) · [DATA_PROCESSORS.md](DATA_PROCESSORS.md) · [SECURITY_BASELINE_CHECKLIST.md](SECURITY_BASELINE_CHECKLIST.md).
> Tatbikat = ekibin bu planları uçtan uca yürütmesi; bulunan eksikler [RISK_REGISTER.md](RISK_REGISTER.md)'a aktarılır (IRP §7).

---

## 1. Senaryo özeti + tetik
**Senaryo:** Off-site/depolama alanından bir **prod DB yedek dosyası (`pliaforma_*.sql.gz.gpg`) yetkisiz erişime/çalınmaya** uğradı
(ör. yanlış yapılandırılmış depolama, çalınan operatör cihazı, sağlayıcı ihlali). **Tetik:** depolama erişim logunda anomali /
sağlayıcı uyarısı / operatör fark etmesi.

**Önemli bağlam (Sprint 31D-2):** Yedek **gpg AES256 ile şifrelidir** ve **`BACKUP_ENCRYPTION_KEY` yedekle aynı yerde DEĞİLDİR**
(kritik recovery secret seti: `JWT_SECRET` / `MFA_ENCRYPTION_KEY` / `BACKUP_ENCRYPTION_KEY` — bkz. [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md)).
→ Saldırgan **yalnız** şifreli dosyayı ele geçirdiyse içeriği **çözemez** → kişisel veri maruziyeti **düşük**. **Ama olay ciddiye alınır**:
anahtarın da sızıp sızmadığı **doğrulanana kadar** ihlal varsayımıyla hareket edilir (karar kaydı §9).

## 2. SEV sınıflandırma (IRP §3)
- **Yalnız şifreli yedek sızdı + key güvende doğrulandı →** SEV-2 (yüksek; veri ifşası doğrulanmadı, kontrol mevcut).
- **Şifreli yedek + `BACKUP_ENCRYPTION_KEY` birlikte sızdı →** **SEV-1** (kişisel veri ifşası kuvvetle muhtemel) → tam IRP + breach runbook.
- İlk an: kanıt aksini gösterene kadar **SEV-1 varsay** (temkinli), key güvende doğrulanınca SEV-2'ye indir.

## 3. 0–72 saat zaman çizelgesi (IRP §4'ün somutlaştırması)
| Faz | Aksiyon (bu senaryoya özgü) | IRP atıf |
|---|---|---|
| **0–1 saat** | Koordinatöre bildir, SEV ata. **Anahtar deposu erişim logunu** incele: `BACKUP_ENCRYPTION_KEY` sızdı mı? Yedek dosyasının hash'i + erişim zaman damgaları korunsun. | IRP §4 "İlk 1 saat" |
| **1–24 saat** | **Kapsama:** anahtar sızma ihtimali varsa → **anahtar rotasyonu** (yeni `BACKUP_ENCRYPTION_KEY`; eski yedekler için eski key güvenli arşive); etkilenen depolama erişimini iptal et; sızan yedeğin hangi tarih/kapsama ait olduğunu belirle. **Etki analizi:** yedek hangi tablo/tarih → hangi kişisel veri kategorileri (§8). | IRP §4 "İlk 24 saat" |
| **24–72 saat** | SEV-1 doğrulandıysa **Kurul bildirimi** hazırla (breach runbook). Tüm aksiyonlar zaman damgalı (delil zinciri §12). | IRP §4 + breach runbook |
| **Kapanış** | RCA (depolama nasıl açıktı, off-site erişim kontrolü eksik miydi), düzeltici aksiyon → RISK_REGISTER, kapanış raporu (IRP §6 şablonu). | IRP §4 kapanış |

## 4. RACI (IRP §1 — roller TBD; tatbikatta atanır)
| Aksiyon | Sorumlu (R) | Onay (A) | Danışılan (C) | Bilgilendirilen (I) |
|---|---|---|---|---|
| SEV atama / koordinasyon | Olay koordinatörü | Veri sorumlusu | Teknik müdahale | Tüm ekip |
| Anahtar sızma doğrulama + rotasyon | Teknik müdahale | Koordinatör | — | Koordinatör |
| Etki/kapsam analizi | Teknik müdahale | Koordinatör | KVKK avukatı | Veri sorumlusu |
| Bildirim kararı (Kurul/ilgili kişi) | Veri sorumlusu | Veri sorumlusu | KVKK avukatı | İletişim/PR |
| Delil zinciri / kayıt | Koordinatör | — | Teknik | — |

## 5. İlk izolasyon adımları
- Sızan yedeğin kaynağı (depolama/off-site/cihaz) **erişimini kes**; ilgili kimlik bilgilerini iptal et.
- **`BACKUP_ENCRYPTION_KEY` rotasyonu** (sızma ihtimali varsa); rotasyon sonrası eski key yalnız **eski yedekleri açmak için** güvenli arşivde.
- Şüpheli ise `JWT_SECRET` rotasyonu (tüm token geçersiz) — IRP §4 "İlk 24 saat".
- Depolama erişim politikasını sıkılaştır (geçici).

## 6. Delil toplama (IRP §5)
- **Yedek dosyasının hash'i** (SHA-256) + sızan kopyanın kapsamı (hangi tarih/DB) kayıt altına alınır.
- Depolama/off-site **erişim logları** + anahtar deposu erişim logları **değiştirilmeden** kopyalanır (UTC zaman damgası).
- **Audit log kontrolü:** [SecurityAuditLog](RETENTION_AND_DELETION_POLICY.md) **append-only + DB-level trigger** (31D-1) → **birincil delil**;
  olay penceresinde olağandışı login/rol değişikliği/`AuditRetentionPurge`/export var mı incelenir (update/delete edilemez → delil bütünlüğü yüksek).

## 7. Backup encryption / key ayrılığı değerlendirmesi (31D-2 bağı)
- Yedek **şifreli** (`gpg AES256`) → düz `.sql.gz` değil → çalınan dosya **tek başına okunamaz**.
- **`BACKUP_ENCRYPTION_KEY` yedekle aynı yerde mi tutuluyordu?** (HAYIR olmalı — runbook kuralı). Eğer ihlal anında **ayrı** ise → maruziyet **düşük**.
- **Karar:** anahtarın sızıp sızmadığı **kanıtla doğrulanır**. Doğrulanamıyorsa **temkinli olarak sızmış varsay** (SEV-1).
- **Tatbikat öğrenimi:** şifreleme + key-ayrılığı, "çalınan yedek = otomatik ihlal" senaryosunu **kontrol altına alıyor** mu? (evet/kısmi → §15).

## 8. Etkilenen veri kategorileri ([DATA_INVENTORY.md](DATA_INVENTORY.md))
Yedek tüm tabloları içerir → potansiyel kategoriler (yalnız anahtar da sızdıysa relevant):
- **Kimlik/iletişim** (YÜKSEK): `User`, `StudentProfile` (FullName/Email/Phone/BirthDate/EmergencyContact).
- **Özel nitelikli — sağlık** (ÖZEL NİTELİKLİ, KVKK m.6): `StudentProfile.HealthNotes` (pilotta toplanMAMA kararı; toplandıysa ağırlaştırıcı).
- **Finans** (YÜKSEK): `Payment`, `StudentContract*`.
- **Kimlik doğrulama:** `PasswordHash` (BCrypt), token **hash'leri**, **TOTP secret'ları şifreli** (`MFA_ENCRYPTION_KEY` ayrı → key olmadan çözülemez), recovery code **hash'leri**.
- **Audit:** `SecurityAuditLog` (PII maskeli).

## 9. KVKK bildirim eşiği değerlendirmesi + karar kaydı
- **Soru:** Kişisel veri ihlali "öğrenildi" mi? (KVKK m.12) — şifreli yedek + **key güvende** → ifşa gerçekleşmedi → **bildirim gerekmeyebilir** (karar avukat/veri sorumlusu).
- **Ama key de sızdıysa / doğrulanamıyorsa** → ifşa muhtemel → **72 saat sayacı başlar** ([BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) §2).
- **Karar kaydı (zorunlu):** "Anahtar durumu: [güvende/sızdı/bilinmiyor] → SEV: [..] → Kurul bildirimi: [gerekli/gerekmez] → gerekçe: [..] → karar veren: [..] → tarih/saat UTC: [..]".

## 10. Kurul bildirimi hazırlık kontrolü
SEV-1 ise [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) §3–4 (gerekli bilgiler + Kurul şablonu) doldurulur: ihlalin niteliği,
etkilenen veri kategorileri (§8), tahmini kişi sayısı, olası sonuçlar, alınan/önerilen tedbirler. **72 saat** içinde (öğrenme anından).

## 11. İlgili kişi bildirimi değerlendirmesi
Yüksek risk varsa (özellikle **sağlık/finans** ifşası) → ilgili kişilere bildirim ([BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) §5 şablonu),
makul en kısa sürede. Şifreli + key güvende ise muhtemelen gerekmez (karar kaydı §9).

## 12. Delil zinciri
Tüm dosya/log kopyaları **hash**'lenir; kim/ne zaman/nereye kopyaladı kaydedilir; orijinaller değiştirilmez (IRP §5).
Audit log (append-only) ve zaman damgalı karar kayıtları birincil delildir.

## 13. Karar kayıtları (örnek tablo — tatbikatta doldurulur)
| Zaman (UTC) | Karar | Gerekçe | Karar veren |
|---|---|---|---|
| | SEV-1 → SEV-2 (key güvende doğrulandı) | Anahtar deposu logunda erişim yok | |
| | Kurul bildirimi gerekmez | İfşa gerçekleşmedi (şifreli + key ayrı) | |

## 14. Tabletop sonuç formu
```
Tatbikat tarihi (UTC):
Katılımcılar / roller:
Senaryo: Çalınan şifreli prod DB yedeği
Süre (tespit→karar):
72h sayacı simülasyonu (öğrenme anı → bildirim kararı):
Doğru çalışan kontroller:   [backup encryption / key ayrılığı / append-only audit / ...]
Bulunan eksikler (gap):
RISK_REGISTER'a eklenen maddeler:
Aksiyon sahipleri / termin:
Sonuç (geçti / iyileştirme gerekli):
```

## 15. Post-incident aksiyonlar → RISK_REGISTER
Tatbikatta bulunan eksikler (ör. off-site erişim kontrolü, anahtar deposu izleme, IRP rollerinin TBD'den çıkarılması) **tarihli madde** olarak
[RISK_REGISTER.md](RISK_REGISTER.md)'a eklenir. Tatbikat **düzenli** tekrarlanır (öneri: yılda 1–2; IRP §7).
