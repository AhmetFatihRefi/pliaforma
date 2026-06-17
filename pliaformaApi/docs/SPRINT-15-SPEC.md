# Sprint 15 — Self-Service Kayıt & Stüdyoya Katılım İsteği

> **Durum:** SPEC TASLAĞI · Kodlama onayı bekleniyor.
> **Önemli:** Bu belge yalnızca **karar/kapsam** içerir. Henüz kod/migration yazılmadı.
> Sprint 15 kodlaması için **ayrı onay** beklenecek.

---

## 0. Ana Hedef

Kullanıcılar kendi hesaplarını açabilsin. **Öğrenci veya hoca olarak** bir stüdyoya **katılım isteği**
gönderebilsin. Stüdyo sahibi/admin **onaylamadan aktif üye olmasın**. Onaydan sonra role göre
**StudentProfile** veya **InstructorProfile** oluşsun. Stüdyoya katılım **public slug ile değil, tahmin
edilemez JoinCode** ile yapılsın. Herkese açık stüdyo arama/dizini **yok** (pilot için JoinCode yeterli).

---

## 1. Mevcut Durum (kanıtlanmış kod gerçekleri — referans)

| Alan | Gerçek |
|---|---|
| `POST /api/auth/register` | **Anonim**; global `User` (GlobalRole=Standard) oluşturur, **üyelik yok**. Frontend'de **sayfası YOK**. |
| `POST /api/auth/register-studio` | Anonim; User + Studio + Owner(Active) üyelik + 14g trial. Frontend `/register-studio`. |
| `POST /api/members/apply` (`ApplyToStudio.cs`) | Auth; **slug** ile; **Instructor** olarak Pending üyelik (`RequestedByUser`) + **InstructorProfile'ı HEMEN oluşturur** (Rate=0). Duplicate: (StudioId,UserId) varsa 409. |
| `POST /api/members/{id}/accept` · `/reject` (`DecideMembership.cs`) | Auth; **yalnız status değiştirir** (Active/Rejected); profil oluşturmaz. |
| Yetki (`MembershipAccess.cs`) | `InvitedByStudio` → davet edilen kullanıcı karar verir; **`RequestedByUser` → o stüdyonun Owner/StudioAdmin'i karar verir** (✅ zaten kurulu). |
| `POST /api/studio/memberships/invite` (`InviteMember.cs`) | Owner/StudioAdmin; **var olan** kullanıcıyı e-posta ile davet; Instructor ise InstructorProfile'ı invite anında oluşturur. |
| Öğrenci girişi | **Yalnız personel-tetikli:** `POST /api/studio/students/guest` → `convert-to-user`. Self-service öğrenci YOK. |
| Frontend auth | `/login`, `/register-studio`, `/select-studio`. **`/register` ve `/join-studio` YOK.** |
| `StudioMembership` | `StudioId, UserId, Role(Owner/StudioAdmin/Instructor/Student), Status(Pending/Active/Rejected/Removed), Direction, AcceptedAt?`. |

**Çıkarım:** Onay altyapısı (apply→pending→studio accept) mantığı **var**; eksikler: (a) `/register` UI yok,
(b) apply **slug** kullanıyor + **profili erken** oluşturuyor + **rol seçtirmiyor** (hep Instructor),
(c) öğrenci self-apply yolu yok, (d) JoinCode yok, (e) stüdyosuz-kullanıcı yönlendirmesi yok.

---

## 2. Locked Decisions (kullanıcı onayı)

1. **Ortak User modeli, ayrı UX.** Ayrı StudentUser/InstructorUser **yok**; `User` ortak, rol stüdyo bazında
   `StudioMembership` üzerinden. Frontend UX ayrı: "Öğrenci olarak katıl" / "Hoca olarak katıl".
2. **JoinCode ile katılım (slug ile DEĞİL).** `Studio` + `JoinCode` (random, tahmin edilemez), `JoinCodeUpdatedAt?`,
   `JoinCodeIsActive` (bool, default true). Owner/StudioAdmin ayarlardan görür + yeniler; ileride pasifleştirilebilir.
3. **Profil onaydan SONRA oluşur.** Başvuruda **profil oluşturulmaz** — yalnız Pending üyelik. Accept'te:
   Role=Student→StudentProfile, Role=Instructor→InstructorProfile. **`ApplyToStudio` revize edilir** (profil
   oluşturma kaldırılır); **`AcceptMembership` profil oluşturma ekler** (idempotent: profil yoksa oluştur →
   invite yolu zaten oluşturduysa no-op, asimetri olmadan çalışır).
4. **Başvuru rolleri sınırlı:** yalnız **Student** ve **Instructor**. Owner/StudioAdmin/PlatformAdmin'e
   başvurulamaz (validasyon + handler reddi).
5. **Pending/duplicate:** aynı kullanıcı aynı stüdyoya — **Active → 409**, **Pending → 409**,
   **Rejected/Removed → yeniden başvurabilir** (mevcut satır Pending'e çevrilerek yeniden kullanılır; yeni rol
   yazılır, AcceptedAt temizlenir). Böylece (StudioId,UserId) tekilliği korunur, geçmiş kayıt çoğalmaz.
