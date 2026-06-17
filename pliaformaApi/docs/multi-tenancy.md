# Multi-Tenancy (Tenant = Studio)

## İzolasyon katmanları

1. **`ITenantScoped`** uygulayan entity'ler `StudioId` taşır.
2. **Token bağlamı:** Aktif stüdyo `studio_id` claim'inden çözülür (`ICurrentUserService.StudioId`).
3. **Handler seviyesi:** Stüdyo-içi sorgu/komutlar `StudioId == currentUser.StudioId` ile filtrelenir;
   FK bütünlüğü tenant-içi doğrulanır. Token claim'ine **kör güvenilmez**.
4. **DB doğrulaması:** Yazma işlemlerinde `MembershipGuardBehavior` üyeliğin DB'de hâlâ `Active`
   olduğunu kontrol eder.

## Neden global query filter ile DEĞİL, handler seviyesinde?

EF Core'da `StudioId` için global query filter, scoped `ICurrentUserService`'i DbContext'e enjekte
etmeyi ve auth sırasında `studio_id` henüz yokken karmaşık kenar durumları gerektirir; ayrıca model
cache davranışını zorlaştırır. Bu yüzden tenant filtresi **handler/sorgu seviyesinde explicit**
yapılır (referans muratliPaket ile aynı tercih). Soft-delete için global filter KULLANILIR
(`IsDeleted == false`).

> Sprint 1+ ile gelen stüdyo-içi entity'ler (`ClassType`, `InstructorProfile`, `InstructorClassType`,
> `StudentProfile`, `InstructorAvailability`, `ClassSession`, `ClassBooking`, `MakeupCreditTransaction`,
> `StudioClosure`, `StudioMakeupPolicy`, `Attendance`, `PackagePlan`, `StudentPackage`,
> `StudentPackageTransaction`, `Payment`, `Notification`) bu kalıbı uygular: handler hedef kaydı
> `Id == ... && StudioId == currentUser.StudioId` ile yükler → başka stüdyonun kaydı **NotFound (404)**.
> Smoke ile kanıtlı (ClassType, StudentProfile, ClassSession/Availability, ClassBooking; Sprint 5: closure
> get → 404, başka stüdyo öğrenci kredisi → 404). Ek rol bazlı daraltma: `StudentProfile`/`ClassSession`/
> `ClassBooking` Instructor yalnızca kendi (oluşturduğu öğrenci / kendi InstructorProfile / kendi seansı);
> telafi kredisi yönetimi Instructor yalnızca kendi oluşturduğu ya da kendi seansında rezervasyonu olan
> öğrenci için; toplu kapanış (closure) stüdyo geneli → Owner/StudioAdmin. Yoklama (`Attendance`): Instructor
> yalnızca kendi seansı (başka stüdyo session attendance → 404, başka hocanınki → 403); öğrenci yalnızca kendi
> geçmişi. Yoklamada `ClassBooking`/`ClassSession`/`StudentProfile` aynı StudioId'ye ait olmalıdır. Paket/ödeme
> (Sprint 7): yönetim **Owner/StudioAdmin** (Instructor erişemez → 403; finans gizliliği); öğrenci yalnızca
> kendi paket/ödemesi; cross-tenant → 404 (smoke ile kanıtlı). **Bildirim (Sprint 9):** kullanıcı yalnızca
> `RecipientUserId == self && StudioId == current` kayıtlarını görür/okur (başka kullanıcı/stüdyo → 404);
> manuel trigger yalnızca aktif stüdyoyu tarar; worker stüdyoları StudioId ile ayrı işler. Dashboard/raporlar (Sprint 8): tüm agregasyon
> `StudioId == token.StudioId` ile sınırlı; by-id dashboard'larda başka stüdyo → 404, Instructor başka hocanın
> iş hacmini göremez, Student yalnızca kendi verisini görür, Instructor staff öğrenci dashboard'unda finans gizli.

## Tenant askıya alma

`Studio.Status != Active` (Suspended/Cancelled) → `StudioStatusBehavior` stüdyo-içi YAZMA isteklerini
(`IStudioWriteRequest`) HTTP 403 ile reddeder. Okuma etkilenmez; veri silinmez. Askıya alma/yeniden
aktifleştirme yalnızca **PlatformAdmin** tarafından `/api/platform/*` üzerinden yapılır (Sprint 10). Worker
yalnızca `Status == Active` stüdyoları taradığından askıdaki stüdyoya hatırlatma üretilmez. Detay:
[platform-admin-and-observability.md](platform-admin-and-observability.md).

## Platform (cross-tenant) erişim — istisna

Tek istisna: **`/api/platform/*`** uçları **bilerek cross-tenant**'tır ve **yalnızca PlatformAdmin** içindir
(`[Authorize(Policy="PlatformAdmin")]` + handler `EnsurePlatformAdmin`). Bu uçlar `studio_id` aramaz. **Normal
tenant uçları PlatformAdmin'i otomatik bypass ETMEZ:** PlatformAdmin'in studio_id/studio_role claim'i yoksa
tenant uçları 403 döner (smoke ile kanıtlı). Yani cross-tenant veri yalnızca platform prefix'i altında açıktır.
