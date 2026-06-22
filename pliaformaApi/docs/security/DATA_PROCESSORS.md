# Veri İşleyenler / Alt-İşleyenler Listesi — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Doldurma sorumlusu: **owner**

KVKK kapsamında **veri sorumlusu** (işletme) adına kişisel veri işleyen üçüncü taraflar burada listelenir.
Her gerçek sağlayıcı için **veri işleyen sözleşmesi (DPA)** ve gerekirse **yurt dışı aktarım** değerlendirmesi
yapılmalıdır (avukat onayı).

> **Önemli:** Aşağıda kod/konfigürasyondan **doğrulanabilen** bilgiler yazılmıştır. Doğrulanamayanlar
> **TBD — owner to confirm** olarak bırakılmıştır. **Uydurma sağlayıcı adı yazılmamıştır.**

---

## 1. Veri işleyen kategorileri

| # | Kategori | Sağlayıcı | İşlenen veri | Konum (ülke) | DPA | Durum |
|---|---|---|---|---|---|---|
| 1 | **Hosting / VPS** | **TBD — owner to confirm** | Tüm uygulama + PostgreSQL verisi (tüm PII) | **TBD** | **TBD** | Prod sunucu sağlayıcısı; en kritik işleyen |
| 2 | **Domain / DNS** | **TBD — owner to confirm** | Alan adı, DNS kayıtları (doğrudan PII değil) | **TBD** | **TBD** | TLS/erişim için kritik |
| 3 | **E-posta / SMTP** | **TBD — owner to confirm** | Alıcı e-posta + e-posta içeriği (doğrulama/parola sıfırlama linkleri, bildirim) | **TBD** | **TBD** | Kodda SMTP modu var (prod'da zorunlu); sağlayıcı env ile gelir |
| 4 | **SMS** | **TBD / Yok** | — | — | — | `NotificationChannel.Sms` enum var ama **gerçek gönderim YAPILMIYOR**; sağlayıcı yok. Eklenirse doldurulacak |
| 5 | **Ödeme sağlayıcı** | **TBD / Yok** | — | — | — | Kodda **gerçek ödeme entegrasyonu yok**; ödeme tutarları manuel kayıt. Gateway eklenirse doldurulacak |
| 6 | **Analytics** | **Yok (doğrulandı)** | — | — | — | Frontend'de analytics SDK bulunmadı; yalnız fonksiyonel localStorage. Eklenirse çerez metni + DPA gerekir |
| 7 | **Monitoring (Prometheus/Grafana)** | **Self-hosted (doğrulandı)** | Operasyonel metrikler (PII içermez; HTTP süre/sayı, runtime) | Hosting ile aynı | İç | `docker-compose.monitoring.yml`; prod'da erişim kısıtlanmalı (P0-3) |
| 8 | **Backup storage** | **TBD — owner to confirm** | PostgreSQL yedeği (TÜM PII) | **TBD** | **TBD** | Şu an `ops/backup-postgres.sh` lokal `.sql.gz`; off-site sağlayıcı kullanılırsa kritik + şifreleme (P1-7) |
| 9 | **Error tracking / logging** | **Yok (doğrulandı)** | — | — | — | Harici error tracking (Sentry vb.) bulunmadı; log self-hosted JSON console. Eklenirse PII kaçağına dikkat |

---

## 2. Doğrulanan bulgular (kod/konfig)
- **SMS / Ödeme / Analytics / Error-tracking:** kodda **aktif harici entegrasyon bulunmadı.** SMS ve ödeme yalnız
  enum/manuel kayıt düzeyinde. → bu kategorilerde şu an veri işleyen **yok**; eklenince güncellenecek.
- **Monitoring & logging:** self-hosted (Prometheus/Grafana + JSON console). PII'siz metrik.
- **E-posta:** SMTP sağlayıcısı prod'da **zorunlu** (env ile gelir) → gerçek bir veri işleyendir; **sağlayıcı adı TBD.**
- **Hosting & backup:** üçüncü taraf VPS/storage kullanılıyorsa **en kritik işleyenler**; ad/ülke **TBD.**

## 3. Yurt dışı aktarım
- Herhangi bir sağlayıcı (hosting/SMTP/backup) **yurt dışında** ise KVKK m.9 yurt dışı aktarım kuralları
  uygulanır (açık rıza veya yeterlilik/taahhütname). **Değerlendirme avukatça yapılacak (TBD).**

## 4. Owner için doldurma checklist
- [ ] Hosting/VPS sağlayıcı adı + ülke + DPA linki
- [ ] Domain/DNS sağlayıcı
- [ ] SMTP sağlayıcı adı + ülke + DPA
- [ ] (Varsa) SMS sağlayıcı
- [ ] (Varsa) Ödeme sağlayıcı
- [ ] (Varsa) Analytics sağlayıcı → çerez metni
- [ ] Backup storage sağlayıcı + ülke + şifreleme
- [ ] (Varsa) Error tracking sağlayıcı
- [ ] Her sağlayıcı için DPA imzalı mı?
- [ ] Yurt dışı aktarım değerlendirmesi (avukat)
