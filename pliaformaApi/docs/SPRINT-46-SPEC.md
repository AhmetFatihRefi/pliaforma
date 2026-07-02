# Sprint 46 — Pilot Simple People Mode

Durum: onaylı spec (implementasyon başlıyor). Commit/push/tag YOK — önce implementasyon + validation raporu.

## 1. Amaç

Uygulama, canlıya alınmadan önce **tek bir salonda ~2 ay pilot** olarak denenecek. Pilot süresince:

- Öğrenciler sisteme **kullanıcı hesabı** olarak dâhil edilmez; sadece **Ad soyad** ile eklenir.
- **Hoca** kendi öğrencisini hızlıca ad-soyad ile ekleyebilir.
- Öğrenciye davet maili, portal erişimi, şifre, hesap, telefon/e-posta zorunluluğu **yok**.
- 2 ay sonra **veri kaybı olmadan** tam kullanıcı modeline (portal/davet/sözleşme) geri dönülebilir.
- Mevcut tam model **silinmez/değiştirilmez**; pilot sade mod **geçici** bir katman olarak eklenir.

Kritik keşif: öğrenci tarafı büyük ölçüde **mevcut "misafir öğrenci" (Guest)** akışıyla zaten karşılanıyor. Bu sprint onu **açar, işaretler ve booking'i bozmadan derse eklenebilir** hâle getirir.

## 2. Mevcut mimari (özet, dosya referanslı)

- `StudentProfile.UserId` **nullable**; `Status=Guest`, yalnız `FullName` zorunlu (`CreateGuestStudent.cs`). Guest oluşturma User/membership/portal/davet yaratmaz.
- Sonradan yükseltme **zaten var**: `POST /api/studio/students/{id}/convert-to-user` → `StudentProfile.Id` korunur, sadece `UserId`/`StudioMembershipId`/`Status` bağlanır, User e-posta tekilliği (unique `NormalizedEmail`) kontrol edilir (`ConvertStudentToUser.cs`).
- Tüm booking/attendance/contract/payment/package **`StudentProfileId`** üzerinden çalışır (UserId değil) → guest tam fonksiyonel.
- Tek sıkı bağ: `BookingCreator` yalnız `SessionKind==Regular` için uygun aktif **contract line** ister; yoksa 409 (`BookingCreator.cs:92-120`). Attendance/report/move null-line'a toleranslı.
- Guest oluşturma endpoint'i şu an **Owner/StudioAdmin**'e kapalıdır Instructor'a (v1.1.2 privacy hardening, `StudentsController.cs:13`).
- `InstructorProfile.UserId` **non-nullable** → accountless hoca bu sprint kapsamı DIŞI.

## 3. Onaylanan kararlar

