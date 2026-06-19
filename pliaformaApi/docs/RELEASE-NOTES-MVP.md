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
- **Sözleşme-tabanlı program (Sprint 18+):** StudentContract program/booking'in **tek yetki kaynağı**; sözleşmeden
  çok-haftalık program üretimi (grup reuse + bireysel); Regular booking sözleşme zorunlu (paket gate kaldırıldı,
  paket modeli **dormant**); öğrenci self-booking kapalı (410).
- **Program operasyonları:** tek ders erteleme, **chain-shift** (zincirleme kaydırma, telafisiz), **grup düzeni
  değişikliği** (öğrenci tek tek taşıma, telafisiz, preview=dry-run, atomik), **fiilî hoca** (substitution, rapora
  fiilîye yazar), **not-delivered** (stüdyo-kaynaklı → telafi), ders onayı (Sprint 25: Instructor kendi dersi /
  confirm-today manager-only).
- **Sözleşme amendment & borç (Sprint 23–24):** ek ders/süre/ücret (append-only ledger, immutable); sözleşme-bazlı
  borç (hesaplanır, stored balance yok); öğrenci self-servis sözleşme/borç görünümü (salt-okunur, per-currency).
- **Ödeme:** manuel Payment (dinamik Overdue), opsiyonel sözleşmeye bağlama. PackagePlan/StudentPackage ledger
  **dormant** (DB'de kalır, üründen çıktı). **Manuel takip** (online tahsilat yok).
- **Dashboard & rapor:** rol bazlı read-only özetler, hoca raporu **parasız** (ders/öğrenci sayısı + hoca değişikliği).
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
- **Staff borç özeti tek-currency MVP varsayımı** (stüdyo tek para birimiyle çalışır); öğrenci tarafı per-currency
  güvenli. Çoklu-currency staff roll-up teknik borç ([TECH-DEBT.md](TECH-DEBT.md)).
- **Dashboard/reports/package-plans `₺` sabit** (TRY varsayımı); pilot tek-currency için yeterli.
- Otomatik birim/entegrasyon testi yok; doğrulama **smoke (27)** + Playwright **E2E (56)** ile.

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

## Sürüm geçmişi (etiketler)
Sprint 0–24 tamamlandı; en güncel etiket **v0.13.0-pilot** (Sprint 24 — öğrenci self-servis sözleşme/borç).
Sprint 25 pilot sertleştirme (UX/güvenlik polish + runbook + tech-debt) ayrı koşuda.

## Doğrulama durumu (bu sürüm)
`dotnet build` 0/0 · `next build` temiz · **migration yok / has-pending No changes** · **27 smoke** (sprint +
security-audit release-gate) ✅ · **Playwright E2E 56** ✅ · same-origin ✅ · full `docker compose up --build` ✅ ·
backup/restore döngüsü doğrulandı ✅. Detay: [TESTING.md](TESTING.md).
