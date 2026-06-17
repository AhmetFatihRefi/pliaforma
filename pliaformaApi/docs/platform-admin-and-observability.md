# Platform Admin, Abonelik İskeleti ve Gözlemlenebilirlik (Sprint 10)

Platform sahibinin (SaaS işleten) tüm stüdyoları **cross-tenant** görebildiği, askıya alıp aktifleştirebildiği,
basit abonelik bilgisini takip ettiği ve sistem/stüdyo sağlık özetini gördüğü sprint. Tüm cross-tenant erişim
**yalnızca `/api/platform/*` altında** ve **yalnızca PlatformAdmin** içindir.

## PlatformAdmin normal studio admin'den nasıl ayrılır?
İki ayrı yetki düzlemi vardır:
- **`GlobalRole`** (User üzerinde): `Standard | PlatformAdmin`. Platformun tamamına ilişkin yetki.
- **`StudioRole`** (StudioMembership üzerinde): `Owner | StudioAdmin | Instructor | Student`. Bir **stüdyo
  içindeki** yetki.

**Owner/StudioAdmin** yalnızca **kendi stüdyosunu** yönetir (tenant-scoped). **PlatformAdmin** ise stüdyo
üyeliğinden bağımsız, platform genelinde iş yapar. PlatformAdmin'in aktif bir stüdyo seçmesi **gerekmez**;
platform uçları `studio_id` claim'i aramaz.

## Platform endpointleri neden ayrı prefix altında?
Cross-tenant veri sızıntısı riskini sınırlamak için **tek bir kapı**: `/api/platform/*`. Bu uçlar
`[Authorize(Policy = "PlatformAdmin")]` ile korunur; policy `global_role == PlatformAdmin` claim'ini şart
koşar (JWT `RoleClaimType` studio_role'e bağlı olduğundan platform yetkisi **claim policy** ile kontrol edilir,
rol tabanlı `[Authorize(Roles=...)]` ile değil). Ayrıca her handler ikinci savunma katmanı olarak
`PlatformRules.EnsurePlatformAdmin` ile GlobalRole'ü tekrar doğrular.

## PlatformAdmin neden tenant endpointlerini otomatik bypass etmez?
Bilinçli karar: **PlatformAdmin'e normal tenant uçlarında otomatik "her şeyi gör" yetkisi verilmez.** Normal
tenant uçları ya `studio_role` (Owner/StudioAdmin/…) ister ya da handler `RequireStudioId()` çağırır.
PlatformAdmin'in (aktif stüdyo seçmemişse) `studio_id`/`studio_role` claim'i yoktur → tenant uçları **403**
döner. Böylece cross-tenant erişim **yalnızca** `/api/platform/*` üzerinden ve açıkça olur; mevcut tenant
mantığına gizli bir arka kapı eklenmez. (Smoke: PlatformAdmin token'ıyla `GET /api/studio` ve
`POST /api/class-types` → 403.)

## Studio status modeli nedir?
`Studio.IsActive` (bool) yerine artık **`StudioStatus`** enum'u tutulur: `Active | Suspended | Cancelled`.
Ek alanlar: `SuspendedAt?`, `SuspendedReason?`, `ReactivatedAt?`, `UpdatedByPlatformAdminUserId?`.
- **Active:** normal çalışır (okuma + yazma).
- **Suspended:** okuma çalışır, **yazma engellenir** (`StudioStatusBehavior`), veri **silinmez**.
- **Cancelled:** bu sprintte yalnızca enum olarak ayrıldı; davranışı Suspended gibidir (yazamaz). Aktif olarak
  kullanılan bir akış yoktur.

`IsActive`, geriye dönük okuma kolaylığı için `Status == Active` döndüren **hesaplanan** (mapped olmayan) bir
property olarak kaldı; veritabanı kolonu kaldırıldı (migration mevcut satırları `IsActive`→`Status` taşır).

## Suspended stüdyoda read/write davranışı nedir?
- **Login engellenmez:** stüdyo kullanıcıları giriş yapabilir (token alır). Bu bilinçli — ileride frontend
  "stüdyonuz askıda" mesajı gösterebilir.
