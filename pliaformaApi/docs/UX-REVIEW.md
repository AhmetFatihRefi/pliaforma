# Pliaforma — UX/UI Değerlendirme Raporu

> Tarih: 2026-06-26 · Kapsam: `pliaformaFrontend` (Next.js 16, App Router, Tailwind 4, React 19)
> Yöntem: Tasarım sistemi + landing + dashboard elle okundu; ~35 sayfa 6 paralel ajanla derinlemesine tarandı. Bu rapor analiz amaçlıdır; kodda değişiklik yapılmamıştır.

## Genel UX Değerlendirmesi

**Güçlü taraflar:**
- **Tasarım sistemi temeli sağlam.** `Modal.tsx` gerçek focus-trap + odak geri verme; `ConfirmDialog.tsx` promise-tabanlı `useConfirm`; `Toast.tsx`; `Select.tsx` (klavye, type-ahead, portal popover, auto-flip) profesyonel seviyede.
- **Tema token'ları tutarlı** (`globals.css`): blush/rose paleti, light/dark, reduced-motion, tema-uyumlu scrollbar.
- **Landing page** (`app/page.tsx`) cilalı: net hiyerarşi, gerçek SVG ikonlar, FAQ.
- **Referans kalite sayfalar:** `equipment` (toast + bağ koruması + canlı önizleme), `payments` (confirm + accordion), `_payment-plan.tsx` (canlı önizleme), `subscription` (danger-tone onaylı iptal).

**Zayıf taraflar (en çok zarar verenler):**
1. **Tutarlılık parçalanmış** — feedback (toast/banner/yok), buton etiketi/varyantı, sayfa genişliği rastgele; bazı yerlerde ham `<select>`/`<input>` özel `Select` varken kullanılıyor.
2. **Yıkıcı işlemlerde güvenlik eşitsiz** — `useConfirm` var ama tutarsız uygulanıyor; onaysız toplu iptal, rol değişimi, "Ödendi", pasifleştirme mevcut. **En yüksek risk.**
3. **İkonografi tutarsız** — nav cryptic dingbat glyph'ler (`▦ ◷ ⌖ ☺ ♺`), landing gerçek SVG.
4. **Loading/skeleton yok** — her refetch tüm içeriği "Yükleniyor…"a düşürüyor (flash).
5. **Ölçeklenebilirlik yok** — hiçbir liste sayfalanmıyor/sıralanmıyor.
6. **Öğrenci self-servis fiilen yok** — panel salt-okuma + iptal; sayfa adları aksini vaat ediyor.
7. **Tarih formatı her yerde ham ISO** (`iso.slice(0,10)`), para `tr-TR` formatlı — tutarsız + timezone riski.
8. **İç sprint jargonu prod'a sızıyor** ("Sprint 10", "Sprint 20: artık kullanılmıyor", "sonraki aşamada gelecek").

---

## Sayfa Sayfa İnceleme

### Landing / `/` (`app/page.tsx`)
- **Durum:** En cilalı sayfa.
- **Sorun:** Footer'da "İletişim/Yardım Merkezi" `href: "#"` ölü linkler (app/page.tsx:637-638).
- **Çözüm:** Gerçek rotaya bağla ya da kaldır. **Öncelik: Düşük.**

### Dashboard / `/dashboard`
- **Sorun:** Ham `<input type="month">` (özel Select varken); Stat kartları drill-down değil ("Gecikmiş 3 ödeme" tıklanamıyor); loading sadece metin.
- **Çözüm:** Kartları filtreli sayfaya link; ay seçiciyi Select'e taşı; skeleton. **Öncelik: Orta.**

### Header / Sidebar (global)
- **Sorun:** Stüdyo değiştirici ham `<select>` (Header.tsx:46); nav ikonları cryptic glyph (nav-items.ts:17-37); 16'ya kadar nav öğesi gruplanmamış.
- **Çözüm:** SVG ikon seti; nav bölümleme; switcher'ı Select'e taşı. **Öncelik: Orta.**

