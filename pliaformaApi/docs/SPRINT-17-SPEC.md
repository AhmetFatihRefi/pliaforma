# Sprint 17 — Davetlerim, Hoca Davet Akışı & Stüdyosuz Kullanıcı Alanı (SPEC)

> Durum: **Spec onaylandı; kodlama için ayrı onay beklenecek.** Pilot **öncesi** küçük tamamlayıcı
> sprint (büyük feature değil — mevcut davet akışının UI + bildirim tamamlanması).
> Bu turda yalnızca bu dosya yazılır; kod/migration **yazılmaz**.

## Amaç

İki onboarding modelini netleştirmek ve mevcut davet akışının eksik UI/bildirim parçalarını tamamlamak:

- **Öğrenci → JoinCode ile başvuru** (Sprint 15 modeli korunur).
- **Hoca → yalnız stüdyo daveti** (self-apply kapanır).
- Davet edilen kullanıcı daveti **uygulamada görüp kabul/ret edebilir** ("Davetlerim").
- Davet anında **çana InApp bildirim** düşer.
- Davet kabulünde **e-posta doğrulaması zorunlu** (Sprint 16 ile tutarlı).
- Stüdyosuz kullanıcı **hata değil, bilinçli empty-state** — ileride discovery/marketplace'e evrilebilir
  (bu sprintte yalnız dokümante edilir, kodlanmaz).

---

## Doğrulanan mevcut durum (kanıtlanmış kod gerçekleri)

- **Hoca self-apply açık:** `Features/Members/ApplyToStudio.cs` — validator `Role`'ü `Student` **veya**
  `Instructor` kabul ediyor (satır 26-27); handler ikinci kez doğruluyor (47-48). Burada hoca kapatılacak.
