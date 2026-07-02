# Pilot Runbook (operatör rehberi)

Pilot sırasında operatörün günlük işletme rehberi. Kurulum/deploy adımları için
[PILOT-DEPLOY-DRY-RUN.md](PILOT-DEPLOY-DRY-RUN.md) ve [PILOT-CHECKLIST.md](PILOT-CHECKLIST.md); yedek için
[BACKUP-RESTORE.md](BACKUP-RESTORE.md); bilinen borçlar için [TECH-DEBT.md](TECH-DEBT.md).

## 1. Pilot ortam kontrolü (go-live öncesi)
- [ ] `docker compose -f docker-compose.prod.yml ps` → postgres/backend/frontend/nginx **Up**.
- [ ] `curl -s http://localhost/health` → `Healthy` (nginx üzerinden backend).
- [ ] TLS açık (443) + HTTP→HTTPS redirect (certbot adımı dry-run §TLS).
- [ ] `.env` gerçek değerlerle dolu (JWT secret güçlü, DB parolası, SMTP varsa).
- [ ] PlatformAdmin hesabı kuruldu (SEED_ADMIN_* ya da manuel) ve giriş yapılabiliyor.
- [ ] İlk gerçek stüdyo Owner'ı oluşturuldu (register-studio) ve giriş doğrulandı.
- [ ] Yedek cron kuruldu (`ops/backup-postgres.sh`, günlük) + **bir kez restore tatbikatı yapıldı**.
- [ ] (Sprint 47) `PILOT_SIMPLE_PEOPLE_MODE` bilinçli karar verildi (bu pilot stüdyo sade-mod kullanacak mı?);
      `deploy/.env`'de doğru değerle set edildi (varsayılan `false`).

## 2. Demo veriyle tanıtım (opsiyonel — gerçek stüdyo öncesi sunum)
Pilot demosu için gerçekçi örnek veri:
```bash
bash pliaformaApi/scripts/pilot-demo-seed.sh http://localhost:5132   # (dev/compose backend BASE'i)
```
> ⚠️ **Yalnız dev/demo ortamı.** `pilot-demo-seed.sh` `demo-*` e-postalı veriyi kurar (e2e-* ve gerçek prod
> verisine dokunmaz; idempotent: önce kendi `demo-*` verisini siler). **Gerçek pilot prod DB'sinde çalıştırmayın.**