### Ders Programı / `/dashboard/schedule` (+ timetable, detail-drawer)
- **Sorunlar:** Zaman ekseni yok (kartlar liste gibi); 7 günde yatay scroll (sütun 208px); "hafta" modeli yanıltıcı (kayan 8 gün, "Bu hafta" bugüne snap); `confirmToday`/`markNotDelivered` önizlemesiz toplu yıkıcı işlem (schedule/page.tsx:289-306); boş hücre daima 09:00 öneriyor; drawer→modal bağlam kaybı; hafta navigasyonunda tüm takvim flash; drawer'da not yazılıp kapanınca uyarısız kayboluyor.
- **Çözüm:** Saat-eksenli görünüm; sütun min-width düşür; hafta modelini netleştir; toplu işlemlere önizlemeli onay; boş hücre saatini bağlamla; skeleton. **Öncelik: Kritik.**

### Boşluk Bul / `/dashboard/find-availability`
- **Sorun:** Schedule "Boşlukları Göster" ile çiftlenmiş; "uygun aralık" iki yüzeyde farklı gösteriliyor; ilk uygun hocayı otomatik seçiyor; 31 gün limiti uyarılmıyor.
- **Çözüm:** Birleştir/rolleri ayır; etiketi eşitle; hoca seçtir. **Öncelik: Orta.**

### Ders Rezervasyonu / `/dashboard/book-classes`
- **Sorun:** Self-booking kaldırılmış ama route + başlık duruyor.
- **Çözüm:** `redirect("/dashboard/my-bookings")`; menüden kaldır. **Öncelik: Düşük.**

### Öğrenciler / `/dashboard/students`
- **Sorun:** Sayfalama/sıralama/filtre yok; `isActive` listede görünmüyor; "+ Misafir ekle" kafa karıştırıcı; çift link.
- **Çözüm:** Pagination + sıralanabilir başlık + segment filtre; satır tıklanabilir; "+ Yeni öğrenci". **Öncelik: Yüksek.**

### Öğrenci Detay / `/dashboard/students/[id]`
- **Sorun:** IA parçalı (Ödemeler/Katılım modal, Sözleşmeler inline); "Pasifleştir" onaysız (page.tsx:76-85); aksiyonlar ayrımsız; özet yok; tarih ham.
- **Çözüm:** Tab yapısı; pasifleştirmeye confirm; özet şeridi; birincil aksiyon `primary`. **Öncelik: Yüksek.**

### Yeni Sözleşme / `/dashboard/students/[id]/contracts/new`
- **Sorun:** ~14 alan dağınık; validasyon submit anında ve en altta; kritik slot adımı altta gizli; para birimi serbest metin; bitiş kör +60 gün; dirty uyarısı yok.
- **Çözüm:** Sticky özet kartı; alanları grupla; slot'u vurgula; para birimi Select; inline validasyon. **Öncelik: Yüksek.**

### Sözleşme Yönet — `ContractDetailModal` (`students/_components.tsx`)
- **Sorun:** Tek "lg" modalda 6 iş akışı; paylaşılan tek `busy` tüm butonları kilitliyor; 5 ayrı mesaj kanalı; "tercih kaydet → yeniden planla" sırası sadece metinle anlatılıyor.
- **Çözüm:** Modal'ı detay sayfasına taşı; sekmeler; aksiyon başına busy; tek toast. **Öncelik: Kritik.**

### Finans/Ödeme Modalı (`students/_components.tsx`)
- **Sorun:** "Ödendi"/"İptal" onaysız; form başlıksız; "atanmamış ödemeler" kriptik.
- **Çözüm:** confirm (İptal danger); başlıklı form + label. **Öncelik: Yüksek.**