- **Davet bildirimi yok + profil erken oluşuyor:** `Features/Members/InviteMember.cs` (48-93) Pending +
  `Direction=InvitedByStudio` membership **+ (Instructor ise) `InstructorProfile`'ı invite anında**
  (75-87) yaratıyor; **hiç `Notification` üretmiyor**. Mevcut user yoksa `ConflictException` atıyor
  (e-posta ile var olan kullanıcıyı arar). **Bu sprintte:** invite anındaki profil oluşturma KALDIRILIR
  (profil yalnız accept'te oluşur — madde 7); bildirim EKLENİR (madde 4).
- **Backend davet listesi hazır, UI yok:** `GET /api/members/my-invitations`
  (`Features/Members/GetMyPendingInvitations.cs` → `Status=Pending && Direction=InvitedByStudio`).
  Frontend `lib/studioApi.ts:92` `myInvitations()` tanımlı **ama hiçbir sayfa çağırmıyor** → davet edilen
  kullanıcı daveti göremiyor/kabul edemiyor.
- **Kabul yolu hazır:** `Features/Members/DecideMembership.cs` — `AcceptMembershipCommand` →
  `Status=Active` + `EnsureRoleProfileAsync` (idempotent: Instructor→InstructorProfile, Student→StudentProfile).
  `RejectMembershipCommand` → `Status=Rejected` (profil yaratmaz).
- **Stüdyosuz yönlendirme var:** `app/(dashboard)/layout.tsx:16-26` →
  `needsJoin = user && !platformOnly && !activeStudio && memberships.length === 0` ise `/join-studio`'ya
  yönlendiriyor. Bu ekran bugün "kod gir" + başvuru durumu gösteriyor; **davetleri göstermiyor**.
- **NotificationType string saklanıyor:** `Persistence/Configurations/NotificationConfiguration.cs:14` →
  `Property(x => x.Type).HasConversion<string>().HasMaxLength(32)`. Enum'a yeni değer eklemek **şema
  migration'ı gerektirmez** ("MembershipInvite" = 16 karakter < 32). Bildirim yaratma deseni:
  `Features/Notifications/ReminderScanner.cs:54-65` → doğrudan `new Notification {…}` +
  `_db.Notifications.Add` + `_sender.SendAsync` (InApp/Log aktif; Email/SMS no-op).
- **NotificationType mevcut değerler:** `Domain/Enums/NotificationEnums.cs` — `ClassReminder=0,
  PaymentDueSoon=1, PaymentOverdue=2, MakeupCreditGranted=3, BookingCancelled=4, SessionRescheduled=5,
  System=6`. Davet için uygun tip yok.

---

## Locked Decisions (kullanıcı onayı)

1. **Davetlerim konumu:** Ayrı `/invitations` route **açılmaz**. Davetler `/join-studio` içinde bir
   "Davetlerim" bölümü olur (stüdyosuz kullanıcı zaten oraya düşüyor → en az sürtünme). İleride
   discovery/marketplace büyürse ayrı `/invitations` veya `/discover` düşünülebilir.
2. **Zamanlama:** Pilot **öncesi** yapılır (hoca onboarding'i pilotta karışmasın; iş küçük, backend %70 hazır).
3. **Hoca self-apply kapanır:** `Student` JoinCode başvurabilir; `Instructor` başvuramaz → backend 400/422,
   frontend'de hoca seçeneği kalkar/disabled. Hoca yalnız davetle katılır.
4. **Davet kabulünde e-posta doğrulaması zorunlu:** Kullanıcı login olabilir, davetlerini görebilir,
   reddedebilir; **ama kabul için `EmailVerifiedAt` dolu olmalı**. Değilse accept **403**:
   "Daveti kabul etmek için önce e-posta adresinizi doğrulayın." Frontend'de unverified kullanıcıda Kabul
   butonu disabled + doğrulama banner/resend görünür.
5. **Davet bildirimi:** `InviteMember` başarılı olunca davet edilen kullanıcıya bir `Notification`
   (`Type=MembershipInvite`) oluşur; `DeduplicationKey` ile tekilleştirilir (aynı davet tekrar bildirim
   üretmez).
6. **Existing invite yolu korunur:** Hesap varsa davet edilir; hesap yoksa hata. Hesabı olmayana e-posta
   davet linki **bu sprintte yok** — sadece mesaj netleşir.
7. **Profil yalnız kabulde oluşur (REV1):** `InviteMember` invite anında **InstructorProfile oluşturmaz**;
   yalnız Pending membership üretir. Profil yalnız `AcceptMembership` sırasında (idempotent
   `EnsureRoleProfileAsync`) oluşur. Kabulden önce davet edilen kişi **aktif hoca gibi görünmez**
   (instructor listeleri / availability / schedule). Reject sonrası InstructorProfile **oluşmamış** olur.
8. **`/join-studio` çift amaçlı (REV1):** Yalnız "stüdyosuz kullanıcı ekranı" değil; aynı zamanda
   **"Davetlerim ve katılım"** ekranı. **Aktif üyeliği olan** kullanıcı da yeni bir stüdyodan davet
   alabilir ve menü/bildirim linkiyle bu ekrana gidip daveti görebilir. Stüdyosuz kullanıcı buraya
   otomatik yönlenir; üyeli kullanıcı manuel/bildirim üzerinden erişir.

---

## Kapsam

### 1. Hoca self-apply kısıtı (backend)

`Features/Members/ApplyToStudio.cs`:

- Validator'a kural: `Role == StudioRole.Instructor` ise reddet → `ValidationException`/422.
  Mesaj: **"Hocalar bir stüdyoya kodla başvuramaz; stüdyonun davet etmesi gerekir."**
- Handler'daki ikinci doğrulama gate'i de aynı kuralı içerir (savunmacı; doğrudan komut çağrısına karşı).
- **Öğrenci (`Student`) JoinCode başvurusu aynen kalır** (200). Owner/StudioAdmin zaten engelli (değişmez).

### 2. `/join-studio` JoinCode formu (frontend)

- Rol seçiminde **yalnız Student**. Hoca seçeneği kaldırılır veya disabled + ipucu:
  **"Hocalar stüdyo davetiyle katılır."**
- Form gönderimi yalnız Student rolüyle yapılır (UI seviyesinde de tutarlı).

### 3. "Davetlerim" bölümü (frontend — yeni)

- `/join-studio` içinde `myInvitations()` tüketen bir bölüm. Her davet kartı:
  **Stüdyo adı · Rol · Davet tarihi · [Kabul] · [Reddet]**.
- **Kabul** → `AcceptMembershipCommand` (mevcut `accept` endpoint). Başarılıysa: `auth/me` veya membership
  state yenilenir; aktif üyelik oluştuysa kullanıcı dashboard/select-studio akışına geçer (mevcut routing
  guard otomatik halleder).
- **Reddet** → `RejectMembershipCommand`. Sonuç `Rejected`; kullanıcı stüdyosuz kalırsa `/join-studio`'da kalır.
- **Email verification gate (frontend):** `user.emailVerified === false` ise Kabul butonu **disabled** +
  doğrulama banner + resend butonu (mevcut Sprint 16 resend akışı). Reddet butonu unverified'da da açık kalır.

### 4. Davet bildirimi (backend — çana düşsün)

- `Domain/Enums/NotificationEnums.cs`: **`MembershipInvite = 7`** (enum sonuna eklenir; string saklandığı
  için şema migration gerekmez).
- `Features/Members/InviteMember.cs`: membership kaydedildikten sonra davet edilen kullanıcıya
  `ReminderScanner` desenini izleyen bir `Notification`:
  - `RecipientUserId` = davet edilen user
  - `StudioId` = davet eden stüdyo
  - `Type = NotificationType.MembershipInvite`
  - `Title = "Stüdyo daveti"`
  - `Body = "{StudioName} sizi {Role} olarak davet etti."`
  - `DeduplicationKey = $"membership-invite:{membershipId}"`
  - Kanal: mevcut InApp + Log üzerinden gönderilir (`_sender.SendAsync`); Email/SMS no-op (değişmez).
- **Dedup:** Aynı membership daveti için ikinci kez bildirim oluşmaz (`DeduplicationKey` mevcut tekilleştirme
  mekanizmasıyla — `ReminderScanner`'daki gibi). Davet zaten benzersiz membership üretir; yeniden gönderim
  senaryosunda dedup key tekrarı engeller.

### 5. `/join-studio` — çift amaçlı: empty-state + Davetlerim/katılım (frontend, REV1)

`/join-studio` artık "hata/eksik" gibi değil, **iki rolü olan** bir ekran:

- **(A) Stüdyosuz kullanıcı için bilinçli empty-state.** Routing guard (`(dashboard)/layout.tsx`) bu
  kullanıcıyı buraya **otomatik** yönlendirir. Başlık: **"Henüz bir stüdyoya bağlı değilsiniz."**
- **(B) Aktif üyeliği OLAN kullanıcı için "Davetlerim ve katılım" ekranı.** Üyeli kullanıcı buraya
  **otomatik yönlenmez** ama **menü/link veya bildirim üzerinden** erişebilir (çünkü başka bir stüdyodan
  yeni davet alabilir). Bu durumda başlık daha nötr olur (örn. "Davetler ve stüdyoya katılım"); empty-state
  vurgusu yalnız (A)'da. → **Önemli:** `/join-studio`'nun render'ı `memberships.length === 0` koşuluna
  **bağlı olmamalı**; üyeli kullanıcı da sayfayı görebilmeli. (Routing guard yalnız stüdyosuz kullanıcıyı
  buraya *iter*; sayfanın kendisi üyeli kullanıcıyı geri *atmaz*.)

- **Bölümler (her iki rolde de):**
  1. **Davetlerim** (madde 3) — `#invitations` / `?tab=invitations` ile derin linklenebilir.
  2. **Öğrenci olarak JoinCode ile başvur** (madde 2 — yalnız Student).
  3. **Kendi stüdyonu oluştur** (`/register-studio`).
  4. *(İleride)* yakındaki stüdyolar / stüdyo keşfi / öne çıkan stüdyolar — **bu sprintte kodlanmaz**,
     yalnız placeholder/not olarak gösterilebilir.

- **Bildirim derin linki:** `MembershipInvite` bildirimine tıklanınca kullanıcı **`/join-studio?tab=invitations`**
  (veya `#invitations`) hedefine gider; bildirim dropdown'ından davete tıklayan kullanıcı doğrudan Davetlerim
  bölümünü görür. (Notification'a derin link alanı eklemek gerekmiyorsa, frontend bildirim tıklamasını
  `Type=MembershipInvite` için bu route'a maplemekle yetinir.)

- **Erişim noktası:** Üyeli kullanıcının bu ekrana ulaşması için en az bir giriş noktası bırakılır
  (bildirim tıklaması yeterli; opsiyonel olarak hesap/menüde "Davetlerim/Stüdyoya katıl" linki).

- **Dokümantasyon notu (kod yorumunda + bu spec):** *"Bu alan ileride stüdyo discovery/marketplace'e
  evrilebilir (kart tanıtım, öne çıkarma, harita/yakındaki stüdyolar). Sprint 17 kapsamı yalnız empty-state
  + Davetlerim + öğrenci JoinCode."*

### 6. Existing invite mesajı (frontend — küçük)

- Stüdyo tarafı `app/(dashboard)/dashboard/instructors/page.tsx` davet akışında, kullanıcı bulunamazsa mesaj
  netleştirilir: **"Bu e-posta ile kayıtlı kullanıcı bulunamadı. Davet edilecek kişi önce hesap
  oluşturmalı."** Yeni UI/endpoint gerektirmez (mevcut `ConflictException` mesajına hizalanır).

### 7. InviteMember: profil oluşturmayı kabule ertele (backend — REV1)

`Features/Members/InviteMember.cs`:

- **InstructorProfile oluşturma bloğu (satır 75-87) KALDIRILIR.** Handler yalnız:
  (a) mevcut user'ı e-posta ile bulur (yoksa `ConflictException` — değişmez),
  (b) tekrar/çakışan üyelik kontrolü (değişmez),
  (c) **Pending** + `Direction=InvitedByStudio` membership oluşturur,
  (d) davet bildirimi üretir (madde 4),
  (e) kaydeder.
- **Profil yalnız kabulde:** `DecideMembership.cs` `AcceptMembership` zaten `EnsureRoleProfileAsync` ile
  Instructor → `InstructorProfile` (idempotent) oluşturuyor → **değişmez**. Sprint 15 self-apply modeliyle
  bu sayede **tam uyumlu** olur (her iki yön de profili accept'te yaratır).
- **Reject yolu:** `RejectMembership` zaten profil oluşturmaz → reddedilen davette **InstructorProfile
  oluşmamış** kalır (invite anında da oluşmadığı için garanti).
- **Aktif görünürlük garantisi:** Kabulden önce membership `Pending` ve InstructorProfile yok →
  instructor listeleri / availability / schedule sorguları (Active membership / mevcut profil filtreleri)
  davet edileni **aktif hoca olarak göstermez**. (Bu sprintte ayrı filtre eklenmez; profilin hiç var
  olmaması en temiz çözümdür — kullanıcının tercih ettiği yaklaşım.)
- **Regresyon notu:** Eğer ileride bir yerin "invite anında profil var" varsayımına dayandığı görülürse,
  bu accept-time oluşturmaya taşınır; mevcut testlerin (Sprint 15/15.x davet) bu değişiklikle bozulmadığı
  doğrulanır.

---

## Backend kabul gate'i (madde 4 ↔ Sprint 16 tutarlılığı)

`DecideMembership.cs` `AcceptMembershipCommand` handler'ında, `Status=Active` yapılmadan **önce**:

- Kabul eden kullanıcının `EmailVerifiedAt == null` ise → **403** "Daveti kabul etmek için önce e-posta
  adresinizi doğrulayın." (Sprint 16'daki apply-gating 403 deseniyle aynı yaklaşım.)
- `RejectMembershipCommand` bu kontrolü **uygulamaz** (unverified kullanıcı daveti reddedebilir).

> Not: Bu, Sprint 16'nın "stüdyo üyeliğine girişte e-posta doğrulaması" prensibini davet kabulüne de taşır.

---

## Kapsam dışı (bilinçli)

- Gerçek marketplace/discovery; harita/yakındaki stüdyolar; öne çıkarma/reklam sistemi.
- Hesabı **olmayan** kişiye gerçek e-posta/SMS **davet linki** (mevcut "önce hesap aç" modeli korunur).
- Öğrenci tarafı discovery üzerinden başvuru modeli.
- Büyük refactor; pipeline sırası / tenant izolasyonu / routing guard mantığı **değişmez** (yalnız
  `/join-studio` içeriği zenginleşir).
- Commit/push (onaysız).

---

## Migration & Backfill

- **Şema migration'ı GEREKMEZ:** `NotificationType` string olarak saklandığından (`HasConversion<string>`,
  maxlen 32) yeni `MembershipInvite` değeri yalnız kod değişikliğidir; mevcut kayıtlar etkilenmez, yeni kolon
  yok. (Davet/membership/notification tablolarına alan eklenmez.)
- **Backfill yok.** Mevcut Pending davetler için geçmişe dönük bildirim üretilmez (yalnız sprint sonrası yeni
  davetlerde bildirim oluşur) — bilinçli, idempotent.

---

## Testler (sprint17-smoke + E2E)

1. **Instructor JoinCode apply → 400/422** (yeni kısıt).
2. **Student JoinCode apply → 200** (regresyon korunur).
3. **Owner/StudioAdmin mevcut bir kullanıcıyı hoca olarak davet eder → Pending invitation oluşur**
   (`Direction=InvitedByStudio`).
4. **Davet edilen kullanıcı `/join-studio` Davetlerim'de daveti görür** (`my-invitations` döner).
5. **Invite notification oluşur** (`Type=MembershipInvite`, doğru `RecipientUserId`, dedup key).
6. **Aynı invitation için duplicate notification oluşmaz** (dedup key tekilleştirir).
7. **Unverified kullanıcı invitation accept → 403** ("önce e-posta doğrulayın").
8. **Verified kullanıcı invitation accept → Active membership + InstructorProfile** (idempotent profil).
9. **Reject → Rejected; profil aktifleşmez**, kullanıcı stüdyosuz kalırsa `/join-studio`'da kalır.
10. **Kabul sonrası memberships reload olur; dashboard/select-studio akışı çalışır.**
11. **Bir kullanıcı başka kullanıcının davetini göremez** (tenant/recipient izolasyonu — `my-invitations`
    yalnız kendi davetlerini döner).

**Profil oluşturma zamanı (REV1):**

12. **Hoca davet edildiğinde membership Pending olur ama InstructorProfile OLUŞMAZ** (invite anında profil yok).
13. **Davet kabul edilince InstructorProfile oluşur** (accept-time, idempotent).
14. **Davet reddedilince InstructorProfile oluşmaz** (ne invite ne reject'te).
15. **Davet kabul edilmeden hoca instructor listelerinde/uygunluklarda (availability/schedule) aktif
    görünmez** (Pending membership + profilsiz).

**Üyeli kullanıcı yeni davet (REV1):**

16. **Zaten aktif stüdyo üyeliği olan kullanıcı yeni bir stüdyodan davet alır** → Pending invitation +
    `MembershipInvite` notification oluşur.
17. **Üyeli kullanıcı `/join-studio` (veya `?tab=invitations`) Davetlerim bölümünde bu daveti görebilir**
    (üyeli olduğu halde sayfadan geri atılmaz).
18. **Kabul edince ikinci stüdyoya da üye olur; mevcut select-studio/çoklu üyelik akışı çalışır.**
19. **Stüdyosuz kullanıcı aynı ekranı empty-state olarak görmeye devam eder** (routing guard ile otomatik
    yönlenir).

20. **Regresyon:** mevcut 17 smoke + E2E bozulmaz (özellikle Sprint 15 join-request + davet, Sprint 16
    auth/email verification, mevcut `/join-studio`).

---

## Sprint 17 Rapor Başlıkları (bittiğinde)

1. Genel Özet
2. Oluşturulan/Değiştirilen Dosyalar
3. Hoca Self-Apply Kısıtı (Student vs Instructor)
4. Davet Bildirimi (yeni `MembershipInvite` + dedup)
5. Davet Kabulünde E-posta Doğrulama Gate'i (403)
6. Frontend: "Davetlerim" Bölümü (kabul/ret)
7. Frontend: Stüdyosuz Kullanıcı Empty-State + Discovery Dokümantasyonu
8. Yetki & Tenant İzolasyonu
9. Test/Doğrulama Sonuçları
10. Bilinçli Yapılmayanlar (discovery/marketplace, e-posta davet linki)
11. Riskler/Borçlar
12. Sonraki Sprint / Faz-2 Önerisi (discovery)

---

## Doğrulama (spec onayı)

Bu turda kod yok. Spec doğru sayılır eğer: (a) 8 locked decision net; (b) hoca self-apply kısıtı + öğrenci
istisnası açık; (c) davet kabulünde e-posta doğrulama gate'i (403) tanımlı; (d) `MembershipInvite` bildirimi
+ dedup key + migration gerekmediği belirtilmiş; (e) "Davetlerim" `/join-studio` içinde (ayrı route yok);
(f) **InviteMember invite anında InstructorProfile oluşturmaz; profil yalnız accept'te oluşur (REV1)**;
(g) **`/join-studio` çift amaçlı — üyeli kullanıcı da davetini görebilir; bildirim `?tab=invitations`'a
derin linklenir (REV1)**; (h) stüdyosuz empty-state bilinçli + discovery yalnız dokümante; (i) existing
invite "önce hesap aç" davranışı korunur; (j) test listesi 20 maddeyi + regresyonu kapsar.
**Kodlama için ayrı onay beklenecek.**
