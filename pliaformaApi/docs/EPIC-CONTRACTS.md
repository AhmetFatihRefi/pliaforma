# EPIC — Sözleşme-Tabanlı Program & Telafi Modeli

> Durum: Onaylı epic. Faz faz uygulanır. Bu dosya **kalıcı epic referansıdır**; tarihi sprint dosyalarının
> üstüne yazılmaz. Sprint detayları ayrı `SPRINT-1X-*.md` dosyalarında.

## Amaç (neden)

Stüdyonun gerçek iş akışı **kredi/paket** değil, **öğrenci–stüdyo sözleşmesi** üzerinden işler: öğrenci hangi
ders tipinden, hangi hocayla, hangi gün/saat, hangi tarih aralığında, kaç ders, kaç telafi, ne ücret diye
anlaşır. **Program bu sözleşmeden üretilir; sözleşmesiz Regular ders programa eklenemez.** Stüdyo, hoca hak
edişini sisteme hesaplatmak istemez — yalnız "hangi hoca, hangi ders tipinden, kaç ders, kaç öğrenci" raporu.

Bu epic domaini sözleşme merkezli kurar; **paket/kredi** ile **hoca-ücret** mantığını **deprecate** eder
(JoinCode deseni: DB'de kalır, üründen çıkar; **drop/migration YOK**).

## Kesin ürün kararları (onaylı)

- Yeni ana kaynak: **öğrenci–stüdyo sözleşmesi/anlaşması**. Paket/kredi DB'de kalır ama yeni akışta dormant.
- **Hoca ücret/hak ediş hesaplaması YOK.** Rate alanları DB'de kalabilir; UI + rapordan parasal hesap kalkar.
- **Program sözleşmeden üretilir. Sözleşmesiz Regular ders eklenmez. Öğrenci self-booking YOK** (admin yönetir).
- **Hoca müsaitliği hard gate değil, advisory.**
- **Aynı stüdyoda aynı saatte farklı hocalar ders verebilir.** Çakışma: **hoca** + **öğrenci** bazlı.
- Ders **grup** veya **bireysel** olabilir. Bireysele 2. öğrenci eklenemez. Grup limiti yok.
- **Grup ayrılmaz bütün değil:** öğrenciler tek tek başka gruba/düzene taşınabilir.
- **Ders süresi ders tipinden** (ClassType.DefaultDurationMinutes).
- **Telafi hakkı kazanılmadan telafi dersi eklenemez.** Kazanım: öğrenci devamsızlığı **veya** stüdyo/hoca
  kaynaklı işlenmeyen ders. Telafi **sözleşme ders hakkından düşmez**, telafi hakkından düşer.
- **Planlanan hoca ≠ fiilî hoca** ayrımı. Hoca değişikliği dersi iptal etmez, telafi doğurmaz; raporda fiilî
  hocaya yazılır; kullanıcıya "hoca değişikliği" gösterilir.
- Sözleşme **uzatılabilir** (ek ders/ek ücret → ödendi/borç). Ücret elle. **Hoca ücreti kapsam dışı.**

## Hedef domain modeli (öneri)

- **`StudentContract`** (yeni): `StudioId, StudentProfileId, ClassTypeId, InstructorProfileId (planlanan),
  StartDate/EndDate (DateOnly), TotalLessonCount, MakeupAllowance, Price, Currency, IsIndividualDefault,
  Status (Active/Completed/Cancelled/Expired), Notes`. Türetilen: scheduled/available/delivered ders sayıları.
- **`ContractAmendment`** (yeni, Faz 5): uzatma — `PreviousEndDate/NewEndDate, AdditionalLessonCount,
  AdditionalMakeupAllowance, AdditionalPrice, PaymentId?`.
- **`ClassSession`** (genişletme): `IsIndividual`, `ActualInstructorProfileId` (fiilî hoca);
  `InstructorProfileId` = planlanan; Capacity grupta sınırsız.
- **`ClassBooking`** (genişletme): `StudentContractId`; Sprint 14 paket alanları dormant.
- **`MakeupCreditTransaction`** (REUSE): yeni kazanım (devamsızlık + stüdyo-kaynaklı) ve gating.
- **`Payment`** (genişletme): `StudentContractId` (borç/ödeme sözleşmeye bağlanır).
- **Deprecate (dormant, drop YOK):** PackagePlan/StudentPackage/kredi tarafı, PackageEligibilityChecker,
  StudentPackageConsumer, InstructorProfile.Rate/RateType, InstructorClassType.RateOverride.

## Faz planı

- **Faz 1 — Sözleşme + sözleşmeden program (çekirdek).** (Sprint 18; ayrıntı: SPRINT-18-CONTRACTS-SPEC.md)
- **Faz 2 — Yoklama onayı + hoca değişikliği + hoca ücreti kaldırma + parasız rapor.**
- **Faz 3 — Telafi tam akışı (devamsızlık/stüdyo-kaynaklı kazanım, gating, tüketim) + kapanış→telafi.**
- **Faz 4 — Haftalık sürükle-bırak takvim + erteleme UX (tek-taşı / zincir-kaydır / köklü düzen değişikliği).**
- **Faz 5 — Sözleşme uzatma/ek protokol + borç/ödeme + bitiş-tarihi hatırlatma.**

## Çapraz riskler

- Booking gate'inin **paket → sözleşme** geçişi, mevcut tüm booking/scheduling testlerini (smoke + e2e-seed +
  E2E) etkiler → kademeli ve bilinçli yapılmalı; yarı-durum olmamalı.
- Telafi muhasebesi (gating + kazanım + "sözleşme hakkından düşmez") net spec + yarış (row-lock) ister.
- Drag-drop + zincirleme kaydırma yeni frontend karmaşıklığı; ayrı faz.