### Telafi Kredileri / `/dashboard/makeup-credits`
- **Sorun:** Aramada loading yok, hata "bulunamadı"ya dönüyor; "Geri al" disabled mantığı gerçek koşulla uyuşmuyor; grant/revoke sonrası feedback yok; politika kartında sprint jargonu (makeup-credits/page.tsx:286,288,297); hareket tablosu mobilde taşıyor.
- **Çözüm:** Spinner + gerçek hata; disabled koşulunu hizala; toast; jargonu kaldır; `overflow-x-auto`. **Öncelik: Yüksek.**

### Telafi Talepleri / `/dashboard/makeup-requests`
- **Sorun:** Çakışma uyarısı onay metnine yansımıyor (page.tsx:58-71); başarı banner'ı auto-dismiss değil, birikiyor; "Düzenle" aslında "düzenleyerek onayla"; arama/pagination yok.
- **Çözüm:** Çakışmada onayı engelle/uyar; banner→toast; etiket netleştir. **Öncelik: Yüksek.**

### Ödemeler / `/dashboard/payments`
- **Sorun:** "Ödendi" confirm'siz ve geri-alınamaz (markPaid'de toast var ama confirm yok); çoklu para birimi yanlış toplanıyor (page.tsx:82-91, `groups[0].currency`); arama/pagination yok; geciken vade ham ISO.
- **Çözüm:** "Ödendi"ye confirm; currency'ye göre grupla; isim araması; overdue vurgu + gecikme günü. **Öncelik: Yüksek.**

### Raporlar / `/dashboard/reports`
- **Sorun:** Export yok; grafik yok; sadece tek ay; özet kartları tıklanamıyor; metrikler "duvar metni"; tek para birimi varsayımı.
- **Çözüm:** CSV export; tarih aralığı; bar/donut; tıklanabilir kartlar; tooltip. **Öncelik: Yüksek.**

### Hocalar / `/dashboard/instructors`
- **Sorun:** Ham `<select>` ile inline rol değişimi anında/onaysız/geri-alınamaz (page.tsx:196-204); davet dropdown İngilizce rol değerleri; Kabul/Reddet onaysız; StudioAdmin engeli açıklanmıyor.
- **Çözüm:** Rol değişimi Select + confirm; `studioRoleLabels`; reddet onayı; toast. **Öncelik: Yüksek.**

