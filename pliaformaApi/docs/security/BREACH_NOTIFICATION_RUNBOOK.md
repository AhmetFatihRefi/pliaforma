# Veri İhlali Bildirim Runbook'u (72 Saat) — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Bağlı: [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md)

> **Bu bir hukuki metin taslağıdır; gönderim öncesi KVKK avukatı onayı zorunludur.** Şablonlardaki köşeli
> parantezli alanlar doldurulacaktır. Süre/eşik bilgileri Kurul'un yürürlükteki kararlarına göre avukatça teyit edilmelidir.

## 1. Ne zaman bildirim gerekir?

KVKK m.12/5: Kişisel verilerin **kanuni olmayan yollarla başkaları tarafından elde edilmesi** hâlinde, veri
sorumlusu durumu **öğrendiği tarihten itibaren en kısa sürede (Kurul kararıyla 72 saat)** Kurul'a bildirir.
İlgili kişiye de **makul en kısa sürede** bilgi verilir.

**Karar akışı:**
1. İhlal kişisel veri içeriyor mu? → Hayırsa SEV-3, bildirim gerekmeyebilir (avukat teyidi).
2. İçeriyorsa kategori? Özel nitelikli (sağlık/HealthNotes) varsa risk yüksek → öncelik.
3. Veri sorumlusu + avukat → Kurul bildirimi + ilgili kişi bildirimi kararı.

## 2. 72 saat sayacı
- **Başlangıç:** ihlalin **öğrenildiği** an (tahmin edilen sızma anı değil).
- Sayaç [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) §4 ile paralel ilerler.
- Hedef: T+48 saatte taslak hazır, T+72'den önce gönderim.

## 3. Kurul'a bildirim — gerekli bilgiler
Kurul'un "Kişisel Veri İhlal Bildirim Formu" doğrultusunda:
- Veri sorumlusu kimlik/iletişim: **[TBD — owner/işletme]**
- İhlalin gerçekleştiği/öğrenildiği tarih: **[...]**
- İhlalin kaynağı/nasıl gerçekleştiği: **[...]**
- Etkilenen kişi grupları ve **kişisel veri kategorileri**: **[kimlik / iletişim / finans / sağlık / ...]**
- Etkilenen kişi ve kayıt sayısı (tahmini): **[...]**
- Olası sonuçlar: **[...]**
- Alınan/önerilen tedbirler: **[kapsama + düzeltici aksiyonlar]**
- İrtibat kişisi: **[TBD]**

## 4. Şablon — Kurul bildirim metni (taslak)

```
KİŞİSEL VERİ İHLALİ BİLDİRİMİ

Veri sorumlusu:            [İşletme adı / ünvanı — TBD]
İrtibat kişisi:            [Ad Soyad / e-posta / telefon — TBD]
İhlal öğrenme tarihi:      [GG.AA.YYYY SS:DD]
İhlal tarihi (tahmini):    [...]
İhlalin niteliği:          [Yetkisiz erişim / sızıntı / kayıp / ...]
Nasıl gerçekleşti:         [Kısa teknik açıklama]
Etkilenen veri kategorileri: [Kimlik, iletişim, finansal, sağlık (özel nitelikli) ...]
Etkilenen kişi/kayıt sayısı: [Tahmini]
Olası sonuçlar:            [...]
Alınan tedbirler:          [JWT secret rotasyonu, parola sıfırlama, zafiyet kapatma, ...]
İlgili kişilere bildirim:  [Yapıldı/Planlandı — tarih/yöntem]
```

## 5. Şablon — İlgili kişi (öğrenci/kullanıcı) bildirim metni (taslak)

```
Konu: Kişisel verilerinizle ilgili önemli güvenlik bilgilendirmesi

Sayın [Ad Soyad],

[İşletme adı] olarak, [tarih] tarihinde sistemimizde bir güvenlik olayı tespit ettik. Yaptığımız
inceleme sonucunda [etkilenen veri kategorileri: ör. ad-soyad, e-posta, telefon] bilgilerinizin
[yetkisiz erişime maruz kalmış olabileceğini] tespit ettik.

Aldığımız önlemler:
- [Etkilenen oturumlar sonlandırıldı / parolanızı sıfırlamanızı rica ederiz]
- [Güvenlik açığı kapatıldı]
- [Yetkili mercilere bildirim yapıldı]

Sizden ricamız:
- Parolanızı değiştirin ve aynı parolayı başka yerlerde kullanmayın.
- Şüpheli e-posta/SMS'lere karşı dikkatli olun.

Sorularınız için: [iletişim — TBD]

Saygılarımızla,
[İşletme adı]
```

> Sağlık verisi (HealthNotes) etkilendiyse dil ve aciliyet avukatça ayrıca gözden geçirilmelidir.

## 6. Bildirim sonrası
- [ ] Tüm bildirimleri (Kurul + kişi) zaman damgalı arşivle (delil).
- [ ] [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) §6 kapanış raporunu tamamla.
- [ ] Düzeltici aksiyonları [RISK_REGISTER.md](RISK_REGISTER.md)'a işle.

## 7. Hızlı kontrol listesi (yazdırılabilir)
- [ ] Öğrenme anı kaydedildi (72 saat sayacı başladı)
- [ ] SEV-1 doğrulandı
- [ ] Delil korundu (audit/log/yedek kopyası + checksum)
- [ ] Kapsama yapıldı (secret rotasyon / token revoke / zafiyet yaması)
- [ ] Etki analizi (kategori + sayı + tenant)
- [ ] Avukat değerlendirmesi alındı
- [ ] Kurul bildirimi gönderildi (tarih: ____)
- [ ] İlgili kişiler bilgilendirildi (tarih: ____)
- [ ] Kapanış raporu + RCA tamamlandı
