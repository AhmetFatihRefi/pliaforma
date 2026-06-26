# Sprint 40 — UX Foundation / Mobile Navigation / Accessibility Polish

**Durum:** Onaylandı (frontend-only).
**İlke:** Yalnız frontend sunum/erişilebilirlik katmanı. Business logic, API contract, veri modeli ve backend davranışı **değişmez**. Backend'e dokunulmadığı için **migration yok**.

## Kapsam (ALINACAK)

1. **Mobil drawer / hamburger navigasyon** — dashboard ve platform tarafında `<lg` ekranlarda menü erişilebilir.
2. **Sidebar/nav ortaklaştırması** — nav verisi + aktif-rota helper paylaşılır (full AppShell rewrite YOK).
3. **Modal erişilebilirliği** — focus trap, ilk-odak, focus restore, `aria-labelledby`; Escape korunur. ConfirmDialog'a dokunulmaz.
4. **Button/Input** — görünür `focus-visible` ring (Select kalitesiyle uyumlu, token'lı).
5. **Türkçe enum/status/role label haritaları** — yalnız UI metni; API değerleri sabit; bilinmeyen enum'da raw fallback.
6. **Bayat metin** — find-availability'deki "Çalışma penceresi 07:00–23:00" kaldırılır.

## Kapsam dışı (BU SPRINTTE YOK)

Pagination / server-side list refactor · loading/skeleton sistemi · toast standardizasyonu · AppShell full rewrite · ikon seti değişimi · native select/checkbox temizliği · finansal confirm redesign · şifre politikası değişimi · backend değişikliği · migration.

## Teknik yaklaşım

### Mobil drawer
- `components/layout/MobileNav.tsx` (yeni): hamburger (`lg:hidden`) + soldan slide-in drawer + overlay.
- Kapanma: **Escape**, **dış tıklama**, **X**, **rota değişimi** (pathname effect). Açıkken **body scroll lock**.
- `components/layout/nav-items.ts` (yeni): `NavItem` tipi, `dashboardNav`, `dashboardGuide`, `platformNav`, `MANAGER/STAFF` rol setleri, `filterNavByRole()`, `isNavActive()`.
- Dashboard: hamburger Header'a mount; rol-filtreli liste + rehber (+ varsa platform-admin linki) drawer'a verilir.
- Platform: hamburger platform header'a mount; `platformNav` verilir.

### Aktif nav
- `isNavActive(pathname, href)`: `/dashboard` ve `/platform` için **tam eşleşme**; diğerleri için `pathname === href || pathname.startsWith(href + "/")` (yanlış parent eşleşmesi engellenir). Hem masaüstü hem mobilde kullanılır.

### Modal focus trap
- Açılışta `document.activeElement` saklanır → kapanışta geri verilir.
- Açılınca ilk odaklanılabilir öğeye (yoksa `tabIndex=-1` dialog'a) odak.
- `keydown` (capture) ile Tab/Shift+Tab dialog içinde döner; Escape korunur.
- `<h3>` başlığa `useId` id + dialog'a `aria-labelledby`.
- **Modal + Select uyumu:** Select popover'ı portal ile `body`'ye render edildiğinden, popover'a `data-select-popover` işareti eklenir; trap, odak bu işaretin içindeyse Tab'ı **kendine almaz** (Select kendi yönetir, Tab'da kapanır).

### Button/Input ring
- `focus:outline-none focus-visible:ring-2 focus-visible:ring-ring/40` (+ Button'da `focus-visible:ring-offset-2 ring-offset-background` → renkli zeminde görünür). Token: `--ring`. `focus-visible` → fare kullanıcısı etkilenmez.

### Türkçe label haritaları (`lib/domain-types.ts`)
- `studioRoleLabels`: Owner→Sahip, StudioAdmin→Stüdyo Yöneticisi, Instructor→Eğitmen, Student→Öğrenci
- `membershipStatusLabels`: Pending→Bekliyor, Active→Aktif, Rejected→Reddedildi, Removed→Çıkarıldı
- `globalRoleLabels`: Standard→Standart, PlatformAdmin→Platform Yöneticisi
- `securityAuditOutcomeLabels`: Success→Başarılı, Failure→Başarısız
- `securityAuditEventTypeLabels`: olay tipleri Türkçe + **bilinmeyen anahtarda raw fallback**.
- Uygulanan ekranlar: Header (stüdyo seçici + rol satırı), app/page.tsx, dashboard/page.tsx, instructors (rol/durum), security-audit (olay/sonuç).
- **API'ye giden value'lar değişmez** (örn. audit filtresi ham enum gönderir; yalnız görünen metin Türkçeleşir).

### Bayat metin
- `find-availability/page.tsx`: "Çalışma penceresi 07:00–23:00." → "Sistem, stüdyonun çalışma saatlerine göre uygun başlangıçları gösterir."
- `settings/page.tsx` fallback metni (henüz ayarlanmadıysa 07:00–23:00) bağlamsal olarak doğru → kalır.

## Riskler
- Modal içi Select + focus trap (en kritik) → `data-select-popover` ile mitige; özel test.
- Global `:focus-visible` outline ile ring çiftlenmesi → bileşende `focus:outline-none`.
- Aktif-rota değişimi → index tam-eşleşme helper'ı.
- Body scroll lock çiftlenmesi (drawer+modal) → her biri kendi önceki değerini geri yükler.
- domain-types ↔ types.ts dairesel import → gerekirse label'lar types.ts'e taşınır.

## Test planı
- `tsc --noEmit` 0 · `next build` 0 · backend değişmedi (dotnet build gerekmez).
- Mobil: dashboard/platform drawer aç-kapat, aktif nav doğru, Escape/dış-tık/X/rota ile kapanış, body scroll lock.
- Klavye: Button/Input ring, Modal'a odak gelir, Tab/Shift+Tab içeride kalır, kapanınca odak döner, Modal içi Select çalışır.
- UI regression: login, dashboard, students, schedule, payments, settings, platform/security-audit.
- Label: roller/durumlar/Success-Failure/audit olayları Türkçe veya güvenli fallback; API enum değerleri sabit.
