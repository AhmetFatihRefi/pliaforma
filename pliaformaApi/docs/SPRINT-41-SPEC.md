# Sprint 41 — UX Safety & Feedback Standardization (SPEC)

> Tarih: 2026-06-26 · Kapsam: yalnız `pliaformaFrontend` (UI). Backend/migration YOK.
> Önkoşul: [UX-REVIEW.md](UX-REVIEW.md) raporu.

## 1. Sprint 41 Kapsam Özeti

Amaç: Kullanıcının yanlışlıkla riskli/geri-alınamaz işlem yapmasını **confirm standardı** ile engellemek; her kritik mutasyon sonrası **toast standardı** ile net geri bildirim vermek; prod'a sızan **jargon/ölü link** temizlemek; **buton label/variant** ve **tarih formatı** tutarsızlığını azaltmak; **payment çoklu para birimi** toplam hatasını UI'da düzeltmek.

İlke: Yeni altyapı yazılmaz; mevcut `useConfirm` (`ConfirmDialog.tsx`) ve `useToast` (`Toast.tsx`) kullanılır. Backend, migration, business logic, payment/debt hesap mantığı değişmez. Tüm uygulama değil, **kritik akışlar** alınır.

Kabul kriteri: Kapsamdaki her yıkıcı işlemde uygun-tone confirm; her kritik mutasyonda success toast / hata toast (veya inline alan hatası); kalıcı banner birikmesi yok; listelenen jargon/ölü link UI'dan kalkmış; `formatDate` util kritik finans/sözleşme/takvim yüzeylerinde kullanılıyor; payment özetleri currency'ye göre doğru.

## 2. Değişecek Dosya Tahmini

- `lib/format.ts` (YENİ)
- `students/[id]/page.tsx`, `students/_components.tsx`
- `instructors/page.tsx`
- `payments/page.tsx`
- `package-plans/page.tsx`, `class-types/page.tsx`, `equipment/page.tsx`
- `closures/page.tsx`, `schedule/page.tsx`
- `makeup-requests/page.tsx`, `makeup-credits/page.tsx`
- Jargon/link: `(platform)/layout.tsx`, `app/page.tsx`

## 3. Confirm Standardı

Mevcut `useConfirm({ title, message, confirmLabel, cancelLabel, tone })`. Mesaj mümkünse: (1) ne olacak, (2) kim/hangi kayıt etkilenir, (3) geri alınabilir mi, (4) finansal/kredi/hak etkisi. Geri-alınamaz/finansal/yetki/kredi etkili → `tone: "danger"`. "Ödendi" işaretleme `default`.

İşlem listesi: öğrenci pasifleştir/aktifleştir, hoca rol değişimi (danger), davet kabul (default)/red (danger)/çıkarma (danger), ödeme "Ödendi" (default), ödeme iptal (danger), paket planı sil (danger), ders tipi/ekipman sil (danger), closures uygula (danger, genel uyarı), schedule "Bugünü onayla" (danger), "İşlenmedi" (danger), rezervasyon iptali (danger), telafi onay/düzenleyerek-onayla (default; çakışmada uyarı), telafi reddet (danger).

## 4. Closures / Schedule toplu işlem

Backend dry-run YOK. Kayıt sayısı uydurulmaz. Genel ama açık uyarı: "Bu işlem seçili aralıktaki dersleri etkileyebilir…". Kesin etki/dry-run ayrı sprint.

## 5. Toast Standardı

`toast.success` / `toast.error(errorMessage(e, fallback))`. Form alanı doğrulaması → inline `FormField error` (toast değil). Kalıcı banner'lar (özellikle `makeup-requests` banner, sayfa-üstü error paragrafları) mümkünse toast'a; kalıcı sonuç panelleri (closures sonuç ekranı) toast'a zorlanmaz. İşlem öncesi eski mesaj temizlenir.

## 6. Jargon / Ölü Link Temizliği

- `(platform)/layout.tsx:63` "Sprint 10 · Platform Admin" → "Platform Yönetimi"
- `makeup-credits/page.tsx:286,288,297` sprint/uygulanmıyor notları → kaldır/nötrle
- `equipment/page.tsx:142` "sonraki aşamada devreye girecek" → kaldır (formül kalsın)
- `class-types/page.tsx:175` "sonraki aşamada gelecek" → hint sadeleştir
- `app/page.tsx:637-638` footer `href:"#"` → kaldır / link olmayan nötr metin (yanıltıcı "yakında" yok)
- Kod yorumlarındaki `// Sprint XX` dokunulmaz.

## 7. Buton Label / Variant Standardı

danger: Sil / İptal et / Pasifleştir / rol düşür / toplu iptal. "Vazgeç" = modal/form kapatma; "Kapat" = salt görüntüleme; "İptal et" = iş kaydı iptali; "Sil" = kalıcı silme (danger). Ghost+danger karışımı yok (package-plans ghost "Sil" → danger). class-types/equipment modal "İptal" → "Vazgeç".

## 8. formatDate Önerisi

`lib/format.ts`: `formatDate(iso)` ("26.06.2026"), `formatDateTime(iso)` ("26.06.2026 14:30"). null/boş → "—"; DateOnly + ISO güvenli; `Intl.DateTimeFormat("tr-TR")`; geçersizde ham string fallback. **Timezone semantiği değişmez.**
Uygulanacak: payments, students/_components (finans/sözleşme), students/[id], makeup-credits, makeup-requests, schedule detay/drawer. Geri kalan sonraki sprint.

## 9. Payment Currency Bug

Gerçek gösterim hatası (`payments/page.tsx:82-91`): özetler currency ayırmadan toplanıp `groups[0].currency` ile gösteriliyor. `PaymentDto.currency` ile UI grouping yapılır; tek-currency görünüm bozulmaz; çoklu-currency'de özetler ayrı gösterilir. Backend değişmez.

## 10. Riskler

confirm akış yavaşlatması (Ödendi default tone); closures kesin sayı verememe (genel uyarı); banner→toast'ta kalıcı sonuç panellerini koruma; currency grouping regresyonu (tek-currency testi); formatDate kapsam sürünmesi; sabit `tr-TR` locale.

## 11. Test Planı

`tsc --noEmit` 0; `next build` 0; backend değişmedi → `dotnet build` gereksiz. Manuel checklist: pasifleştir/rol/davet/ödeme onay+toast; çakışmalı telafi uyarısı; sil danger; closures/schedule toplu confirm; rezervasyon iptali confirm; success toast; banner birikmiyor; jargon/ölü link yok; payment tek/çoklu currency; kritik tarihler tr-TR.

## 12. Kapsam Dışı (sonraki sprintler)

Takvim redesign; ContractDetailModal→sayfa; sözleşme wizard; pagination/server-side; skeleton; öğrenci self-servis; bildirim deep-link; rapor export/grafik; nav ikon seti; AppShell rewrite; backend/migration; closures dry-run; formatDate tüm yüzeyler + timezone; auth iyileştirmeleri; settings/platform/health iyileştirmeleri.
