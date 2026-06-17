# Pliaforma — MVP Pilot Sürüm Notları

Pilates stüdyoları için multi-tenant SaaS. Bu sürüm **pilot/MVP**'dir: küçük bir stüdyoyla gerçek kullanım için
yeterli; bazı kurumsal özellikler **bilinçli olarak** ertelenmiştir (aşağıda). .NET 10 (Clean Architecture +
özel CQRS) + Next.js 16 + PostgreSQL; same-origin (nginx) dağıtım.

## Roller
- **PlatformAdmin** (global): tüm stüdyoları yönetir (`/platform`); stüdyo askıya alma/aktifleştirme, abonelik
  takibi (tahsilat yok), sistem sağlığı. Tenant verisine yalnız `/api/platform/*` üzerinden erişir.
- **Owner / StudioAdmin** (stüdyo): stüdyonun her şeyini yönetir.
- **Instructor** (stüdyo): yalnız kendi programı/öğrencileri/yoklaması; finansal tutarları görmez.
- **Student** (stüdyo): yalnız kendi rezervasyon/paket/ödeme/yoklama/bildirimleri.

## Ana modüller (MVP'de var)
- **Kimlik & üyelik:** global User + StudioMembership (davet/başvuru/kabul), çoklu stüdyo, JWT (kısa access +
  httpOnly refresh), rate limit.
- **Hoca & grup:** InstructorProfile + ücret, ClassType taksonomisi, hoca↔grup atama, müsaitlik.
- **Öğrenci:** misafir + User'a dönüştürme (Id korunur), sağlık notu maskeleme.
- **Takvim & rezervasyon:** ClassSession (tek/bulk), atomik kapasite (race-safe), çift-rezervasyon koruması,
  iptal/geri-açma.
- **Erteleme & telafi:** reschedule, toplu kapanış, **telafi kredisi ledger**, makeup seans tüketimi.
- **Yoklama:** Present/Absent/Excused/LateCancel, opsiyonel telafi kredisi, aylık özet.
- **Paket & ödeme:** PackagePlan, StudentPackage (ledger), manuel Payment (dinamik Overdue), yoklama→paket düşüm
  (atomik, idempotent). **Manuel takip** (online tahsilat yok).
- **Dashboard & rapor:** rol bazlı read-only özetler, hoca tahmini kazancı, ödeme/hoca raporu (finans gizliliği).
- **Bildirim:** ders/ödeme hatırlatma (idempotent worker + manuel trigger), in-app bildirim merkezi + zil.
  Kanallar **InApp/Log** (gerçek SMS/WhatsApp/e-posta YOK).
- **Platform admin:** stüdyo listesi/detay, suspend/reactivate, abonelik iskeleti, overview/health.

## Bilinçli eksikler (MVP kapsamı dışı)
Gerçek SMS/WhatsApp/e-posta/push · gerçek online ödeme & abonelik tahsilatı & fatura/e-arşiv · recurring booking ·
Excel/PDF export · kampanya/kupon · bekleme listesi · otomatik geç-iptal/no-show cezası · paket otomatik expiry
job · gelişmiş finans/muhasebe raporu · 2FA/hesap kilitleme · gelişmiş metrik (Prometheus) · multi-instance
distributed lock · CSP başlığı.

## Bilinen kısıtlar (known limitations)
- **TLS pilot-öncesi açılmalı** (nginx 443 taslak); Secure refresh cookie HTTPS ister.
- **Timezone:** ClassSession StartAt stüdyo-yerel duvar saati (UTC dönüşümü ertelendi); gece yarısını aşan seans
  desteklenmez; gün-sınırı edge'leri stüdyo TZ'sine bağlı.
- **Paket expiry:** süresi geçmiş paket tüketimde reddedilir ama DB `Status` otomatik `Expired` olmaz (job yok);
  `effectiveStatus` ile gösterilir.
- **Bildirim worker** tek-instance varsayar; çok-instance'ta nadir dedup yarışı (DB index korur).
- **Bildirim teslimi** yalnız in-app/log; "gönderildi" gerçek bir dış teslim anlamına gelmez.
- **Backend HTTP healthcheck yok** (aspnet imajında araç yok); resilience `restart` + nginx retry ile.
- Otomatik birim/entegrasyon testi yok; doğrulama smoke (13) + Playwright E2E (25) ile.

## Pilotta test edilmesi gerekenler
- Her rolün giriş/yönlendirme akışı + günlük kullanım (rezervasyon, yoklama, paket/ödeme kaydı).
- Bildirimlerin beklenen anda üretimi (worker prod'da açık) + in-app okuma.
- Stüdyo suspend/reactivate'in kullanıcı deneyimine etkisi (yazma engeli mesajı ileride iyileştirilebilir).
- Yedek alma + **geri yükleme** (felaket tatbikatı) en az bir kez.
- Mobil kullanım (stüdyo personeli telefon/tablet).
- TLS sonrası login + refresh cookie davranışı.

## Sonraki sprint önerileri
1. **Gerçek bildirim kanalları** (SMS/e-posta sağlayıcı + sender registry + worker tek-instance kilidi).
2. **Gerçek abonelik/online ödeme tahsilatı** (gateway + webhook + otomatik PastDue→suspend).
3. **Güvenlik sertleştirme +**: CSP (nonce), 2FA, hesap kilitleme, bağımlılık tarama gate (CI).
4. **Timezone tam modeli** + paket expiry job + recurring booking.

## Doğrulama durumu (bu sürüm)
`dotnet build` 0/0 · `npm run build` temiz · **13 smoke** (12 sprint + security) ✅ · **Playwright E2E 25/25**
(2x deterministic + prod compose build) ✅ · same-origin ✅ · full `docker compose up --build` ✅ · backup/restore
döngüsü doğrulandı ✅. Detay: [TESTING.md](TESTING.md).