6. **Stüdyosuz kullanıcı deneyimi:** login olmuş ama aktif üyeliği olmayan kullanıcı dashboard'a düşmez →
   **`/join-studio`**'ya yönlenir.
7. **Stüdyo tarafı pending istek ekranı:** Owner/StudioAdmin bekleyen istekleri (isim/e-posta/istenen rol/tarih)
   görür, kabul/ret eder; kabulde profil oluşur.

---

## 3. Kapsam (uygulama maddeleri)

### 3.1 Studio JoinCode (yeni alanlar + migration)
- `Studio` + `JoinCode (string, unique)`, `JoinCodeUpdatedAt (DateTime?)`, `JoinCodeIsActive (bool, default true)`.
- **Üretim:** kayıt-stüdyoda + migration **backfill** ile mevcut tüm stüdyolara random kod (tahmin edilemez:
  ör. 10 karakter Base32, ~50 bit; karışabilecek karakterler (0/O, 1/I) çıkarılır). Global unique (çakışmada yeniden üret).
- **Endpoint (Owner/StudioAdmin):** `GET /api/studio/join-code` (kodu + durumu döner), `POST /api/studio/join-code/regenerate`
  (yeni kod üretir, `JoinCodeUpdatedAt` günceller). Ayarlar ekranında gösterilir.

### 3.2 Açık hesap oluşturma (frontend)
- Backend `POST /api/auth/register` **var** — yalnız **`/register` sayfası** eklenir (ad/e-posta/parola/telefon).
- Aynı e-posta ile register → **409** (mevcut davranış). Kayıt sonrası otomatik login → üyeliği yoksa `/join-studio`.

### 3.3 JoinCode ile katılım isteği (`ApplyToStudioCommand` revizyonu)
- İmza: `ApplyToStudioCommand(string JoinCode, StudioRole Role)` (slug yerine JoinCode; + Role).
- **Validasyon:** Role ∈ {Student, Instructor} (aksi → 400/422). JoinCode boş değil.
- **Akış:** JoinCode ile aktif (`JoinCodeIsActive=true`) stüdyo bulunur; yoksa **generic 404** "Geçersiz veya pasif
  katılım kodu." (stüdyo varlığı sızdırılmaz). Var olan (StudioId,UserId) üyelik:
  - Active/Pending → **409** "Bu stüdyoya zaten üyesiniz veya bekleyen başvurunuz var."
  - Rejected/Removed → satır yeniden kullanılır: `Role=istenen`, `Status=Pending`, `Direction=RequestedByUser`,
    `AcceptedAt=null`.
  - Yoksa → yeni Pending üyelik (`RequestedByUser`, `Role=istenen`).
