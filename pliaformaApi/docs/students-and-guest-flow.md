# Öğrenciler, Misafir Akışı ve User'a Dönüştürme (Sprint 2)

## Neden StudentProfile User'dan ayrı?
Öğrenci, stüdyonun **operasyonel kaydıdır**; gerçek bir hesabı (User) olmak zorunda değildir. İleride
rezervasyon/ödeme/yoklama kayıtları **`StudentProfileId`'ye** referans verecektir. Bu yüzden öğrenci
kimliği, kimlik-doğrulama hesabından (User) ayrı tutulur: bir öğrenci hesapsız (misafir) yaşayabilir,
sonra bir User'a bağlanabilir — ve bağlanırken **profil Id'si değişmez**, geçmiş veri kopmaz.

## Neden misafir kaydı User'sız oluşturulur?
Telefonla arayan veya kapıdan gelen kişi için hoca/yönetici anında kayıt açabilmeli; o anda kişinin
e-postası/parolası olmayabilir. `StudentProfile.UserId = null` → **misafir**. `Status = Guest`.
`CreatedByUserId` kaydı açan kişidir (hocanın erişim sınırı buna dayanır).

## Neden dönüşümde StudentProfileId korunur?
Dönüşüm bir **güncellemedir, yeni kayıt değil**: yalnızca `UserId` + `StudioMembershipId` set edilir,
`Status = Active`, `ConvertedAt` damgalanır. Profil **silinmez**, Id sabit kalır. Böylece gelecekteki
rezervasyon/ödeme/yoklama (hep `StudentProfileId`'ye bağlı) dönüşümden etkilenmez. (Smoke ile kanıtlı:
convert öncesi/sonrası Id aynı.)

## Instructor'ın öğrenci erişim sınırı
- Owner/StudioAdmin: **tüm** öğrencileri görür/yönetir, misafir oluşturur, günceller, dönüştürür,
  pasifleştirir.
- Instructor: **yalnızca KENDİ oluşturduğu** misafirleri (`CreatedByUserId == currentUser`) görür/
  günceller/dönüştürür. Başka hocanın öğrencisine erişemez (403). Tüm liste görünmez (yalnızca kendi).
- Student: yönetim uçlarına erişemez (403); yalnızca `GET /api/student/me` ile kendi profilini görür.
- PlatformAdmin: bu sprintte tenant bypass YOK (ayrıca ele alınacak).

## HealthNotes (hassas alan) kimler görür?
`HealthNotes` yalnızca **yetkili görüntüleyiciye** doldurulur (`StudentDto.From(..., includeHealth)`),
aksi halde `null` döner: Owner/StudioAdmin, **kaydı oluşturan** hoca, ve **öğrencinin kendisi**. Diğer
durumlarda alan boş gelir (backend'de maskelenir, frontend yalnızca geleni gösterir).

## Misafir → User dönüşümü senaryoları
**1) E-posta mevcut bir User'a aitse:** o User kullanılır (duplicate yaratılmaz). Stüdyoda üyeliği
yoksa Student üyeliği (Active) oluşturulur; varsa o üyelik kullanılır (StudioMembership `(StudioId,
UserId)` benzersizdir). `StudentProfile.UserId/StudioMembershipId` set edilir, `Status=Active`.
- O kullanıcıya bağlı **başka aktif** öğrenci profili varsa → **409 Conflict**.

**2) E-posta sistemde yoksa:** yeni User (GlobalRole=Standard) oluşturulur — **InitialPassword zorunlu**
(min 8). Ad/telefon profilden User'a kopyalanır. Student üyeliği (Active) oluşturulur. Profil bağlanır.

**Genel kurallar:** Email dönüşümde zorunlu. Zaten dönüştürülmüş (UserId dolu) → 409. Pasif (Inactive)
profil → dönüştürülemez (409). Instructor yalnızca kendi misafirini dönüştürür. Profil Id sabit; Notes/
HealthNotes korunur.

## Soft-delete / pasifleştirme
`StudentProfile` hard-delete EDİLMEZ. `deactivate` → `Status=Inactive`, `IsActive=false` (listede
"Pasif" rozetiyle görünür). `reactivate` → `IsActive=true`, `Status = UserId!=null ? Active : Guest`.
`BaseEntity.IsDeleted` (global filter) ayrı bir mekanizmadır; bu sprintte öğrenci silme yoktur.

## Tenant izolasyonu
Tüm uçlar `StudioId == currentUser.StudioId` ile yüklenir; başka stüdyonun öğrencisi **404**. Body'den
gelen StudioId'ye güvenilmez (StudioId yalnızca token claim'inden). Smoke ile kanıtlı: Owner C, A'nın
öğrencisine get/update/deactivate → 404.

## Endpoint özeti
| Endpoint | Yetki | İşlev |
|---|---|---|
| `GET /api/studio/students` | Owner/Admin (tümü) · Instructor (kendi) | liste |
| `GET /api/studio/students/search?query=` | aynı | ad/telefon/e-posta araması |
| `GET /api/studio/students/{id}` | tenant + yönetim yetkisi | tekil |
| `POST /api/studio/students/guest` | Owner/Admin/Instructor | misafir oluştur |
| `PUT /api/studio/students/{id}` | yönetim yetkisi | güncelle |
| `POST /api/studio/students/{id}/convert-to-user` | yönetim yetkisi | User'a bağla |
| `POST /api/studio/students/{id}/deactivate` · `/reactivate` | yönetim yetkisi | pasif/aktif |
| `GET /api/student/me` | kimlik doğrulanmış | kendi profili |

Manuel doğrulama: `scripts/sprint2-smoke.sh` (25 senaryo: akış + tenant + instructor scope + student
engeli + Id korunumu + dönüşüm senaryoları).
