# SPRINT 29B — SPEC: Candidate Selection (Regular Preferred Day/Time + Makeup Separate List)

> v1.8.0-pilot (29A: PreferredSlot + strict payment) üzerine. Bu sprint **yalnız okuma** (candidate query'leri + UI).
> **Şema değişikliği YOK, migration YOK.** Tag önerisi (yeşil olursa): `v1.9.0-pilot`.

## Onaylanan kararlar (kullanıcı)
- **D1 Route:** `GET /api/studio/class-sessions/{sessionId}/regular-candidates?level=` ve `.../makeup-candidates?level=` (class-sessions konvansiyonu).
- **D2 Auth:** yalnız **Owner/StudioAdmin**. Instructor bu aday listelerini **göremez** (booking ekleme/iptal instructor'da yok).
- **D3 Yanlış kind → 400:** `regular-candidates` yalnız Regular session; `makeup-candidates` yalnız Makeup session. Yanlış türde → **400 BadRequest** (boş liste değil — bug gizlenmesin).
- **D4 Aynı öğrenci çok line:** her uygun line **ayrı candidate row**; line etiketi net; line-seçtirme modalı geri gelmez; "Ekle" o satırın `LineId`'iyle booking yapar.
- **D5 Slotsuz legacy:** aday olur ama **en sonda**; slotsuzlar kendi aralarında **öğrenci adına** göre.

## 1. Mevcut kod (yeniden kullanılan)
- `ContractLineRules.AvailableAsync(db,line,ct)` = **AvailableForPlanning** (`LessonCount − Booked Regular`).
- `SessionRules.StudentConflictAsync` / `PendingStudentConflictAsync` (`IReadOnlyCollection<Guid>`): aynı-saat çakışma.
- `MakeupEligibleLineQuery.BuildAsync` (28C-2): Active line + line-bazlı makeup available>0 + pasif hariç + level. `MakeupEligibleLineDto` (lineLabel, availableCredits, level, isIndividual, classType...).
- `StudentContractLinePreferredSlot` (29A): `DayOfWeek`, `PreferredStartTime (TimeOnly?)`, `IsActive`.
- `ClassSession.StartAt` → `sessionDate`, `sessionDay=StartAt.DayOfWeek`, `sessionTime=TimeOnly.FromDateTime(StartAt)`.

## 2. Regular candidate — `GetRegularBookingCandidatesQuery(ClassSessionId, StudentLevel? Level)`
Session yükle; **`SessionKind != Regular` → 400** ("Bu uç yalnız normal (Regular) seanslar içindir.").
Filtreler:
- `line.Status==Active` · `ClassTypeId==session.ClassTypeId` · `InstructorProfileId==session.InstructorProfileId` (28C-1) · `StartDate<=sessionDate<=EndDate` · öğrenci `!=Inactive` · `level==null || öğrenci.Level==level`.
- **AvailableForPlanning > 0** (`AvailableAsync`).
- **Bireysel kuralı:** `line.IsIndividual && !session.IsIndividual` → ele.
- **Gün hard-filter:** aktif slotlar arasında `DayOfWeek==sessionDay` varsa aday (`matchedDay=true`); **slot yoksa** aday (`hasSlots=false`, en sona); slotu var gün yoksa → **ele**.
- **Çakışma:** `StudentConflictAsync` veya `PendingStudentConflictAsync` true → ele; bu seansta zaten `Booked` → ele (duplicate).

**Sıralama:** (1) slotlu+gün-eşleşen önce, slotsuz legacy en sonda; (2) gün-eşleşen içinde timed slot `|PreferredStartTime − sessionTime|` (dk) en küçük üstte; (3) saatsiz slot timed'lardan sonra; (4) slotsuz legacy en sonda; (5) `StudentName` artan.

**DTO** `RegularCandidateDto(StudentProfileId, StudentName, StudentLevel?, LineId, LineLabel, ClassTypeName, AvailableForPlanning, IsIndividual, MatchedDay, HasPreferredSlots, NearestPreferredTime (TimeOnly?), MinutesFromSession (int?))`. Aynı öğrencinin çok uygun line'ı → çok satır (D4).

## 3. Makeup candidate — `GetMakeupBookingCandidatesQuery(ClassSessionId, StudentLevel? Level)`
Session yükle; **`SessionKind != Makeup` → 400** ("Bu uç yalnız telafi (Makeup) seanslar içindir.").
`MakeupEligibleLineQuery.BuildAsync(level)` + session-scope:
- `line.ClassTypeId == session.ClassTypeId` (**server-side**; mismatch gelmez).
- line-bazlı makeup **available>0** (mevcut).
- **Çakışma:** `StudentConflictAsync`/`PendingStudentConflictAsync` → ele; bu seansta zaten Booked → ele.
- **Tercih gün/saat kuralı UYGULANMAZ** (gün-bağımsız). Level opsiyonel.
- **DTO:** mevcut `MakeupEligibleLineDto` reuse (`MakeupCandidateDto = MakeupEligibleLineDto`). Sıralama: ad → classType.

## 4. Endpoint + Auth
`BookingsController` (base `api/studio`, `[Authorize(Roles="Owner,StudioAdmin")]`):
- `GET class-sessions/{sessionId:guid}/regular-candidates?level=`
- `GET class-sessions/{sessionId:guid}/makeup-candidates?level=`
Wrong-kind → 400 (handler `BadRequestException`/`ValidationException`). Auth controller-seviyesinde (Instructor 403).

## 5. Frontend (`schedule/page.tsx` StaffBookingModal)
- Regular dalı: `bookingCandidateService.regular(sessionId, level)`; **client-side line çözümü + pickLines KALDIRILIR**. Her satır (öğrenci, line); "Ekle" → `bookingService.create(sessionId, studentProfileId, lineId)`.
- Makeup dalı: `bookingCandidateService.makeup(sessionId, level)`; **client-side classType filtresi kalkar**. Level dropdown backend'e gider.
- **Regular row rozetleri:** line label · AvailableForPlanning (kalan planlanabilir) · tercih günü/saat (matchedDay+NearestPreferredTime; slotsuz→"tercih yok") · dakika yakınlığı (±N dk) · seviye · Bireysel/Grup.
- **Makeup row rozetleri:** line label · availableCredits (kalan telafi) · seviye · Bireysel/Grup.

## 6. Kapsam dışı
delivered consumption · planned/delivered/remaining · unconfirm · payment değişikliği · preferred slot migration · due-date reminder.

## 7. Test (`sprint29b-smoke.sh`)
gün eşleşen aday gelir · slotlu gün-eşleşmeyen gelmez · slotsuz legacy en sonda · 18:00'da 18:30 tercih 20:00'den üstte · saatsiz slot timed'lardan sonra · level filtresi · same-time çakışan gelmez · pending conflict gelmez · bu seansta booked duplicate gelmez · bireysel line grup dersinde gelmez · AvailableForPlanning=0 gelmez · makeup gün/saat bağımsız · makeup classType server-side eşleşir · makeup available yoksa gelmez · makeup çakışan elenir · wrong-kind 400 (her iki uç) · Owner/StudioAdmin erişir · Instructor 403 · aynı öğrenci 2 uygun line → 2 row. Regresyon: v1.8/v1.7/v1.6/v1.5 yeşil. `run-smokes.sh`'e eklenir.

## 8. Riskler
N+1 per-line available/conflict (makeup deseni gibi; aday listesi sınırlı, sessiz cap yok) · saat-null yakınlık sıralaması kenar durumları · slotsuz legacy seli (en sonda + filtreler hafifletir) · aynı öğrenci çok satır UI netliği.

## 9. Uygulama sırası
1. `RegularCandidateDto` + query/handler. 2. Makeup query/handler (session-scope wrapper). 3. Controller 2 route + wrong-kind 400. 4. Frontend types + `bookingCandidateService` + StaffBookingModal rewrite + rozetler. 5. `sprint29b-smoke` + kayıt. 6. Doğrulama (build/tsc/next 0 · migration yok · has-pending temiz · smoke 37 · E2E · compose).
