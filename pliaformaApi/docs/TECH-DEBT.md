# Teknik Borç Kaydı (Sprint 25 — pilot sınıflandırması)

Bu belge bilinen teknik borçları **pilot-blocker / non-blocker** olarak sınıflandırır. Pilot, küçük bir stüdyoyla
gerçek kullanım hedefidir; aşağıdaki non-blocker maddeler **bilinçli** ertelenmiştir ve pilotu engellemez.

> Genel durum: Sprint 25 itibarıyla **pilot-blocker teknik borç YOKTUR.** Tek gerçek pilot-blocker olan ders onayı
> yetki açığı (Instructor stüdyo-geneli confirm-today) bu sprintte düzeltildi.

## Pilot-blocker (düzeltildi)

| # | Borç | Durum |
|---|---|---|
| B1 | **Ders onayı yetki açığı:** `ConfirmSession`/`ConfirmToday` controller-geneli `Instructor` rolünü miras alıyordu → Instructor stüdyo-geneli bugünü onaylayabiliyordu. | ✅ **Sprint 25'te düzeltildi.** `confirm-today` artık Owner/StudioAdmin-only; `confirm` Instructor için yalnız planlanan/fiilî kendi dersi. |

## Non-blocker (bilinçli ertelenen — pilot OK)

| # | Borç | Sınıf | Gerekçe / azaltım |
|---|---|---|---|
| N1 | **Gerçek drag/drop takvim grid** | non-blocker | Mevcut modal-tabanlı erteleme/chain-shift/grup-düzeni akışı işlevsel; grid pilot sonrası UX yatırımı. |
| N2 | **ScheduleSeries entity** (seri görünürlük/seri-undo) | non-blocker | Grup "implicit" (paylaşılan booking'ler); operasyonlar sözleşme-bazlı çalışıyor. Migration gerektirir → ayrı sprint. |
| N3 | **Closure full-undo** (kapanış geri al) | non-blocker | Manuel telafi + tekil iptal/yeniden planlama mevcut; tam closure-undo otomasyonu ertelendi. |
| N4 | **Staff borç özeti multi-currency roll-up** | non-blocker | Pilot tek-currency (TRY). Staff `StudentDebtSummaryDto` tek-currency varsayımıyla toplar; **öğrenci tarafı per-currency güvenli** (tek cross-currency toplam yok). Çoklu-currency staff roll-up ileride. |
| N5 | **Dashboard/reports/package-plans `₺` sabit** | non-blocker | TRY varsayımı; pilot tek-currency için yeterli. studio.Currency'ye bağlama N4 ile birlikte ele alınacak. |
| N6 | **package/rate DB drop** | non-blocker | Paket/kredi + hoca ücreti alanları **dormant** (DB'de kalır, üründen çıktı). Drop riskli/gereksiz; migration gerektirir. |
| N7 | **Online ödeme / fatura / e-arşiv / muhasebe** | non-blocker | Manuel ödeme kaydı + borç takibi yeterli; gerçek tahsilat gateway + webhook ileride. |
| N8 | **Telafi (makeup) expiry zorlaması** | non-blocker | Ledger + opsiyonel `ExpiresAt` mevcut; otomatik expiry job ertelendi. |
| N9 | **Timezone / duvar-saati modeli** | non-blocker | ClassSession StartAt stüdyo-yerel duvar saati; tek-stüdyo pilot için yeterli; gece-yarısı aşan seans desteklenmez. |
| N10 | **CSP başlığı + TLS prod** | non-blocker | nginx güvenlik başlıkları mevcut; CSP (Next inline tema script'i nonce ister) + 443/TLS pilot-deploy dry-run'da dokümante, certbot adımı hazır. |
| N11 | **Öğrenci self-booking** | non-blocker (bilinçli kapalı) | Program admin-güdümlü; self-book/self-cancel uçları **410 Gone**. Bilinçli tasarım, borç değil. |
| N12 | **Backend HTTP healthcheck (compose)** | non-blocker | `restart: unless-stopped` + nginx retry; `/health` endpoint mevcut. |
| N13 | **Otomatik birim/entegrasyon testi (xUnit) yok** | non-blocker | Doğrulama 27 smoke + 56 E2E ile; pilot sonrası CI birim testi eklenebilir. |
| N14 | **Bildirim worker tek-instance** | non-blocker | Çok-instance'ta nadir dedup yarışı (DB index korur); pilot tek-instance. |
| N15 | **Installment (taksitli ödeme planı) ölü kod — Sprint 45 soft removal** | non-blocker | `ContractLinePaymentInstallment` entity/tablo/DbSet + `ContractLinePaymentPlan.cs` (DTO/rules/handler) **legacy olarak duruyor** ama üründen çıktı: API uçları 410 Gone, yeni line create plan almaz, UI plan göstermez. Borç/gecikme tek kaynak `StudentContractLine.DueDate` + line.Paid. `[Obsolete]` attribute kullanılmadı (TreatWarningsAsErrors=true); dosya başında LEGACY uyarı bloğu var. **Hard removal (DROP TABLE + entity/handler silme) ayrı tech-debt sprintine bırakıldı** (destructive migration riski). |

## Pilot sonrası öncelik önerisi
1. Gerçek bildirim kanalları (SMS/e-posta sağlayıcı) + worker tek-instance kilidi.
2. Gerçek abonelik/online ödeme tahsilatı (gateway + webhook + PastDue→suspend).
3. Multi-currency staff roll-up (N4/N5) — gerçek çoklu-currency ihtiyacı çıkarsa.
4. Timezone tam modeli (N9) + telafi expiry job (N8).
5. CSP (nonce) + 2FA + CI birim testi (N10/N13).
6. Drag/drop grid (N1) + ScheduleSeries (N2) — UX/ölçek yatırımı.
