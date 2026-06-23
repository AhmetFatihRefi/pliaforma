# KVKK Owner Soru Formu — Pliaforma

> **UYARI:** Bu form ve buradan üretilen tüm hukuki metinler **taslaktır**. **Avukat onayı olmadan yayınlanmaz.**
> Amaç: KVKK aydınlatma/gizlilik/çerez metinleri, DPA ve başvuru süreci için owner'dan ve avukattan netleştirilmesi gereken bilgileri toplamak.
>
> İlgili teknik dosyalar: [../security/DATA_INVENTORY.md](../security/DATA_INVENTORY.md), [../security/DATA_PROCESSORS.md](../security/DATA_PROCESSORS.md), [../security/RETENTION_AND_DELETION_POLICY.md](../security/RETENTION_AND_DELETION_POLICY.md), [../security/KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md](../security/KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md), [../security/INCIDENT_RESPONSE_PLAN.md](../security/INCIDENT_RESPONSE_PLAN.md)

---

## A. Owner Tarafından Verilen Kararlar (NET — 2026-06-23)

Bu kararlar metin taslaklarına işlendi:

| # | Konu | Karar |
|---|---|---|
| 1 | Online ödeme | **Şu an YOK** |
| 2 | Öğrenci ödemesi | Sistemde **yalnız manuel kayıt** (stüdyo girer) |
| 3 | Ödeme girişi | Hocalar / stüdyo yetkilileri ödeme aldıklarında **elle** girer |
| 4 | Komisyon | Pliaforma öğrenci ödemelerinden **komisyon almaz** |
| 5 | Platform geliri | İleride **Netflix benzeri abonelik paketleri** (stüdyodan) |
| 6 | Deneme süresi | **2 ay** ücretsiz deneme |
| 7 | Şirket türü | **Şahıs şirketi** |
| 8 | SMS/WhatsApp | **Şu an YOK**, ileride eklenecek |
| 9 | Pazarlama bildirimi | **Şu an YOK** |
| 10 | HealthNotes (sağlık verisi) | **Şu an KAPALI** (toplanmıyor) |
| 11 | Öğrenci–stüdyo bağı | Öğrenci **tek stüdyoya bağlı değil**; bir stüdyodan çıkıp başka stüdyoya girebilir |
| 12 | Hesap modeli | Kullanıcının **global Pliaforma hesabı** var; stüdyo üyeliği ayrı `StudioMembership` üzerinden |
| 13 | Tenant izolasyonu | Her stüdyo **yalnız kendi kapsamındaki** veriyi görür |

---

## B. Hâlâ Owner'dan Beklenen Bilgiler (TBD)

Aşağıdakiler metinlerde `[PLACEHOLDER]` olarak duruyor; doldurulması gerekiyor.

### B.1 Şirket / Veri Sorumlusu Kimliği
- [ ] Şahıs şirketinin **tam yasal unvanı** → `[ŞAHIS ŞİRKETİ UNVANI]`
- [ ] **T.C. / Vergi kimlik no** ve vergi dairesi
- [ ] **Resmi adres** → `[ADRES]`
- [ ] **KVKK başvuru e-postası** → `[KVKK BAŞVURU E-POSTASI]`
- [ ] **İletişim telefonu** → `[TELEFON]`
- [ ] (Varsa) **KEP adresi**