### Pilates Grupları / `/dashboard/class-types`
- **Sorun:** Toast yok (equipment'ta var); süre `NaN` riski; "Kapasite kontrolü sonraki aşamada gelecek" jargonu (page.tsx:175).
- **Çözüm:** Toast; validasyon; jargonu gizle. **Öncelik: Orta.**

### Ekipman / `/dashboard/equipment` (referans sayfa)
- **Sorun:** Bağlı ekipmanda "Sil" tıklanınca toast ile reddediliyor (baştan disabled olmalı); "sonraki aşamada devreye girecek" jargonu (page.tsx:142); 0 adet'e izin.
- **Öncelik: Düşük.**

### Kapalı Günler / `/dashboard/closures`
- **Sorun:** Onaysız toplu yıkıcı işlem (page.tsx:108-129) — "Uygula" tek tıkla onlarca seansı iptal/erteliyor; dry-run/geri-al yok; `endAt < startAt` validasyonu yok.
- **Çözüm:** confirm + dry-run önizleme; tarih validasyonu. **Öncelik: Kritik.**

### Paket Planları / `/dashboard/package-plans`
- **Sorun:** "Sil" `variant="ghost"` (page.tsx:93); gelecek fiyat kısmi doldurulursa sessizce yutuluyor; boş kapsamlı plan kaydedilebiliyor; toast yok.
- **Çözüm:** Sil→danger; gelecek-fiyat validasyonu; boş kapsamı engelle; toast. **Öncelik: Yüksek.**

### Ayarlar / `/dashboard/settings`
- **Sorun:** Hata olunca yarım sayfa (settings/page.tsx:227); iki ayrı kaydet butonu → sessiz değişiklik kaybı; tz/currency serbest metin; toplu saat butonlarında geri-al yok.
- **Çözüm:** Kartları ayır; tz/currency Select; dirty uyarısı. **Öncelik: Yüksek.**

### Aboneliğim / `/dashboard/subscription`
- **Sorun:** `d == null` boş durumu yok; yükseltme yolu yok; iptal butonu `secondary` ama işlem yıkıcı.
- **Öncelik: Orta.**

### Öğrenci Sayfaları (my-*)
- **my-bookings:** Öğrenci rezervasyon yapamıyor (yalnız iptal); başlık aksini vaat ediyor; iptal onayında kredi sonucu söylenmiyor. **Kritik.**
- **my-contracts:** 5 jargon-ağırlıklı metrik eşit; "Kalan hak" vurgulanmamış; açıklama `title` (mobilde görünmez). **Yüksek.**
- **my-makeup-credits:** Mobilde tablo taşıyor; bakiye tekrar; "telafiyi stüdyo planlar" gizli. **Yüksek.**
- **my-attendance:** Ay değişince tüm sayfa "Yükleniyor"a düşüyor; liste seçilen ayla filtreli değil. **Yüksek.**
- **my-payments:** Özet/toplam yok ("ne kadar borcum var?" cevapsız); gecikenler öne çıkmıyor. **Yüksek.**
- **my-packages:** "Paketlerim" ↔ "Sözleşmelerim" kavramsal çakışıyor (ikisi de kalan ders hakkı). **Yüksek.**
- **my-profile:** Başlık "Profilim" ama hiç düzenleme yok; guide "güncelleyin" diyor → çelişki. **Yüksek.**
- **notifications:** Okundu/okunmadı ayrımı çok zayıf; sayı/filtre yok; bildirimler tıklanamıyor/deep-link yok. **Yüksek.**
- **guide:** Gerçek sayfalarla çelişiyor ("Profilim", "Programım"); ikon karışımı. **Yüksek.**

### Platform (süper-admin)
- **layout:** "Sprint 10 · Platform Admin" prod'da görünür (layout.tsx:63). **Orta.**
- **platform (genel bakış):** 14 metrik başlıksız; anomali renkle vurgulanmıyor; kartlar drill-down değil. **Yüksek.**
- **studios:** Sıralama + pagination yok (security-audit'te var — tutarsız). **Yüksek.**
- **studios/[id]:** Askıya al onaylı ama "aktifleştir" onaysız; iki ayrı hata bölgesi; "(bu ay)" başlığı yanıltıcı. **Yüksek.**
- **plans:** Default plan tekilliği denetlenmiyor; 11 alan gruplanmamış. **Yüksek.**
- **health:** Auto-refresh yok; başarısız bildirim/DB sorunu renkle vurgulanmıyor. **Yüksek.**
- **security-audit:** Aktör/Stüdyo filtreleri GUID istiyor (pratikte kullanılamaz); mobil fallback yok. **Yüksek.**

### Auth Akışları
- Parola görünürlük toggle'ı hiçbir yerde yok. **Yüksek.**
- check-email "Doğruladım, devam et" doğrulamadan ilerletiyor (page.tsx:31). **Yüksek.**
- MFA kurtarma kodları kopyala/indir yok + onaysız geçiş (page.tsx:62-74). **Yüksek.**
- join-studio davet yüklemede loading yok + hata yutuluyor. **Yüksek.**
- select-studio `switchStudio` catch yok + boş durum çıkmazı. **Yüksek.**
- login/mfa challenge ekranlarında geri/çıkış yok. **Yüksek.**
- Parola kuralı tutarsız (register 8 / register-studio 10 / reset 8). **Orta.**

---

## En Kritik 10 UX Problemi

1. Onaysız/önizlemesiz toplu yıkıcı işlemler (closures, schedule `confirmToday`/`notDelivered`). **(Kritik)**
2. Onaysız inline rol değişimi (instructors). **(Kritik/Güvenlik)**
3. `ContractDetailModal` mimari hatası (6 iş akışı tek modalda). **(Kritik)**
4. Takvimde zaman ekseni yokluğu + 7 günde yatay scroll. **(Kritik)**
5. Öğrenci self-servis yok; sayfa adları/guide çelişiyor. **(Kritik/Yüksek)**
6. Finansal onay eksik + çoklu para birimi yanlış toplanıyor. **(Yüksek)**
7. Tutarlılık dağınıklığı (feedback, buton, genişlik, ham select/input). **(Yüksek)**
8. Loading/skeleton yok; refetch flash'ı. **(Yüksek)**
9. Ölçeklenebilirlik (pagination/sıralama yok). **(Yüksek)**
10. Bildirimler eyleme dönüşmüyor + okundu ayrımı zayıf + dashboard drill-down yok. **(Yüksek)**

## Hızlı Kazanımlar

- Tüm yıkıcı işlemlere mevcut `useConfirm`'i uygula.
- Tüm mutasyonlara `useToast` ekle; banner'ları toast'a çevir.
- İç jargonu kaldır; landing ölü linkleri düzelt.
- Tek `formatDate(iso)` util'i (`Intl.DateTimeFormat("tr-TR")`).
- Ham `<select>`/`<input type=month>` → mevcut `Select`.
- Tablolara mobil `overflow-x-auto` (Table bileşeni zaten yapıyor; bypass eden sayfaları düzelt).
- Buton etiketi/varyant standardı ("Vazgeç", "Sil = danger").
- package-plans gelecek-fiyat/boş-kapsam validasyonu.
- Nav ikonlarını gerçek SVG seti.

## Daha Büyük Tasarım/UX İyileştirmeleri

- Takvim yeniden tasarımı (saat ekseni + responsive).
- Öğrenci/sözleşme detayını sayfa + sekme mimarisine taşı.
- Yeni sözleşme'yi wizard + sticky özet.
- Ortak server-side pagination + sıralama + filtre bileşeni.
- Öğrenci self-servis kararı (ekle ya da isim/guide hizala).
- Raporlama (tarih aralığı + export + grafik).
- Dashboard & platform anomali-vurgulu, drill-down'lı yeniden düzen.
- Skeleton bileşen ailesi.
- Tek ikon sistemi + nav gruplaması.

## Mobil UX Notları

- Takvim mobilde boş günleri tamamen gizliyor (masaüstüyle tutarsız).
- Geniş tablolar (makeup-credits, makeup-requests, my-makeup-credits, security-audit, reports-ödeme) `overflow-x-auto` kullanmıyor.
- `title` attribute açıklamaları (my-contracts, reports) mobilde hiç görünmüyor → popover gerekli.
- Tap target'lar küçük (takvim "+" 24px, fontlar 9-11px).
- Drawer mobilde neredeyse tam ekran; bottom-sheet daha uygun.

## Uygulama Sırası

**1. Önce (güvenlik + güven, düşük efor):** Yıkıcı işlemlere confirm + mutasyonlara toast; closures/schedule önizlemeli onay; payments currency + "Ödendi" onayı; jargon/ölü link temizliği; auth hızlı düzeltmeleri.

**2. Sonra (tutarlılık + ölçek):** `formatDate` + ham select/input→Select; buton/varyant/genişlik standardı; ikon seti; pagination/sıralama/skeleton altyapısı; öğrenci detay & sözleşme yönet → sayfa+sekme; öğrenci paneli isim/guide hizalaması; settings kart ayrımı.

**3. En son (kapsamlı redesign):** Takvim saat-eksenli; sözleşme wizard; raporlama; dashboard/platform yeniden düzen; öğrenci self-servis.
