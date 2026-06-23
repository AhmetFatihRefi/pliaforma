# Veri Envanteri (Data Inventory) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Kapsam: tüm kalıcı entity'ler (PostgreSQL) · Kaynak: kod incelemesi (HEAD)

Bu envanter, `src/Pliaforma.Domain/Entities/*` altındaki entity'lerin kişisel veri içeriğini sınıflandırır.
KVKK m.6 "özel nitelikli kişisel veri" riski taşıyan alanlar ayrıca işaretlenmiştir.

## Hassasiyet seviyeleri

- **ÖZEL NİTELİKLİ** — KVKK m.6 (sağlık, vb.). En yüksek koruma; açık rıza/ayrı tedbir gerekir.
- **YÜKSEK** — kimlik, iletişim, finans, sözleşme, adres.
- **ORTA** — roller, statü, aktivite zaman damgaları, token meta (IP/UserAgent).
- **DÜŞÜK** — operasyonel/referans veri; tek başına kişisel veri değil.

## Erişim rolleri (referans)

`Roles.cs`: GlobalRole = `Standard` | `PlatformAdmin`. StudioRole = `Owner` | `StudioAdmin` | `Instructor` | `Student`.
Tüm tenant verisi `StudioId` ile izole (`ITenantScoped`); erişim handler seviyesinde `StudioId == currentUser.StudioId`
ile doğrulanır (bkz. [../SECURITY-REVIEW.md](../SECURITY-REVIEW.md) §2).

---

## 1. Özel nitelikli / özel nitelikli veri riski

