# Hocalar, Üyelik Akışı & Pilates Grupları (Sprint 1)

## Roller, yetki sınırları & Owner koruması (Sprint 1 Fix)

| Aksiyon | Owner | StudioAdmin | Instructor | Student |
|---|:--:|:--:|:--:|:--:|
| Üye listele / davet (Instructor) | ✓ | ✓ | – | – |
| **StudioAdmin davet/rol atama** | ✓ | – | – | – |
| Rol değiştir (`PATCH .../role`) | ✓ | – | – | – |
| Üye çıkar (Instructor) | ✓ | ✓ | – | – |
| **StudioAdmin'i çıkar** | ✓ | – | – | – |
| ClassType / atama / ücret yönetimi | ✓ | ✓ | – | – |

**Owner koruma kuralları (backend zorlar, UI yalnızca kolaylık):**
- Owner üyeliği **çıkarılamaz** (`RemoveMember` → 409).
- Owner'ın rolü **değiştirilemez**; Owner kendi rolünü düşüremez (`ChangeMemberRole` target=Owner → 409).
- `Owner` rolüne **bu uçtan yükseltme yapılamaz** (rol yalnızca StudioAdmin|Instructor).
- **Yalnızca Owner**, StudioAdmin rolü verir/alır ve StudioAdmin'i çıkarır (StudioAdmin başka
  StudioAdmin'e dokunamaz → 403). Davet `StudioAdmin` rolüyle ise handler Owner şartı koşar.
- Instructor ve Student yönetim uçlarına **erişemez** (403). Rol kapısı `[Authorize(Roles=...)]` +
  handler kontrolleri. Smoke ile kanıtlı: `scripts/role-permission-smoke.sh` (17/17).

> StudioAdmin başka bir StudioAdmin'i değiştiremez/çıkaramaz — **güvenli varsayım**: StudioAdmin
> seviyesini yalnızca Owner yönetir.

## Üyelik yaşam döngüsü

`StudioMembership` = (User, Studio, Role, Status, Direction). İki yön:
- **Davet (stüdyo→kullanıcı):** Owner/StudioAdmin kayıtlı bir kullanıcıyı hoca olarak davet eder →
  Pending, `InvitedByStudio`. Davet edilen kullanıcı kabul eder.
- **Başvuru (kullanıcı→stüdyo):** Kullanıcı slug ile başvurur → Pending, `RequestedByUser`. Stüdyo
  yöneticisi onaylar.

Kabul/ret yetkisi **yön bazlıdır** (`MembershipAccess`): InvitedByStudio → yalnızca davet edilen;
RequestedByUser → yalnızca o stüdyonun Owner/StudioAdmin'i. Çıkarma (Removed) yalnızca yönetici;
Owner çıkarılamaz.

> **Sprint 1 sınırı:** Davet yalnızca **kayıtlı kullanıcı** için (e-posta tabanlı dış davet/e-posta
> gönderimi sonraki sprintte). Hoca başvurusunda InstructorProfile ücret 0 ile açılır; yönetici sonra
> günceller.

## InstructorProfile (hoca ücreti — stüdyo bazında)

Instructor üyeliğiyle birlikte (StudioId+UserId) oluşur. Alanlar: `DisplayName`, `Bio?`, `Rate`,
`RateType` (PerSession/PerHour/PerStudentPerSession), `Color?`, `IsAcceptingStudents`. Ücret stüdyo
bazındadır; aynı hoca farklı stüdyoda farklı ücrete sahip olabilir.

## ClassType (pilates grubu)

Dört eksen + süre: `Equipment` (Mat/Reformer/Cadillac/Chair/Barrel) × `CapacityType`
(Private/Duo/Group) × `Level` (Beginner/Intermediate/Advanced/AllLevels) × `Purpose`
(General/ClinicalRehab/Prenatal) + `DefaultDurationMinutes`. **Kapasite kuralı:** Private⇒1, Duo⇒2,
Group⇒2..50 (`CapacityRules`).

## InstructorClassType (hoca ↔ grup) + RateOverride

Çoka-çok atama. `RateOverride` **opsiyoneldir**: doluysa o grup için hoca ücretini override eder,
**boş/null ise** hoca profilindeki ana `Rate` geçerli olur. Negatif olamaz (validator). (InstructorProfileId,
ClassTypeId) benzersiz. Atamada hem profil hem grup aktif stüdyoya ait olmalıdır.

Uçlar: `POST` (ata, override ops.), `PUT .../class-types/{assignmentId}` (override güncelle/temizle),
`DELETE` (kaldır). Frontend atama modalında override alanı vardır; mevcut atamada override gösterilir
ve düzenlenebilir.

## Yetki & tenant izolasyonu (bu sprintte gerçekleşti)

- Stüdyo-içi YAZMA komutları `IStudioWriteRequest` taşır → `StudioStatusBehavior` (stüdyo aktif) +
  `MembershipGuardBehavior` (üyelik DB'de Active) çalışır.
- Controller'larda rol kapısı: `[Authorize(Roles="Owner,StudioAdmin")]` (yazma) — `RoleClaimType`
  `studio_role` claim'ine bağlıdır.
- Handler'lar hedef kaydı `StudioId == currentUser.StudioId` ile yükler → başka stüdyonun kaydı
  **NotFound (404)**. Smoke ile kanıtlandı: B stüdyosu A'nın grubuna GET/PUT/DELETE → 404; Instructor
  rolüyle grup oluşturma → 403.

## Endpoint özeti

| Endpoint | Yetki | İşlev |
|---|---|---|
| `GET /api/studio` · `PUT /api/studio` | üye · Owner/Admin | stüdyo ayarları |
| `GET/POST/PUT/DELETE /api/class-types` | okuma: üye · yazma: Owner/Admin | grup CRUD |
| `GET /api/studio/memberships` | Owner/Admin | üye listesi (+ücret) |
| `POST /api/studio/memberships/invite` | Owner/Admin (StudioAdmin daveti: Owner) | StudioAdmin/Instructor daveti |
| `PATCH /api/studio/memberships/{id}/role` | **Owner** | rol değiştir (StudioAdmin\|Instructor) |
| `POST /api/studio/memberships/{id}/remove` | Owner/Admin (Owner & StudioAdmin korumalı) | üye çıkar |
| `POST /api/members/apply` | kullanıcı | stüdyoya başvuru |
| `GET /api/members/my-invitations` | kullanıcı | bekleyen davetlerim |
| `POST /api/members/{id}/accept` · `/reject` | yön bazlı | üyelik kararı |
| `PUT /api/instructors/{id}/rate` | Owner/Admin | ücret güncelle |
| `GET/POST /api/instructors/{id}/class-types` · `PUT/DELETE /api/instructors/class-types/{id}` | okuma: üye · yazma: Owner/Admin | hoca↔grup atama (+RateOverride) |

Manuel doğrulama: `scripts/sprint1-smoke.sh` (akış+tenant) · `scripts/role-permission-smoke.sh` (rol/yetki).