**Demo kullanıcıları** (parola hepsi `DemoPass123!`):
| Rol | E-posta |
|---|---|
| Owner | demo-owner@pliaforma.local |
| StudioAdmin | demo-admin@pliaforma.local |
| Instructor (Ayşe) | demo-ayse@pliaforma.local |
| Instructor (Mehmet) | demo-mehmet@pliaforma.local |
| Öğrenci (login'li) | demo-zeynep@pliaforma.local |

**Demo içeriği:** grup + bireysel sözleşme · Elif sözleşmesinde amendment (+4 ders +2000 TL) · Can 4000 TL borç /
Zeynep ödemesi tam · Burak telafi hakkı (devamsızlık) · fiilî hoca örneği · not-delivered örneği · closure örneği.

## 3. Rol bazlı demo senaryosu (≈15 dk anlatım)
1. **Owner** ile giriş → Dashboard (özet kartlar) → **Öğrenciler**: Can'ın "Ödemeler" → **Borç özeti** (4000 kalan),
   Elif'in "Sözleşmeler" → **Ek işlem geçmişi** (amendment).
2. **Ders Programı**: bir grup dersinde "Rezervasyonlar" / "Yoklama"; "Fiilî hoca" ve "İşlenmedi" aksiyonları;
   "Grup düzeni" (öğrenci taşıma) önizlemesi.
3. **Raporlar**: hoca raporu **parasız** (ders/öğrenci sayısı + hoca değişikliği).
4. **Instructor** (Ayşe) ile giriş → yalnız kendi programı; finans linkleri yok; **"Bugünü onayla" görünmez**;
   kendi dersinde "Onayla" var.
5. **Öğrenci** (Zeynep) ile giriş → **Sözleşmelerim** (ders hakkı + ödeme durumu, yumuşak dil) · **Telafi
   Kredilerim** · **Ödemelerim** (sözleşme adıyla) · **Programım**. Hiçbir yazma işlemi yok.
6. **PlatformAdmin** ile giriş → `/platform` stüdyo listesi/detay + suspend/reactivate + health.

## 4. Günlük sağlık kontrolleri
- `docker compose -f docker-compose.prod.yml logs -f backend` → JSON log; hata/exception tarama.
- `/platform/overview` → bekleyen/başarısız bildirim, gecikmiş ödeme sayıları.
- `/platform/health` → sistem durumu.
- Disk/DB boyutu + yedek log'u (`/var/log/pliaforma-backup.log`).
- Bildirim worker prod'da açık (300s); beklenen hatırlatmalar üretiliyor mu.
- **(Sprint 47) `/health` — harici bir uptime servisiyle veya elle günlük kontrol.** Pilot boyunca Prometheus/Grafana
  KULLANILMAYACAK (dev-only scrape hedefi, bilinçli karar — bkz. PILOT-GO-LIVE-CHECKLIST.md §4); `/health` + log
  taraması tek gözlem yüzeyidir.
- **(Sprint 47) Hatırlatmalar yalnız uygulama içidir.** E-posta/SMS/WhatsApp kanalı YOK (`LoggingNotificationSender`
  no-op); "hatırlatma gitmedi" şikayeti gelirse önce bunu hatırlat, bug değildir.

## 5. Yedek / geri yükleme (hatırlatma)
- Günlük cron: `ops/backup-postgres.sh /var/backups/pliaforma` (RETENTION_DAYS ile rotasyon).
- **Restore tatbikatı**: dump'ı `pliaforma_restore_test` geçici DB'ye yükle → tablo sayıları doğrula → DB'yi düşür.
- ⚠️ **`docker compose down -v` prod'da ASLA** — named volume `pgdata` silinir (veri kaybı). Sade `down` veri korur.
- Detay: [BACKUP-RESTORE.md](BACKUP-RESTORE.md).

## 6. Sorun triyajı
| Belirti | İlk kontrol | Aksiyon |
|---|---|---|
| Giriş yapılamıyor | backend up? `/health`? e-posta doğrulandı mı? | log; gerekiyorsa kullanıcı `EmailVerifiedAt` elle set. |
| "Stüdyonuz askıda" yazma engeli | `/platform/studios` durum | PlatformAdmin'den reactivate. |
| Bildirim gelmiyor | worker açık mı? `/platform/overview` failed sayısı | manuel `run-reminders` trigger; log incele. |
| 403/409/400 beklenmedik | rol/iş kuralı mı? | mesajı kullanıcıya açıkla; gerçek bug ise kaydet (aşağıdaki format). |
| Yavaşlık | DB boyutu, container CPU/mem | logları/metikleri incele; gerekirse restart. |

**Bug kaydı formatı:** rol · ekran/URL · yapılan işlem · beklenen vs gerçek · zaman · ekran görüntüsü ·
backend log parçası (varsa correlationId).

## 7. Rollback / deploy notları
- Deploy: `git pull` (root monorepo) → `docker compose -f docker-compose.prod.yml up -d --build`.
- **Rollback:** önceki etikete dön — `git checkout v0.13.0-pilot` → `up -d --build`. (DB migration geri alımı
  gerekiyorsa önce yedekten restore; bu sürümde Sprint 25 **migrationsızdır**.)
- Migration: bu sürüm pilot boyunca **şema değiştirmez** (Sprint 25 migration yok). Yeni sprint migration getirirse
  deploy öncesi yedek + `has-pending` kontrolü zorunlu.

## 8. Post-pilot adımları
- Geri bildirim toplama (rol bazlı kısa anket + birebir görüşme).
- Log/metrik analizi: en sık hata, en yavaş ekran, kullanılmayan özellik.
- [TECH-DEBT.md](TECH-DEBT.md) önceliklendirmesini gözden geçir (gerçek çoklu-currency, gerçek ödeme, bildirim
  kanalları ihtiyacı çıktı mı).
- Karar: sonraki stüdyoya genişle / düzeltme için duraklat / kapsam revize.