- **Profil OLUŞTURULMAZ** (madde Locked #3). `IStudioWriteRequest` değil (tenant-öncesi).

### 3.4 Onayda profil oluşturma (`AcceptMembership` revizyonu)
- Mevcut: status→Active. **Eklenir:** Active'e çevirmeden önce/sonra (aynı tx):
  - `Role==Instructor` ve (StudioId,UserId) için aktif InstructorProfile **yoksa** → oluştur (DisplayName=user.FullName,
    Rate=0, RateType=PerSession). (Invite yolu zaten oluşturduysa no-op.)
  - `Role==Student` ve (StudioId,UserId) için StudentProfile **yoksa** → oluştur (UserId set, Status=Active,
    StudioMembershipId bağlı, Source=Web, CreatedByUserId=onaylayan).
- Reject: profil oluşmaz (mevcut davranış korunur).
- Yetki: `MembershipAccess` (RequestedByUser → Owner/StudioAdmin) **zaten doğru**; değişmez.

### 3.5 Stüdyo tarafı bekleyen istekler
- Owner/StudioAdmin için bekleyen **RequestedByUser + Pending** üyelikleri listeleyen görünüm. Mevcut
  `GET /api/studio/memberships` genişletilir veya `GET /api/studio/join-requests` eklenir; DTO: membershipId,
  applicantName, applicantEmail, requestedRole, requestedAt, direction. Kabul/ret mevcut `accept/reject` ile.

### 3.6 Frontend
- **`/register`** (açık kayıt) + **`/join-studio`** (JoinCode input + rol seçimi Öğrenci/Hoca + "bekleyen
  başvurularım" + "reddedilen başvurularım"). Login/register sonrası **üyeliksiz kullanıcı `/join-studio`'ya** yönlenir
  (mevcut PlatformAdmin→/platform redirect deseni gibi `(dashboard)/layout` veya auth guard'da).
- Stüdyo: ayarlarda **JoinCode görünümü + "Kodu yenile"**; üyelik yönetimi ekranında **"Bekleyen katılım istekleri"**
  bölümü (isim/e-posta/rol/tarih + Kabul/Ret).
- `domain-types.ts` + `studioApi.ts`/auth servisleri senkron.

---

## 4. Migration & Backfill

- `Sprint15_JoinCodeAndSelfService` (öngörülen): `Studio.JoinCode/JoinCodeUpdatedAt/JoinCodeIsActive` kolonları +
  **backfill** mevcut stüdyolara random benzersiz JoinCode (`JoinCodeIsActive=true`). Unique index `Studio.JoinCode`
  (filtered, IsDeleted=false). Backfill kimseyi etkilemez (yalnız kod atar).
- Profil/üyelik için yeni tablo gerekmez (mevcut `StudioMembership`/`StudentProfile`/`InstructorProfile` yeterli).

---

## 5. Email Doğrulaması (bu sprintte YOK — not)
- Bu sprintte gerçek email gönderimi/doğrulaması **yok**. Register/login email doğrulamasız çalışır.
- İleride `User.EmailVerifiedAt` eklenebilir; doğrulanmamış email bazı işlemleri kısıtlayabilir. Spec'te not olarak kalır.

## 6. Güvenlik
- JoinCode **tahmin edilemez** (yeterli entropi, CSPRNG). Geçersiz/pasif kod → **generic mesaj** (stüdyo varlığı/ismi sızmaz).
- `apply` + `register` + `join-code/regenerate` **rate limit** altında (mevcut auth limiter veya ayrı politika).
- Kullanıcı **başka stüdyonun** pending listesini göremez; Owner/StudioAdmin yalnız **kendi** stüdyosunun isteklerini görür.
- Student/Instructor **pending istek onaylayamaz** (MembershipAccess). PlatformAdmin tenant **bypass etmez**.

---

## 7. Testler (sprint15-smoke + E2E)

- Açık `register` başarılı; **aynı email ile register → 409**.
- Login sonrası **stüdyosuz kullanıcı `/join-studio`'ya yönlenir** (E2E).
- **JoinCode ile Student başvurusu** oluşturulur (Pending, profil YOK).
- **JoinCode ile Instructor başvurusu** oluşturulur (Pending, profil YOK).
- **Geçersiz/pasif JoinCode → generic 404** (stüdyo bilgisi sızmaz).
- Aynı stüdyoya **ikinci Pending başvuru → 409**; **Active üyeyken tekrar başvuru → 409**; **Rejected sonrası yeniden başvuru → 200 (Pending)**.
- Owner pending Student başvurusunu **kabul → StudentProfile oluşur** (Active üyelik).
- Owner pending Instructor başvurusunu **kabul → InstructorProfile oluşur** (Active üyelik).
- **Reject sonrası aktif profil oluşmaz.**
- **StudioAdmin de kabul edebilir** (MembershipAccess Owner|StudioAdmin) — doğrula.
- Owner/StudioAdmin/PlatformAdmin rolüne **başvuru → 400/422** (sınırlı roller).
- **Student/Instructor pending requests'i göremez** (403); **cross-tenant pending list → 403/404**.
- JoinCode regenerate sonrası **eski kod çalışmaz, yeni kod çalışır**; Instructor/Student regenerate → 403.
- **Mevcut 15 smoke + security + 31 E2E bozulmaz** (özellikle Sprint 1 üyelik, mevcut instructor invite/apply, Sprint 2 öğrenci).

---

## 8. Sprint 15 Rapor Başlıkları (bittiğinde)
1. Genel Özet 2. Oluşturulan/Değiştirilen Dosyalar 3. JoinCode (entity+migration+backfill) 4. Açık Kayıt (register UI)
5. Katılım İsteği (JoinCode+rol+pending/duplicate) 6. Onayda Profil Oluşturma (Student/Instructor) 7. Stüdyosuz
Kullanıcı Deneyimi 8. Stüdyo Pending İstek Ekranı 9. Yetki & Tenant 10. Güvenlik 11. Test/Doğrulama Sonuçları
12. Bilinçli Yapılmayanlar 13. Riskler/Borçlar 14. Sonraki Sprint Önerisi.

---

## 9. Bu sprintte YAPILMAYACAKLAR
- Gerçek email verification / email gönderimi.
- Password reset.
- Public stüdyo arama/dizini.
- Invitation link/token sistemi (JoinCode yeterli).
- Online ödeme.
- Büyük refactor; pipeline/tenant güvenliği değişmez.
- Commit/push (onaysız).

---

## 10. Açık Karar Noktaları (kodlama onayında netleşecek)
- **Pending istek listesi:** mevcut `GET /api/studio/memberships`'i mi genişletelim yoksa ayrı `GET /api/studio/join-requests`
  mi? (Öneri: ayrı endpoint — temiz DTO + applicant email/role/tarih.)
- **JoinCode formatı/uzunluğu:** öneri 10 karakter Base32 (karışan karakterler çıkarılmış). Onayında sabitlenir.
- **Invite yolu:** InviteMember'ın invite-anında InstructorProfile oluşturması korunur (accept idempotent oluşturma
  ile uyumlu); istenirse o da accept'e ertelenebilir (kapsam dışı bırakıldı).