### B.2 Altyapı / Veri İşleyenler
- [ ] **Hosting / VPS sağlayıcısı** adı + ülke → `[HOSTING SAĞLAYICI]`
- [ ] **Domain / DNS sağlayıcısı** + alan adı → `[DOMAIN ADI]`
- [ ] **SMTP / e-posta sağlayıcısı** adı + ülke → `[SMTP SAĞLAYICI]` *(prod'da gerçek SMTP zorunlu)*
- [ ] **Yedek (backup) saklama lokasyonu** + ülke → `[BACKUP LOKASYONU]`
- [ ] Tüm sağlayıcılar için **DPA imzalı mı?**
- [ ] Sağlayıcılar **yurt dışında mı?** (yurt dışı aktarım değerlendirmesi için)

### B.3 İleriye Dönük (Şu An Kapalı) Özellikler
- [ ] SMS/WhatsApp eklenince sağlayıcı → `[SMS/WHATSAPP SAĞLAYICI - ŞU AN YOK]`
- [ ] Online ödeme eklenince sağlayıcı → `[ÖDEME SAĞLAYICI - ŞU AN YOK]`
- [ ] Analytics/pixel eklenecek mi? (şu an yok)

### B.4 Saklama / Silme
- [ ] **Mali kayıt** (ödeme/sözleşme) yasal saklama süresi (mali müşavir teyidi)
- [ ] Öğrenci ilişkisi bitince veri saklama beklentisi
- [ ] Stüdyo platformdan ayrılırsa veri saklama/silme beklentisi

### B.5 Incident / Sorumluluk
- [ ] **Olay koordinatörü** (ad/iletişim)
- [ ] **Teknik müdahale sorumlusu**
- [ ] **Kurul'a bildirimi yapacak kişi** (veri sorumlusu adına)
- [ ] **KVKK avukatı/danışmanı**
- [ ] İç ihlal bildirim kanalı

### B.6 Resmi / Hukuki Statü
- [ ] **VERBİS durumu** → `[VERBİS DURUMU]` (kayıt var mı / gerekli mi)
- [ ] Pilotta kaç stüdyo / kaç öğrenci (gerçek veri mi test mi)

---

## C. Avukata Sorulacak Kararlar (Hukuki)

1. **Rol ayrımı:** Stüdyonun öğrenci/hoca verisinde stüdyo = veri sorumlusu, Pliaforma = veri işleyen kurgusu uygun mu? Platform hesabı/güvenlik/abonelik verisinde Pliaforma ayrıca veri sorumlusu mu?
2. **Aydınlatma yeri:** Platform aydınlatması (global kayıt anı) + stüdyo bilgilendirmesi (davet/katılım anı) ikili yapı yeterli mi?
3. **Hukuki sebepler:** Kimlik/iletişim/ödeme için "sözleşmenin ifası" + "hukuki yükümlülük" + "meşru menfaat" dağılımı; açık rıza yalnız sağlık/pazarlama için — doğru mu?
4. **Mali saklama:** Ödeme/sözleşme kayıtlarının saklama süresi ve silme talepleriyle çakışmasının çözümü.
5. **Audit log:** `SecurityAuditLog` 2 yıl + IP/UserAgent saklamanın hukuki sebebi (meşru menfaat) yeterli mi?
6. **Misafir öğrenci:** Hesapsız (UserId=null) öğrenci kaydında aydınlatma nasıl yapılmalı (kişi sistemde yokken stüdyo onun adına kayıt açıyor)?
7. **Üçüncü kişi verisi:** Acil durum iletişim kişisi (`EmergencyContactName/Phone`) ve doğum tarihi (`BirthDate`) işlemenin hukuki sebebi.
8. **Çoklu stüdyo üyeliği:** Bir kullanıcının birden çok stüdyoya katılması; bir stüdyodan ayrılınca verinin akıbeti (anonimleştirme vs. mali saklama).
9. **Yurt dışı aktarım:** Hosting/SMTP yurt dışındaysa hangi mekanizma.
10. **Başvuru süreci:** İlgili kişi başvurusunun 30 gün, kimlik doğrulama, stüdyo–platform arası yönlendirme kurgusu.
11. **İhlal bildirimi:** 72 saatlik sayacın başlangıcı ve bildirim eşiği ([BREACH_NOTIFICATION_RUNBOOK](../security/BREACH_NOTIFICATION_RUNBOOK.md) taslak hazır).
12. **VERBİS:** Şahıs şirketi için kayıt yükümlülüğü.
13. **Açık rıza ayrımı:** Aydınlatma metni ile açık rızanın ayrı tutulması (rıza opt-in, ayrı onay).

---

## D. Çıktı Dosyaları

- [LEGAL-DOCS-DRAFT-PACKAGE.md](LEGAL-DOCS-DRAFT-PACKAGE.md) — tüm metin taslakları bu formdaki kararlara göre üretildi.

---

*Bu analiz/forma dayalı metinler hukuki görüş değildir; avukat kontrolü gerekir.*
