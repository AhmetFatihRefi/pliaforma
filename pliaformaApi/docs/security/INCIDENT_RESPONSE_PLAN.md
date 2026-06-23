# İhlal / Olay Müdahale Planı (Incident Response Plan) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Bağlı: [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md)

Bu plan, kişisel veri ihlali veya güvenlik olayında izlenecek adımları tanımlar. Amaç: hızlı kapsama,
KVKK m.12 gereği **72 saat içinde Kurul'a bildirim** hazırlığı, delil koruma ve kök neden.

> İsim/iletişim/numara alanları **TBD — owner dolduracak.** Uydurma bilgi yazılmamıştır.

---

## 1. Roller ve iletişim (TBD)

| Rol | Görev | Kişi / İletişim |
|---|---|---|
| Olay koordinatörü | Süreci yönetir, karar verir | **TBD** |
| Teknik müdahale | Sistem inceleme, kapsama | **TBD** |
| Veri sorumlusu (işletme) | Kurul/ilgili kişi bildirimi kararı | **TBD** |
| KVKK avukatı | Hukuki değerlendirme | **TBD** |
| İletişim/PR | Kullanıcı bilgilendirme dili | **TBD** |

**Olay bildirim kanalı (internal):** TBD (örn. özel e-posta/■ telefon hattı).

---

## 2. Olay nasıl tespit edilir? (kaynaklar)

- **Sistem:** `/health` ve `/platform/health` anormallikleri; Prometheus/Grafana metrik sapması.
- **Loglar:** prod JSON log'da beklenmeyen 500 artışı, yetkisiz erişim denemeleri.
- **Audit log (P0-2 sonrası):** olağandışı login/failed-login, rol değişikliği, toplu export.
- **Dış sinyal:** kullanıcı şikayeti, sağlayıcı (hosting/SMTP) uyarısı, güvenlik araştırmacısı bildirimi.
- **Rate limit/429 artışı:** brute-force göstergesi.

> Audit log (P0-2) devreye girene kadar tespit yeteneği **kısıtlıdır** — bu, risk register'da P0 olarak işaretlidir.

---

## 3. Olay sınıflandırma

| Seviye | Tanım | Örnek |
|---|---|---|
| **SEV-1 (Kritik)** | Kişisel veri ihlali doğrulandı veya kuvvetle muhtemel | DB sızıntısı, yedek çalınması, yetkili hesap ele geçirme, cross-tenant veri sızması |
| **SEV-2 (Yüksek)** | Güvenlik açığı; veri ihlali henüz doğrulanmadı | Aktif istismar edilen zafiyet, secret sızıntısı |
| **SEV-3 (Orta/Düşük)** | Sınırlı etki, ihlal yok | Tekil hatalı yetki, başarısız saldırı denemesi |

SEV-1/SEV-2 → bu plan tam uygulanır + [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md).

---

## 4. Zaman çizelgesi

### İlk 1 saat — Tespit & triyaj
- [ ] Olayı koordinatöre bildir; SEV seviyesi ata.
- [ ] **Delili koru:** log/audit/yedek dosyalarını **değiştirmeden** kopyala; zaman damgalarını not et.
- [ ] Etki kapsamını ilk tahmin et (hangi tablo/tenant/kullanıcı?).

### İlk 24 saat — Kapsama & değerlendirme
- [ ] **Kapsama:** sızan kimlik bilgisini iptal et (JWT secret rotasyonu → tüm token geçersiz; etkilenen kullanıcı parola sıfırlama; refresh token revoke).
- [ ] Gerekirse erişimi geçici kısıtla / şüpheli IP engelle (nginx) / etkilenen hesabı pasifleştir.
- [ ] Zafiyeti kapat (yama/konfig).
- [ ] **Etki analizi:** hangi kişisel veri kategorileri (kimlik/iletişim/finans/**sağlık**), kaç kişi, hangi tenant.
- [ ] Veri sorumlusu + avukat ile **bildirim gerekliliği** kararı (KVKK m.12/5).

### 24–72 saat — Bildirim hazırlığı
- [ ] SEV-1 doğrulandıysa **Kurul bildirimi** hazırla ([BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md)) — *öğrenme anından itibaren en geç 72 saat.*
- [ ] İlgili kişilere bildirim metnini hazırla (makul en kısa süre).
- [ ] Tüm aksiyonları zaman damgalı kaydet (delil zinciri).

### Kapanış (72 saat sonrası)
- [ ] **Kök neden analizi** (RCA): nasıl oldu, hangi tedbir eksikti.
- [ ] Düzeltici aksiyonlar → [RISK_REGISTER.md](RISK_REGISTER.md)'a yeni/güncel maddeler.
- [ ] **Kapanış raporu** (aşağıdaki şablon).

---

## 5. Delil toplama kuralları
- Log/audit/yedekleri **orijinalini değiştirmeden** ayrı güvenli konuma kopyala (hash/checksum al).
- Zaman damgalarını UTC olarak kaydet.
- Audit log (P0-2) update/delete edilemez → birincil delil kaynağı olacak.
- Erişim/komut geçmişini koru (kim ne yaptı).

---

## 6. Kapanış raporu şablonu

```
Olay ID:                 INC-YYYY-NN
Tespit tarihi/saati (UTC):
Öğrenme anı (UTC):
SEV seviyesi:
Etkilenen veri kategorileri:   [kimlik / iletişim / finans / sağlık / ...]
Etkilenen kişi sayısı (tahmini):
Etkilenen tenant(lar):
Kök neden:
Kapsama aksiyonları:
Kurul'a bildirildi mi? (tarih):
İlgili kişilere bildirildi mi? (tarih/yöntem):
Düzeltici aksiyonlar (Risk ID):
Tekrarı önleme:
Hazırlayan / onaylayan:
```

---

## 7. Düzenli tatbikat
- **Tabletop drill** (masa başı senaryo provası): **Sprint 31D-3'te HAZIRLANDI** →
  [INCIDENT_TABLETOP_DRILL.md](INCIDENT_TABLETOP_DRILL.md). Senaryo: "Çalınan **şifreli** prod DB yedeği"
  (31D-2 backup encryption bağlamı: yedek gpg-şifreli + key ayrı → maruziyet düşük, ama olay ciddiye alınır + karar kaydı).
  Ekip bu planı uçtan uca yürütür; bulunan eksikler tarihli madde olarak [RISK_REGISTER.md](RISK_REGISTER.md)'a eklenir.
  **Düzenli tekrar** (öneri: yılda 1–2) operatör sorumluluğudur.