| Entity (alan) | Tutulan veri | Kişisel? | Özel nitelikli riski | Hassasiyet | Erişen roller | İşleme amacı | Retention önerisi | Audit ihtiyacı |
|---|---|---|---|---|---|---|---|---|
| `StudentProfile.HealthNotes` | Serbest metin sağlık/sakatlık notu (≤2000) | Evet | **EVET (m.6 sağlık)** | ÖZEL NİTELİKLİ | Owner/StudioAdmin (Instructor erişimi 31C'de karara bağlanacak), öğrencinin kendisi | Ders güvenliği/uyarlama | **Veri minimizasyonu: pilotta toplanMAZ; tutulacaksa sözleşme+rıza süresi** | **Yüksek — erişim + değişiklik audit** |
| `StudentProfile.Notes` | Serbest metin genel not (≤2000) | Evet | Olası (sağlık yazılabilir) | YÜKSEK | Owner/StudioAdmin, oluşturan Instructor | Operasyonel not | Öğrenci ilişkisi süresi | Değişiklik audit |
| `Attendance.Notes` | Yoklama notu (serbest metin) | Evet | Olası (devamsızlık/sağlık sebebi) | ORTA-YÜKSEK | Owner/StudioAdmin/Instructor | Yoklama açıklaması | Ders kaydı süresi | Önerilir |
| `MakeupRequest.Description` / `DecisionNote` | Telafi gerekçesi / karar notu | Evet | Olası (sağlık sebebi) | ORTA | Owner/StudioAdmin/Instructor | Telafi yönetimi | İşlem süresi + finansal | Önerilir |
| `ClassSession.Notes` / `RescheduleReason` | Seans/erteleme notu | Olası | Olası | ORTA | Owner/StudioAdmin/Instructor | Operasyon | Ders kaydı süresi | Düşük |
| `ClassBooking.CancellationReason` | İptal gerekçesi | Olası | Olası | ORTA | Staff | Operasyon | Ders kaydı süresi | Düşük |
| `Notification.Body` / `Title` | Bildirim içeriği (≤1000/≤200) | Evet | Olası (tutar/isim/sağlık referansı) | ORTA-YÜKSEK | Alıcı kullanıcı (`RecipientUserId==self`) | Bilgilendirme | Kısa (90 gün öneri) | Düşük |

> **Karar — HealthNotes (özel nitelikli):** Veri minimizasyonu uygulanacaktır. Zorunlu değilse pilotta
> **aktif toplanmayacak.** Tutulacaksa: (a) aydınlatma + açık rıza ihtiyacı dokümante edilir, (b) erişim
> Owner/StudioAdmin ile sınırlandırılır, (c) Instructor erişimi 31C'de ayrıca tartışılır, (d) erişim/değişiklik
> SecurityAuditLog'a düşer, (e) serbest metin alanlarına sağlık verisi yazılmaması için UI uyarısı değerlendirilir.
> Teknik aksiyonlar 31B/31C'ye bölünür. Risk: [RISK_REGISTER.md](RISK_REGISTER.md) → P1-8.

---

## 2. Yüksek hassasiyet (kimlik / iletişim / finans / sözleşme)

| Entity (alan) | Tutulan veri | Kapsam | Hassasiyet | Erişen roller | Amaç | Retention önerisi | Audit |
|---|---|---|---|---|---|---|---|
| `User` (Email, Phone, FullName, PasswordHash, LastLoginAt) | Kimlik + iletişim + BCrypt hash | **Global (tenant-dışı)** | YÜKSEK | Kendi; PlatformAdmin | Kimlik doğrulama | Hesap aktif olduğu sürece | Login/rol audit |
| `StudentProfile` (FullName, Email, Phone, BirthDate, EmergencyContactName/Phone) | Öğrenci kimlik + acil durum + doğum tarihi | Tenant | YÜKSEK | Owner/StudioAdmin, oluşturan Instructor, kendisi | Öğrenci yönetimi | İlişki + yasal süre | Erişim/değişiklik |
| `StudentContract` / `StudentContractLine` / `ContractAmendment` (Price, tutarlar, DueDate, Notes) | Sözleşme finansal şartları | Tenant | YÜKSEK | Owner/StudioAdmin; Student kendi sözleşmesi (read-only) | Sözleşme/tahsilat | **Finansal — yasal saklama (TBD avukat)** | **Oluştur/değiştir/sil audit** |
| `Payment` (Amount, DueDate, PaidAt, Notes) | Ödeme kaydı | Tenant | YÜKSEK | Owner/StudioAdmin | Tahsilat | **Finansal — yasal saklama (TBD)** | **Audit** |
| `InstructorProfile` (DisplayName, Bio, Rate) | Eğitmen ücreti/profil | Tenant | YÜKSEK (Rate) | Owner/StudioAdmin; kendisi | Bordro/atama | İlişki süresi | Değişiklik audit |
| `Studio` (ContactEmail, ContactPhone, AddressLine, City, SuspendedReason) | Stüdyo iletişim/adres | **Global** | YÜKSEK | Owner/StudioAdmin; PlatformAdmin | Tenant yönetimi | Tenant aktif süresi | Düşük |
| `PlatformSubscription` / `PlatformSubscriptionPayment` (MonthlyPrice, tutarlar, dönemler) | Platform abonelik/ödeme | Platform | YÜKSEK | PlatformAdmin | Faturalama | Finansal — yasal | Audit |

---

## 3. Orta hassasiyet (rol / statü / token meta)

| Entity (alan) | Tutulan veri | Hassasiyet | Not |
|---|---|---|---|
| `StudioMembership` (Role, Status, AcceptedAt) | Kullanıcının stüdyodaki yetkisi | ORTA | **Rol değişikliği audit gerekir** |
| `AuthToken` (TokenHash, Purpose, ExpiresAt, ConsumedAt, CreatedIp, UserAgent) | E-posta doğrulama/parola sıfırlama tek-kullanımlık token | ORTA | **Ham token TUTULMAZ; sadece SHA-256 hash.** IP/UserAgent saklanıyor → retention gerekir |
| `RefreshToken` (TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash, CreatedByIp) | Oturum yenileme | ORTA | **Sadece SHA-256 hash.** Rotasyonlu; süresi dolan kayıtlar temizlenmeli |
| `User.LastLoginAt`, `StudioMembership.AcceptedAt`, `StudentProfile.ConvertedAt` | Aktivite zaman damgaları | ORTA | Davranışsal meta |
| `User.FailedLoginCount`, `User.LockoutEnd` | Hesap kilitleme sayacı/kilit (Sprint 31B-2) | DÜŞÜK-ORTA | Brute-force koruması; başarı/reset/change'de sıfırlanır |
| `SecurityAuditLog` (Sprint 31B-2) | Güvenlik denetim olayları: `EventType, Outcome, ActorUserId?, **ActorEmailMasked** (maskeli), StudioId?, Ip?, UserAgent?, Detail?` | ORTA | **Append-only**, BaseEntity değil (soft-delete yok). **PII maskeli** (ham e-posta/parola/token/secret/request-body ASLA). **Ip + UserAgent**: güvenlik/denetim amaçlı (KVKK meşru menfaat). **Retention: 2 yıl** ([RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md)); purge job 31D. Erişim: yalnız PlatformAdmin (viewer 31C). |

---

## 4. Düşük hassasiyet (operasyonel / referans)

`ClassType`, `ClassBooking` (statü), `StudentPackage` / `StudentPackageTransaction`, `MakeupCreditTransaction`,
`PackagePlan*`, `InstructorClassType`, `InstructorAvailability`, `StudioClosure`, `StudioMakeupPolicy`,
`StudentContractLinePreferredSlot`, `SubscriptionPlan`. Çoğu serbest metin `Notes` alanı içerir (sağlık verisi
yazılmaması UI uyarısı kapsamına alınmalı) ama tek başına kişisel veri değildir.

---

## 5. Yatay konular

- **Soft-delete:** Tüm `BaseEntity` global query filter ile `IsDeleted == false` filtrelenir. Veri fiziksel
  olarak silinmez → retention/anonimleştirme açığı (bkz. [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md)).
- **Tenant izolasyonu:** Global entity'ler (`User`, `Studio`, `AuthToken`, `RefreshToken`, `SubscriptionPlan`,
  `PlatformSubscription`) `StudioId` ile izole **değildir**; raporlama/export'ta dikkat.
- **Loglara PII:** Prod JSON log; request body / parola / token loglanmaz. Tek istisna: dev'de `LogEmailSender`
  parola sıfırlama linkini loglar (prod'da kapalı). Log toplama prod'da bu kaynağı dışlamalı.
- **Dışa aktarım (export):** Ödeme/öğrenci raporları PlatformAdmin/Owner erişiminde; bunlar audit kapsamına
  alınmalı (P0-2).

## 6. Açık noktalar
- Finansal kayıtların (sözleşme/ödeme) yasal saklama süresi → **KVKK/mali müşavir onayı (TBD).**
- HealthNotes pilotta toplanacak mı → owner kararı (varsayılan: HAYIR).
- Instructor'ın HealthNotes/Notes erişim sınırı → 31C.
