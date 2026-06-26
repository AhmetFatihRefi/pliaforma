# Sprint 42 — Historical Lesson Entry / Past Session Backfill (SPEC)

> Tarih: 2026-06-26 · Yalnız manuel, bilinçli backfill. Normal akış geçmişe otomatik üretim YAPMAZ.
> Karar: Bölüm-2 öğrenci ekleme **(A) dahil**. Self-booking geçmişe izin vermez. Update/reschedule/bulk/replan geçmişe gitmez.

## 1. Mevcut Past-Date Guard Analizi
- Create: `SessionRules.EnsureValidAsync` → `if (startAt < nowLocal) throw "Geçmiş bir zamana ders oluşturulamaz."` (SessionRules.cs:128-129). Tek tekil-create engeli.
- Update saat değişimi: aynı `EnsureValidAsync` (SessionMutations.cs:63). **Değişmeyecek** (geçmişe taşıma yasak kalır).
- Reschedule/chain: `EnsureMovableAsync` (SessionRules.cs:95-96). **Değişmeyecek.**
- Booking: `BookingCreator` "Geçmiş seansa rezervasyon yapılamaz." (BookingCreator.cs:54-55). Ayrı engel.
- Bulk/contract-generate/replan: ileriye üretir; **dokunulmaz**.
- Yetki: Create endpoint zaten `[Authorize(Roles="Owner,StudioAdmin")]` → Instructor 403.
- Confirm/attendance: past guard yok; geçmiş seans Scheduled oluşur, yoklama mevcut akışla yönetilir.

## 2. Backend — Create Override
- `CreateClassSessionCommand` + `bool AllowPastSession = false` (default false, geriye uyumlu).
- `SessionRules.EnsureValidAsync(..., bool allowPast = false)` → **yalnız** `startAt < nowLocal` kontrolü allowPast true ise atlanır. Çakışma + pending-telafi her durumda.
- Çalışma saatleri (`EnsureWithin`) ve ekipman guard (`EnsureCanReserveAsync`) zaten `EnsureValidAsync` DIŞINDA → override etkilemez, korunur.
- Otomatik attendance/confirm/present ÜRETİLMEZ — ders normal Scheduled oluşur.

## 3. Backend — Past Booking Override (A)
- `BookingCreator.CreateAsync(..., bool allowPast = false)` → **yalnız** "Geçmiş seansa rezervasyon yapılamaz." guard'ı allowPast true ise atlanır.
- Staff add-student komutu (Owner/StudioAdmin) `allowPastBooking` taşır → CreateAsync'e `allowPast: true` geçer (yalnız onaylıysa).
- Self-booking yolu **her zaman false** (geçmiş rezervasyon yapamaz).
- Korunan guard'lar: contract line uygunluk + tarih aralığı, kalan hak/booked count, öğrenci çakışması, kapasite, bireysel/grup tercihi, aktif kayıt, Scheduled-only. Hesap motoru değişmez.

## 4. Yetki
- Owner/StudioAdmin: geçmiş ders create + geçmiş seansa öğrenci ekleme.
- Instructor: create 403 (mevcut). Past booking override yalnız staff akışı.
- Self-booking: past override almaz.

## 5. İş Kuralları
Sadece confirm ile aşılan: `startAt < nowLocal` (create) ve "geçmiş seansa rezervasyon" (booking). Korunan: tenant/studio, yetki, çalışma saatleri, kapalı gün, hoca çakışması, ekipman kapasite, pending makeup çakışması, line uygunluk/tarih, kapasite/bireysellik, aktif/pasif, öğrenci çakışması.

## 6. Frontend — Create UX
- CreateSessionModal: `startAt` geçmişse inline warning; submit'te danger confirm; onayda `allowPastSession:true`. İptalde istek gitmez. Bugün/gelecek: warning/confirm yok.
- Warning: "Geçmiş tarihli ders oluşturuyorsunuz. Bu ders raporlara, katılım durumuna ve öğrencilerin ders haklarına etki edebilir."
- Confirm: başlık "Geçmiş tarihli ders oluşturulsun mu?" · mesaj "Geçmiş tarihli ders oluşturuyorsunuz. Bu işlem katılım, ders hakkı ve raporları etkileyebilir. Verileri geriye dönük doldurmak için kullanılmalıdır." · onay "Evet, geçmiş ders oluştur" · vazgeç "Vazgeç".

## 7. Frontend — Past Booking UX
- StaffBooking akışı: hedef seans geçmişse, ekleme öncesi danger confirm; onayda `allowPastBooking:true`. İptalde istek gitmez. Bugün/gelecek normal.
- Confirm: başlık "Geçmiş derse öğrenci eklensin mi?" · mesaj "Bu öğrenci geçmiş tarihli bir derse eklenecek. Bu işlem ders hakkı, katılım ve raporları etkileyebilir. Verileri geriye dönük doldurmak için kullanılmalıdır." · onay "Evet, öğrenci ekle" · vazgeç "Vazgeç".

## 8. Audit
Mevcut `ISecurityAuditLogger.LogAsync(...)` kullanılır (ChangeMemberRole vb. ile aynı). Geçmiş ders create + geçmiş booking için best-effort log; `SecurityAuditEventType`'a migration gerektirmeyen enum üyeleri eklenir (`PastSessionCreated`, `PastBookingCreated`) — gerekirse. Scope büyürse minimum `detail` log ile bırakılır; aksi halde raporda belirtilir.

## 9. Riskler
Ders hakkı: geçmiş booking mevcut tüketim mantığını çalıştırır (uyarı verilir). Attendance: geçmiş seans confirm'de işaretsizler Present sayılır (mevcut kural). Rapor: geçmiş ay metrikleri değişir. Yanlış veri: confirm+warning+audit ile azaltılır. TZ: UI yerel saatle "geçmiş mi" der; otorite backend stüdyo-yerel nowLocal.

## 10. Test Planı
Backend: dotnet build 0; has-pending clean; allowPastSession false→red, true→oluşur; future/today normal; Instructor 403; çalışma saati dışı geçmiş→red; hoca/ekipman çakışması geçmişte→red. Booking: allowPastBooking false→red, true→eklenir; self-booking geçmiş→red; contract/kapasite/çakışma korunur. Frontend: tsc 0; next build 0; geçmiş warning; confirm iptal→istek yok; onay→flag true; future normal; past booking confirm; today/future booking normal.

## 11. Kapsam Dışı
Toplu/Excel/CSV import; geçmişe program/replan/bulk üretme; update/reschedule geçmişe taşıma; attendance/takvim redesign; ders hakkı/payment/debt motoru; büyük API refactor; migration (zorunlu değilse).