- **Yazma (IStudioWriteRequest):** `StudioStatusBehavior` `Status != Active` ise **403** ile reddeder.
- **Okuma:** etkilenmez; kullanıcı kendi verisini görebilir. (Smoke: askıda `POST /api/class-types` → 403,
  `GET /api/studio` → 200.)
- **Hatırlatma:** worker yalnızca `Status == Active` stüdyoları tarar → **askıdaki stüdyo için reminder
  üretilmez**. Manuel trigger de yazma isteği olduğundan askıda **403** olur. (Smoke ile doğrulandı.)

## Suspend / Reactivate akışı
- **Suspend** (`POST /api/platform/studios/{id}/suspend`, body `{ reason }`): `Status=Suspended`,
  `SuspendedAt=now`, `SuspendedReason=reason`, `UpdatedByPlatformAdminUserId=admin`. Mevcut booking/session/
  payment **kalır** (silinmez).
- **Reactivate** (`POST .../reactivate`): `Status=Active`, `ReactivatedAt=now`, `SuspendedAt=null`. **Gerekçe
  (`SuspendedReason`) geçmiş olarak KORUNUR** (denetim için); yazma tekrar açılır.

Bu komutlar `ICommand`'dır (TransactionBehavior sarar) ama **`IStudioWriteRequest` DEĞİLDİR** — yani stüdyo
bağlamı gerektirmez ve kendi suspend kuralına takılmaz.