1. **Booking:** Alternatif B — gizli **pilot contract/line**. Null-line Regular yoluna gidilmez; `BookingCreator` line zinciri gevşetilmez. Pilot line **lazy** (derse ekleme anında) oluşturulur/bulunur; sonra mevcut engine normal çalışır.
2. **İşaret:** Notes/string konvansiyonu değil, **açık `IsPilot` bool** — non-destructive migration. `StudentContract.IsPilot` + `StudentContractLine.IsPilot`, default `false`. (Parent contract da rapor/UI query'lerinde görünmemesi için her ikisinde.)
3. **Instructor:** Pilotta hoca **gerçek ama minimal login hesabı** ile girer. Accountless instructor / `InstructorProfile.UserId` nullable / attach-user endpoint YOK.
4. **Görünürlük:** Hoca **tüm stüdyo öğrencilerini ad-soyad düzeyinde** görür (minimal DTO: Id + FullName + hesap-var-mı). Sağlık/finans/sözleşme/PII hocaya kapalı. Owner/Admin tam erişim devam.
5. **Feature flag:** Global env/config `Pilot:SimplePeopleMode` (env `Pilot__SimplePeopleMode=true`), default **false**. Per-studio setting yok (migration+admin UI gerektirir; ayrı sprint).
6. **Endpoint:** Mevcut `POST /api/studio/students/guest` kullanılır. Owner/StudioAdmin her zaman; **Instructor yalnız flag açıkken** (kapalıyken 403).

## 4. Backend tasarımı

### 4.1 Şema / migration (non-destructive)
- `StudentContract.IsPilot : bool` (default false) + config `HasDefaultValue(false)`.
- `StudentContractLine.IsPilot : bool` (default false) + config `HasDefaultValue(false)`.
- Migration yalnız 2 kolon ekler (ADD COLUMN ... DEFAULT false NOT NULL). DROP/veri taşıma yok. Mevcut satırlar `false` alır.

### 4.2 Feature flag
- `IPilotModeInfo { bool SimplePeopleModeEnabled }` (Application.Common.Interfaces), Infrastructure'da `IConfiguration`'dan okunur (`ReminderWorkerInfo` deseni), singleton. Anahtar `Pilot:SimplePeopleMode`.

### 4.3 Pilot contract/line lazy provisioning
- Yeni servis `PilotContractProvisioner` (Features/Contracts): `ResolveOrCreatePilotLineAsync(studioId, student, session, userId, ct)`.
  - Var olan **aktif pilot line**'ı (IsPilot, student, session.ClassTypeId) bulur → varsa onu döner.
  - Yoksa: student'ın **pilot contract**'ını bulur/oluşturur (IsPilot=true), altına pilot line ekler.
  - Pilot line: `TotalPrice=0`, `PricePerLesson=0`, `DueDate=null`, `LessonCount=100000` (pratikte sınırsız; UI'da görünmez), `PreferredMaxCapacity=null` (kapasite tercihi yok), `InstructorProfileId=null` (herhangi hoca), `StartDate=2000-01-01`, `EndDate=2999-12-31` (backfill dâhil her tarih kapsanır), `Status=Active`, `IsPilot=true`, `Notes="[PILOT] Otomatik oluşturulan pilot kaydı"`.
- `BookingCreator` Regular dalı: resolver null dönerse **ve** `IPilotModeInfo.SimplePeopleModeEnabled` ise provisioner'dan line alınır; değilse mevcut 409 aynen atılır. (Null-line yoluna gidilmez; gerçek line ile devam.)

### 4.4 Pilot kayıtların gizlenmesi (tek chokepoint)
- `ContractLineQuery.BuildAsync` başına `q = q.Where(l => !l.IsPilot)` → tüm finans/sözleşme/rapor/hatırlatma DTO yüzeyleri (staff/student/studio line listeleri, `ComputeDebtAsync`, `StudioDashboard`, `StudentDashboard`, `PaymentsReport`, `ReminderScanner`) pilot line'ı görmez.
- `ContractLineResolver.ResolveAsync` sorgusuna `&& !l.IsPilot` → pilot line **auto-resolve'da asla seçilmez** (flag kapansa da yeni booking'lerde otomatik kullanılmaz). Pilot line yalnız açık pilot-provisioning yolundan kullanılır.
- Explicit line seçimi (`ContractLineEligibilityChecker`) ve `MoveBookings` mevcut booking'in line'ını taşıdığından pilot filtresi UYGULANMAZ → eski pilot booking'ler veri kaybı olmadan taşınabilir/çalışır.

### 4.5 Endpoint / permission
- `POST /api/studio/students/guest` → yeni `StudioGuestStudentsController` (route değişmez), `[Authorize(Roles="Owner,StudioAdmin,Instructor")]`. `CreateGuestStudentHandler`: role==Instructor ve flag kapalı → `ForbiddenException` (403). Owner/Admin her zaman. `StudentsController`'dan guest action kaldırılır (route çakışması olmaması için).
- `GET /api/studio/students/minimal?search=` → aynı controller, aynı roller. `GetStudentsMinimalQuery`: Instructor flag açık değilse 403; Inactive hariç tüm stüdyo öğrencilerini **minimal DTO** (Id, FullName, HasAccount) döner. Owner/Admin da kullanabilir. PII/sağlık/finans YOK.
- Instructor booking: `StaffCreateBooking` — flag açıkken instructor için `CreatedByUserId==userId` kısıtı gevşetilir (kendi seansı kısıtı korunur), böylece ortak/grup öğrencileri kendi dersine ekleyebilir. Flag kapalıyken mevcut kısıt aynen.

### 4.6 /auth/me
- `MeResult`'a `bool PilotSimplePeopleMode` (default false). `GetMeHandler` `IPilotModeInfo`'dan doldurur. Frontend bu alandan pilot UI'yı açar.

### 4.7 Audit / güvenlik
- Guest oluşturma zaten Instructor için loglanabilir; mevcut `SecurityAuditEventType` yeterliyse ek event üretilmez (enum genişletmesi migration gerektirmez ama kapsam-dışı tutulur). Tenant izolasyonu: tüm sorgular `StudioId` filtreli; instructor başka stüdyoya yazamaz/göremez (JWT `RequireStudioId`).
- PII minimum: pilot kayıt yalnız FullName tutar.

## 5. Frontend tasarımı

- `auth` context: `me.pilotSimplePeopleMode` okunur; `usePilotMode()` benzeri erişim.
- Öğrenci ekleme (`GuestFormModal`): flag açıkken **yalnız Ad soyad**; telefon/e-posta/kaynak/seviye/sağlık/acil-kişi alanları gizli.
- Öğrenci listesi: hesabı olmayan (pilot) kayıt için sakin ibare **"Pilot kayıt · hesap yok"**. Convert/davet CTA'ları flag açıkken gizli.
- Hoca: flag açıkken "Hızlı öğrenci ekle" görünür; öğrenci seçiminde minimal liste kullanılır.
- Pilot line/sözleşme/`100000 hak` kullanıcıya hiçbir ekranda görünmez (backend zaten gizler).
- Flag kapalıyken mevcut tam UI aynen.

## 6. Pilot → full upgrade (veri kaybı yok)

- `StudentProfile` ana kayıt; `Id` değişmez. `convert-to-user` ile `UserId` bağlanır, e-posta alınır, duplicate email reddedilir, `StudioMembership(Student)` açılır/yeniden kullanılır.
- Geçmiş booking/attendance/payment/contract aynı `StudentProfileId` üzerinden görünmeye devam eder. Pilot line'lar (IsPilot) full modda da booking geçmişini korur; finans UI'da görünmez; yeni booking'lerde auto-resolve etmez.

## 7. Riskler

- Pilot line'ın finans/hak ekranlarını şişirmesi → `IsPilot` + `BuildAsync`/resolver filtresiyle giderildi.
- Instructor auth gevşetme PII sızıntısı → yalnız minimal DTO + create; liste/detay/health kapalı kaldı.
- Flag global → yanlış prod açılışı: default false; prod'da açıkça set edilmedikçe kapalı.
- Move: pilot booking gerçek line taşıdığından Sprint 43 bozulmaz.

## 8. Test planı

Backend: instructor guest create flag kapalı→403 / açık→ok; owner/admin→ok; UserId null; membership yok; davet/mail/token yok; email/phone'suz oluşur; instructor başka stüdyoya ekleyemez; instructor minimal listeye erişir (tüm stüdyo, PII yok); owner/admin tümünü görür; pilot öğrenci Regular derse eklenir; pilot line/contract IsPilot=true; pilot line finans/contract query'lerinde gizli; kapasite/çakışma/yoklama çalışır; Sprint 43 move pilot öğrenciyle çalışır; convert-to-user sonrası StudentProfile.Id sabit; duplicate email reddedilir; flag kapalıyken full mode bozulmaz.

Frontend: `tsc --noEmit` 0, `next build` 0; flag açıkken sadece Ad soyad; instructor hızlı ekleme; email/phone/portal/davet gizli; pilot kayıt sakin ibare; pilot line/9999 hak görünmez; flag kapalıyken UI bozulmaz.

Validation: `dotnet build` 0; migration `has-pending` clean; `scripts/sprint46-smoke.sh`.

## 9. Kapsam dışı

Accountless instructor; `InstructorProfile.UserId` nullable; instructor attach-user endpoint; User/Identity modeli değişikliği; portal silme; tam öğrenci modelini kaldırma; ödeme/sözleşme motoru yeniden yazımı; SMS/WhatsApp davet; public signup; onboarding redesign; eski veri silme; pilot sonu otomatik migration; per-studio pilot setting.
