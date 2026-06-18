# SPRINT 18 — Sözleşme + Sözleşmeden Program (Faz 1)

> Epic: [EPIC-CONTRACTS.md](EPIC-CONTRACTS.md). Bu Faz 1'in spec'i. Faz 1, riski yönetmek için **iki alt-sprinte
> bölündü** (gerekçe aşağıda). Kod uygulanırken bu dosya güncellenir.

## Neden iki alt-sprint?

Faz 1'in çekirdek davranışı **"Regular booking artık sözleşmeye dayanır, pakete değil"**. Bu **kesme (cutover)**
mevcut booking/scheduling kullanan **~10 smoke + e2e-seed + birkaç E2E**'yi kırar (hepsi bugün paket/seans
üzerinden booking yapıyor). Kırılan testleri tek seferde yeni sözleşme akışına taşımak, yeni entity + program
üretimi + frontend ile birleşince tek turda **yarım yamalak** kalma riski taşır (kullanıcı kuralı #5).

Bu yüzden:

- **Sprint 18a — Sözleşme temeli (ADDITIVE, kırıcı değil).** Yeni yetenek eklenir; mevcut akış bozulmaz.
- **Sprint 18b — Booking kesme (BREAKING, bilinçli).** Sözleşme zorunlu olur, paket gate/self-booking kalkar,
  tüm test scaffolding sözleşmeye taşınır.

---

## Sprint 18a — Sözleşme temeli (additive)

### Backend
1. **`StudentContract` entity** + enum `StudentContractStatus {Active, Completed, Cancelled, Expired}` + config +
   DbSet + `IAppDbContext`. Migration `Sprint18_StudentContractFoundation`.
2. **`ClassSession`** alanları: `IsIndividual (bool, default false)`, `ActualInstructorProfileId (Guid?)`;
   `Capacity` semantiği gevşetilir (grup için pratikte sınırsız — detay aşağıda). Aynı migration.
3. **`ClassBooking`**: `StudentContractId (Guid?)` (FK SetNull). Aynı migration.
4. **Contract feature** (`Features/Contracts/`): `CreateStudentContract`, `GetStudentContracts(studentId)`,
   `GetStudentContract(id)` (+ türetilen sayılar), `CancelStudentContract`. Owner/StudioAdmin;
   `IStudioWriteRequest`. Controller: `api/studio/students/{studentId}/contracts`, `api/studio/contracts/{id}`.
5. **`ContractEligibilityChecker`**: bir öğrenci+sözleşme için available-lesson hesabı
   (`Total − (o sözleşmeye bağlı aktif Booked, Makeup hariç)`), ders tipi/tarih uyumu. Sprint 14 "available
   credit" deseninin sözleşme karşılığı.
6. **`GenerateContractScheduleCommand(contractId, slots[{dayOfWeek, startTime}], reuseExistingGroup, individual)`**:
   StartDate..EndDate, seçili gün/saat, `TotalLessonCount` dolana kadar; kapanış günlerini atla; eşleşen mevcut
   seans (hoca+ders tipi+tarih+saat) varsa **reuse** (gruba bağla) yoksa yeni `ClassSession` + `ClassBooking
   (StudentContractId)`. **Çakışma: hoca + öğrenci** (yeni öğrenci kontrolü); availability **advisory**.
   Önce **önizleme** (hangi tarihler), sonra **onay**. Bireysel ise IsIndividual=true + 2. öğrenci engeli.
7. **`BookingCreator` additive contract path:** `StudentContractId` verilirse paket yerine
   `ContractEligibilityChecker` kullanılır; verilmezse **mevcut paket yolu aynen çalışır** (geri uyum →
   mevcut testler bozulmaz). **Öğrenci çakışma kontrolü** her iki yolda eklenir. **Bireysel seans** enforcement.
8. **Capacity:** grup seansında üst sınır yok (kullanıcı elle yönetir). Uygulama: `IsIndividual` → max 1;
   değilse `TryReserveSeatAsync` kapasite kontrolünü atlar/sonsuz sayar (BookedCount yalnız gösterim).

### Frontend
9. Öğrenci detayında **"Sözleşmeler"** bölümü: oluştur (ders tipi, hoca, başlangıç/bitiş, gün+saat slotları,
   ders hakkı, telafi sayısı, ücret, bireysel mi), listele (kalan ders/durum), iptal.
10. Sözleşme detayında **"Programı oluştur"** → slot önizleme → onay → sessions/bookings. "Mevcut gruba bağla".
11. Schedule görünümünde **grup/bireysel rozeti** + hoca-değişikliği yeri (alan eklendi, Faz 2'de dolar).

### Sprint 18a testleri (sprint18-smoke)
- Sözleşme oluştur → 200; alanlar doğru; türetilen available = Total.
- Programdan-program: Pzt/Prş 18:00, 16 ders → tam 16 session+booking; doğru tarihler; kapanış atlanır.
- 17. sözleşme-booking'i → 409 (available aşımı).
- Aynı hoca aynı saat 2. ders → 409; **farklı hoca aynı saat → 200**; aynı öğrenci çakışma → 409.
- Bireysel seansa 2. öğrenci → 409; grup seansa N öğrenci → 200.
- Mevcut gruba bağlanma → yeni session açılmaz, mevcut session'a booking.
- **Regresyon:** mevcut paket-tabanlı booking aynen çalışır → mevcut smoke + E2E **bozulmaz** (additive).

---

## Sprint 18b — Booking kesme (breaking, bilinçli)

> 18a yeşil olduktan sonra. Burada mevcut testler **kasıtlı** kırılır ve sözleşmeye taşınır.

1. **Regular booking sözleşme zorunlu:** `BookingCreator` Regular dalında paket gate'i kaldırılır; sözleşmesiz
   Regular booking → 409/410. `PackageEligibilityChecker`/`StudentPackageConsumer` çağrılmaz.
2. **Öğrenci self-booking kaldırılır** (admin-güdümlü): `StudentBookCommand` / `book-classes` deprecate (410 +
   UI gizleme).
3. **Paket UI deprecate:** paket-plan, paket-sat, paket-uygunluk göstergeleri gizlenir (DB drop YOK).
4. **Test scaffolding sözleşmeye taşınır:** e2e-seed + ~10 smoke (sprint2/3/4/5/6/7/8/9/14, security-audit)
   booking yapan yerler önce sözleşme oluşturur. Paket smoke'ları (sprint7/14) **deprecate** işaretlenir veya
   sözleşmeye taşınır. E2E 04/10 güncellenir.
5. **Tam doğrulama:** smoke suite + E2E + compose config/up + full compose validation.

---

## Faz 1'de YAPILMAYANLAR (sonraki fazlar)
Yoklama "hepsini onayla" + substitution **kullanımı** (alan 18a'da eklenir, mantık Faz 2); telafi yeni gating +
kazanım + kapanış→telafi (Faz 3); drag-drop + zincir-kaydır + köklü düzen değişikliği (Faz 4); sözleşme uzatma +
borç (Faz 5); hoca ücreti UI/rapor temizliği (Faz 2). **Paket DB drop hiçbir fazda yok.**

## Açık sorular
Bkz. epic. Faz 1 için kritik onaylar (kullanıcı kararlarıyla netleşti): paket dormant (drop yok), self-booking
yok, availability advisory, çakışma hoca+öğrenci, bireysel seans düzeyinde, sözleşmesiz Regular yok.