## PlatformSubscription gerçek ödeme değildir, neyi temsil eder?
**Basit takip iskeleti.** Gerçek tahsilat, online ödeme, fatura/e-arşiv **YOKTUR**. Yalnızca platform admin'in
plan/kademe/fiyat/dönem bilgisini **kaydetmesi** içindir. Alanlar: `PlanName, Tier?, Status, MonthlyPrice,
Currency, BillingPeriod, CurrentPeriodStart, CurrentPeriodEnd, TrialEndsAt?, CancelledAt?, Notes?`.
- **Status** (`SubscriptionStatus`): `Trialing | Active | PastDue | Suspended | Cancelled`. **StudioStatus ile
  birebir aynı olmak zorunda değildir** — ayrı yönetilir. Bir stüdyoyu suspend etmek aboneliği otomatik
  değiştirmez (istenirse admin abonelik Status'unu da elle Suspended yapabilir).
- **Bir stüdyo = bir abonelik:** `(StudioId) WHERE IsDeleted=false` filtered unique index. `PUT .../subscription`
  **upsert**'tir (yoksa oluşturur, varsa günceller). `POST .../subscription/cancel` Status=Cancelled +
  CancelledAt yazar (kayıt silinmez).

## Platform Overview neyi gösterir?
`GET /api/platform/overview` — basit count/summary (ağır sorgu yok): toplam/aktif/askıda/iptal stüdyo, toplam
kullanıcı/öğrenci/hoca, bu ay rezervasyon (CreatedAt UTC) ve ödeme kaydı (DueDate), tüm stüdyolarda effective
**gecikmiş ödeme tutarı**, bekleyen/başarısız bildirim, son bildirim aktivitesi, DB sağlık bayrağı. "Bu ay"
sınırları **UTC ay** ile hesaplanır (platform geneli kaba özet; stüdyo-yerel TZ'lerin karışımı pratik değil).

## Health summary ne gösterir?
`GET /api/platform/health-summary` — **READ-ONLY**, sistemi değiştirmez. DB sağlık bayrağı, stüdyo sayıları,
bildirim pending/failed/son-24s-gönderilen, son bildirim zamanı, **hatırlatma worker** açık mı + interval,
sunucu UTC saati. Gerçek metrik altyapısı (Prometheus vb.) **YOKTUR** — basit sağlık görünümüdür. Worker
yapılandırması `IReminderWorkerInfo` soyutlamasıyla okunur (Application katmanı IConfiguration'a bağlı değildir).

`GET /api/platform/recent-activity` — birkaç kaynaktan (yeni stüdyo, ödeme, bildirim) az kayıt çekilip zamana
göre birleştirilmiş cross-tenant son aktivite listesi.

## Hangi indexler eklendi ve neden?
Sprint 10'da küçük bir **index/performans incelemesi** yapıldı. Kontrol edilenler ve sonuç:

| Tablo | İndeks | Durum |
|---|---|---|
| Notifications | `(StudioId, RecipientUserId)` | ✅ vardı (Sprint 9) |
| ClassBookings | `(StudioId, ClassSessionId)` | ✅ vardı (Sprint 4) |
| Payments | `(StudioId, Status)` | ✅ vardı (Sprint 7) |
| **Payments** | **`(StudioId, DueDate)`** | ➕ **EKLENDİ** |
| ClassSessions | `(StudioId, StartAt)` | ✅ vardı (Sprint 3) |
| StudentPackages | `(StudioId, StudentProfileId)` | ✅ vardı (Sprint 7) |
| Attendances | `(StudioId, ClassSessionId)` | ✅ vardı (Sprint 6) |

Yalnızca **`Payments (StudioId, DueDate)`** eksikti ve eklendi: hatırlatma taraması (due-soon/overdue, `DueDate`
filtresi) ve dashboard/rapor (DueDate ay aralığı) bu kolona göre süzdüğünden faydalı. Gereksiz index basılmadı.
Ayrıca yeni tablo için `PlatformSubscriptions (StudioId) UNIQUE WHERE IsDeleted=false`.

## Tenant izolasyonu nasıl korunur?
Platform uçları **bilerek cross-tenant**'tır (PlatformAdmin tüm stüdyoları görür) ama:
- Erişim yalnızca PlatformAdmin (policy + handler guard). Diğer roller **403**.
- Stüdyo bazlı sorgular `Id == request.StudioId` ile yüklenir; olmayan stüdyo **404**.
- Detay/sayım sorguları ilgili `StudioId` ile filtrelenir → bir stüdyonun verisi başka stüdyonunkine karışmaz
  (smoke: A detayında A sahibinin e-postası, B detayında B sahibinin e-postası). Liste sayımları **GroupBy** ile
  toplu çekilir (N+1 yok).

## Gelecekte gerçek abonelik/online ödeme nasıl bağlanabilir?
- **Abonelik tahsilatı:** `PlatformSubscription` zaten durum makinesine sahip. Bir ödeme sağlayıcısı (Stripe/
  iyzico) entegrasyonu, başarılı tahsilatta `Status=Active` + `CurrentPeriodEnd` ilerletir; bir `ProviderRef` +
  webhook + retry eklenir. Şema değişmeden takip → tahsilata evrilir.
- **Otomatik askı:** bir job, `CurrentPeriodEnd` geçmiş + ödenmemiş abonelikleri `PastDue`/`Suspended` yapıp
  stüdyoyu suspend edebilir (şu an manuel).
- **Gerçek metrikler:** health summary, OpenTelemetry/Prometheus exporter ile zenginleştirilebilir (şu an basit
  count). **Multi-instance** için worker'a distributed lock eklenmeli (bkz. [notifications-and-reminders.md](notifications-and-reminders.md)).

## Endpoint özeti
| Endpoint | İşlev |
|---|---|
| `GET /api/platform/overview` | platform geneli özet |
| `GET /api/platform/studios?search&status` | stüdyo listesi (arama + durum filtresi) |
| `GET /api/platform/studios/{id}` | stüdyo detayı (sahip + abonelik + sayım + son aktivite) |
| `POST /api/platform/studios/{id}/suspend` | askıya al (`{ reason }`) |
| `POST /api/platform/studios/{id}/reactivate` | yeniden aktifleştir |
| `GET /api/platform/studios/{id}/subscription` | aboneliği getir (yoksa null) |
| `PUT /api/platform/studios/{id}/subscription` | abonelik upsert |
| `POST /api/platform/studios/{id}/subscription/cancel` | aboneliği iptal et |
| `GET /api/platform/health-summary` | sistem/gözlemlenebilirlik özeti (read-only) |
| `GET /api/platform/recent-activity` | platform geneli son aktivite |

Tümü **PlatformAdmin**; diğer roller 403. Frontend: ayrı `/platform` route grubu (kendi layout + sidebar,
stüdyo seçimi gerektirmez), `PlatformAdmin` değilse `/dashboard`'a yönlenir.

Manuel doğrulama: `scripts/sprint10-smoke.sh` (PlatformAdmin yetki + 4 rol 403 + tenant bypass yok + overview/
list/detail + abonelik upsert/get/cancel/validation + suspend/reactivate + suspended read/write/reminder +
health + tenant ayrımı + 404).
