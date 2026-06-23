# Saklama ve Silme Politikası (Retention & Deletion) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Süreler **öneridir**; finansal/hukuki süreler **avukat/mali müşavir onayı (TBD)** gerektirir.

KVKK ilkeleri: **veri minimizasyonu**, **amaçla sınırlılık**, **saklama süresiyle sınırlılık**. Veri, işleme amacı
ortadan kalktığında silinmeli, yok edilmeli veya anonim hâle getirilmelidir.

---

## 1. Önemli mevcut durum notu: soft-delete yeterli DEĞİL

Sistemde tüm `BaseEntity` **soft-delete** ile çalışır (`IsDeleted == true` + global query filter). Bu,
veriyi uygulama görünürlüğünden çıkarır ama **fiziksel olarak DB'de tutmaya devam eder.** KVKK açısından
"silme/yok etme" için bu tek başına yeterli değildir. Gerekli:
- belirli veriler için **hard-delete** veya **anonimleştirme** akışı (⏳ 31D / post-pilot),
- süresi dolan teknik kayıtlar için **zamanlı purge** (⏳ 31D),
- silme/anonimleştirme işlemlerinin **audit'e** düşmesi (P0-2).

---

## 2. Saklama takvimi (öneri)

| Veri kategorisi | Entity | Önerilen saklama | Süre sonu işlemi | Statü/Sprint |
|---|---|---|---|---|
| **E-posta/parola sıfırlama token'ı** | `AuthToken` | Süre dolumu + kısa tampon (örn. 7 gün) | **Hard-delete (purge job)** | ⏳31D (P1-3) |
| **Refresh token** | `RefreshToken` | `ExpiresAt` + revoke sonrası kısa tampon | **Hard-delete (purge job)** | ⏳31D (P1-3) |
| **Bildirim** | `Notification` | 90 gün (öneri) | Hard-delete/anonimleştir | ⏳ post-pilot |
| **Güvenlik audit log** | `SecurityAuditLog` (P0-2) | **2 yıl** (delil değeri; PII maskeli, append-only) | **2 yıl sonrası arşivle/sil** (kesin yöntem avukat/owner onayıyla netleşir) | ⏳31C+ |
| **Aday/guest öğrenci (dönüşmemiş)** | `StudentProfile` (Source=guest, ConvertedAt yok) | 6–12 ay aktivite yoksa | Anonimleştir/sil | ⏳ post-pilot |
| **Aktif öğrenci kimlik/iletişim** | `StudentProfile`, `User` | İlişki süresi + makul tampon | İlişki bitince anonimleştir/sil | ⏳ post-pilot |
| **Özel nitelikli (HealthNotes)** | `StudentProfile.HealthNotes` | **Mümkünse hiç tutma**; tutulursa rıza/amaç süresi | İlk fırsatta sil | **Karar: pilotta toplama (P1-8)** |
| **Sözleşme** | `StudentContract*`, `ContractAmendment` | **Finansal/hukuki süre — TBD** | TBD | ⏳ avukat |
| **Ödeme kaydı** | `Payment`, platform ödemeleri | **Finansal süre (vergi/ticaret hukuku) — TBD** | TBD | ⏳ mali müşavir |
| **Yoklama/telafi/paket ledger** | `Attendance`, `Makeup*`, `StudentPackage*` | İlişki + raporlama süresi | Anonimleştir | ⏳ post-pilot |
| **Operasyonel loglar** | uygulama logu | 30–90 gün (öneri) | Rotasyon/silme | ⏳ ops |

> Finansal kayıtlar (sözleşme/ödeme) genellikle vergi/ticaret mevzuatı gereği **daha uzun** tutulur; bu süre
> KVKK silme talebine karşı **hukuki saklama gerekçesi** oluşturabilir. **Kesin süre avukat/mali müşavir onayı ister (TBD).**

---

## 3. Anonimleştirme adayları
İlişki bittiğinde kişisel veri yerine geri döndürülemez şekilde maskelenebilecek alanlar:
- `StudentProfile`: FullName → "Silinmiş Öğrenci #id", Email/Phone/BirthDate/EmergencyContact* → null/maske, HealthNotes → sil.
- `User`: ilişkili profil silindiğinde Email/Phone/FullName anonimleştir (audit referansları korunur).
- Ledger/istatistik kayıtları: kişi referansı koparılır, sayısal/operasyonel veri raporlama için kalır.

> Anonimleştirme **geri döndürülemez** olmalı; aksi halde hâlâ kişisel veridir.

---

## 4. İlgili kişi talepleri (KVKK m.11 / "unutulma hakkı")
- Silme/anonimleştirme talebi geldiğinde: kimlik doğrula → hukuki saklama yükümlülüğü kontrolü (finansal kayıtlar) →
  yükümlülük yoksa anonimleştir/sil → işlemi audit'e yaz → ilgiliye yanıt.
- **Pilot dönemi:** tam otomatik akış yok; talepler **manuel** + bu politikaya göre işlenir. Otomatik akış post-pilot (P2-6).

---

## 5. Teknik aksiyonlar (sprint bağlama)
- **31D-1 — UYGULANDI:** in-app `AuditRetentionPurgeBackgroundService` (ReminderBackgroundService deseni). Hedefler:
  (a) **SecurityAuditLog** `OccurredAt < now-730g` fiziksel sil; (b) **AuthToken** `ExpiresAt < now-tampon`,
  **RefreshToken** `ExpiresAt < now-tampon OR (RevokedAt < now-tampon)` fiziksel hard-delete (BaseEntity soft-delete'i
  bypass). **Güvenlik kapıları:** `AuditPurge:Enabled` (dev false), `DryRun` (default true; ilk deploy true → gözlem →
  false), `AuditRetentionDays<=0` refuse, cutoff run başında sabit, batched, exception app'i düşürmez. Tur başına ≤1
  `AuditRetentionPurge` meta event (PII yok; cutoff geçmişte → kendini silmez, sonsuz döngü yok). **Bildirim purge → post-pilot.**
- **31D-1 — DB-level append-only:** PostgreSQL trigger (`Sprint31D_AuditAppendOnly`) SecurityAuditLogs'ta — **UPDATE her zaman**
  engellenir, **DELETE yalnız** oturumda `app.audit_purge='on'` (yalnız purge worker set eder) ise izinli, **INSERT serbest**.
  **Dürüst sınır:** Bu, kaza/bug/normal app update-delete akışına karşı **DB-level guard**'dır; **tam-compromised app'e karşı
  mutlak koruma DEĞİLDİR** (uygulama GUC'u set edebilir). Daha güçlü ayrım (ayrı maint DB rolü + REVOKE) → post-pilot.
- **31C:** Silme/anonimleştirme işlemleri SecurityAuditLog'a düşsün. **SecurityAuditLog retention = 2 yıl**:
  append-only, PII maskeli, normal kullanıcı akışında update/delete edilmez, soft-delete uygulanmaz, erişim çok
  sınırlı (yalnız PlatformAdmin / çok sınırlı Owner). 2 yıl sonrası arşivle/sil (kesin yöntem avukat/owner onayı).
- **post-pilot:** Öğrenci/User anonimleştirme akışı + ilgili kişi talep self-service.

## 6. Açık noktalar (TBD)
- Finansal kayıt yasal saklama süresi (avukat/mali müşavir).
- VERBİS ve saklama süreleri tablosunun resmî hâli (avukat).
- HealthNotes nihai kararı (toplanacaksa rıza + süre).
