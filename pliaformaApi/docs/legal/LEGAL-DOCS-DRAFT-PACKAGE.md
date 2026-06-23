# Pliaforma — Hukuki Metin Taslak Paketi (KVKK)

> # ⚠️ BU METİNLER TASLAKTIR — AVUKAT ONAYI OLMADAN YAYINLANMAZ.
>
> Bu paket, [KVKK-OWNER-QUESTIONNAIRE.md](KVKK-OWNER-QUESTIONNAIRE.md) içindeki owner kararlarına ve repodaki teknik gerçeklere ([../security/DATA_INVENTORY.md](../security/DATA_INVENTORY.md), [../security/DATA_PROCESSORS.md](../security/DATA_PROCESSORS.md)) dayanır.
> Tüm `[KÖŞELİ PARANTEZ]` ifadeleri **doldurulacak placeholder**'dır.
> Aydınlatma metni ile **açık rıza ayrıdır**; rıza ileride ve yalnız gerekli yerlerde alınır (bkz. Bölüm 7).

İçindekiler:
1. Pliaforma Platform KVKK Aydınlatma Metni Taslağı
2. Stüdyo Davet/Katılım Bilgilendirme Metni Taslağı
3. Kullanım Şartları Taslağı
4. Stüdyo ↔ Pliaforma Veri İşleme Sözleşmesi (DPA) Taslağı
5. Minimal Çerez Politikası Taslağı
6. Veri Sahibi Başvuru Süreci Taslağı
7. Açık Rıza — Koşullu Başlıklar (tam metin yazılmadı)
8. Eksik Bilgiler / TBD Listesi
9. HealthNotes Notu (sağlık verisi)

---

## 1. Pliaforma Platform KVKK Aydınlatma Metni Taslağı

**[ŞAHIS ŞİRKETİ UNVANI]** ("Pliaforma", "veri sorumlusu") olarak, 6698 sayılı Kişisel Verilerin Korunması Kanunu ("KVKK") kapsamında kişisel verilerinizi aşağıda açıklanan amaçlar ve hukuki sebeplerle işliyoruz.

### 1.1 Veri Sorumlusu
- **Unvan:** [ŞAHIS ŞİRKETİ UNVANI]
- **Adres:** [ADRES]
- **KVKK Başvuru E-postası:** [KVKK BAŞVURU E-POSTASI]
- **Telefon:** [TELEFON]
- **VERBİS:** [VERBİS DURUMU]

### 1.2 Bu Metnin Kapsamı
Bu aydınlatma metni, Pliaforma platformunda **global kullanıcı hesabınızla** ilgili veri işleme faaliyetlerini kapsar:
- Hesap oluşturma (kayıt) ve giriş (login)
- E-posta doğrulama
- Şifre sıfırlama
- Çok faktörlü kimlik doğrulama (MFA)
- Güvenlik ve denetim (audit) kayıtları
- Stüdyo oluşturma (stüdyo sahibi iseniz)
- Bir stüdyoya üyelik başvurusu / davet kabulü
- Platform aboneliği (stüdyo sahibi iseniz)

> Bir stüdyoya katıldığınızda, o **stüdyonun** kendi faaliyeti için işlediği verilere ilişkin ayrıca bilgilendirme yapılır (bkz. Bölüm 2). Stüdyo, kendi öğrenci/hoca verileri bakımından bağımsız bir veri sorumlusudur; Pliaforma bu verileri stüdyo adına işleyen konumundadır.

### 1.3 İşlenen Kişisel Veriler

| Kategori | Veriler | Kaynak |
|---|---|---|
| Kimlik/İletişim | Ad-soyad, e-posta, telefon | Sizden (kayıt) |
| Hesap güvenliği | Şifre (geri döndürülemez şekilde **hash**lenir; düz metin saklanmaz), giriş zamanı, başarısız giriş sayısı, geçici kilit durumu | Sistem |
| MFA | TOTP doğrulayıcı gizli anahtarı (**şifreli** saklanır), tek kullanımlık kurtarma kodları (**hash**lenir) | Sizden/sistem |
| Oturum | Oturum yenileme ve doğrulama token'ları (**hash**lenir), oluşturma IP'si, tarayıcı bilgisi (User-Agent) | Sistem |
| Güvenlik/denetim | Olay türü, sonuç, **maskelenmiş** e-posta, IP, tarayıcı bilgisi, zaman | Sistem (otomatik) |
| Stüdyo (sahibi iseniz) | Stüdyo adı, iletişim e-postası/telefonu, adres, şehir | Sizden |
| Abonelik (sahibi iseniz) | Abonelik planı, dönem, ücret, ödeme kaydı | Sistem |

### 1.4 İşleme Amaçları
- Hesabınızın oluşturulması ve yönetimi, kimliğinizin doğrulanması
- Giriş güvenliği, MFA ve yetkisiz erişimin önlenmesi
- E-posta doğrulama ve şifre sıfırlama işlemleri
- Platform güvenliğinin sağlanması, kötüye kullanımın tespiti ve **hesap verebilirlik** (denetim kayıtları)
- Stüdyo oluşturma ve stüdyo üyeliklerinin yönetimi
- Platform aboneliğinin ve (ileride) ücretlendirmenin yürütülmesi
- Hukuki yükümlülüklerin yerine getirilmesi

### 1.5 Hukuki Sebepler (KVKK m.5) — *avukat onayına tabi*
- **Sözleşmenin kurulması/ifası (m.5/2-c):** hesap, üyelik, abonelik
- **Hukuki yükümlülük (m.5/2-ç):** mali/yasal kayıtlar
- **Meşru menfaat (m.5/2-f):** güvenlik, denetim logları, kötüye kullanımın önlenmesi
- **Açık rıza (m.6):** yalnız ileride gerekirse (sağlık verisi, ticari ileti — bkz. Bölüm 7)

### 1.6 Aktarım
Kişisel verileriniz; barındırma, e-posta gönderimi ve yedekleme gibi hizmetler için yetkili **veri işleyenlere** aktarılabilir:
- Hosting: [HOSTING SAĞLAYICI]
- E-posta (SMTP): [SMTP SAĞLAYICI]
- Yedek saklama: [BACKUP LOKASYONU]
- *(Şu an yok)* SMS/WhatsApp: [SMS/WHATSAPP SAĞLAYICI - ŞU AN YOK]
- *(Şu an yok)* Ödeme: [ÖDEME SAĞLAYICI - ŞU AN YOK]

Yurt dışına aktarım söz konusu ise KVKK m.9 kapsamında değerlendirilir. *(Sağlayıcı ülkeleri TBD — avukat onayı.)*

### 1.7 Saklama Süreleri
- Hesap verileri: ilişki (hesap aktif) süresince + yasal tampon
- Oturum/doğrulama token'ları: süresi dolduktan kısa süre sonra **kalıcı olarak silinir** (otomatik temizlik)
- Güvenlik/denetim kayıtları: **2 yıl** (değiştirilemez/append-only)
- Mali/abonelik kayıtları: ilgili mevzuat süresi *(TBD — mali müşavir/avukat)*

### 1.8 Haklarınız (KVKK m.11)
Bilgi talebi, düzeltme, silme/yok etme, işlemeye itiraz ve diğer haklarınız için [KVKK BAŞVURU E-POSTASI] üzerinden başvurabilirsiniz (bkz. Bölüm 6).

---

## 2. Stüdyo Davet/Katılım Bilgilendirme Metni Taslağı

> Gösterim yeri: kullanıcı bir stüdyoya **davet edildiğinde** veya **join code ile başvurduğunda** / katılımı kabul ederken (kısa, ekran içi metin).

**Bir stüdyoya katılıyorsunuz.**

Pliaforma hesabınız size aittir ve birden fazla stüdyoya katılabilirsiniz. Bir stüdyoya katıldığınızda, **yalnızca o stüdyonun yetkilileri** aşağıdaki bilgilerinizi görebilir:

- **Ad-soyad, e-posta, telefon** (hesap bilgilerinizden)
- O stüdyo kapsamındaki **üyelik, ders/rezervasyon, yoklama, paket/ders hakkı, sözleşme ve ödeme** kayıtlarınız

Önemli noktalar:
- Bu bilgiler **ilgili stüdyonun veri sorumluluğunda** işlenir; stüdyo bu verileri kendi faaliyeti (ders planlama, devam takibi, tahsilat) için kullanır.
- **Stüdyolar birbirlerinin verilerini göremez.** Her stüdyo yalnızca kendi kapsamındaki verilere erişir.
- Başka bir stüdyoya da katılabilir veya bir stüdyodan ayrılabilirsiniz.
- **Stüdyo kapsamındaki verilerinizle** ilgili talepleriniz (görme, düzeltme, ayrılma, silme) öncelikle **ilgili stüdyoya** yönlendirilir; **platform hesabınızla** ilgili talepleri Pliaforma değerlendirir (bkz. başvuru süreci).
- Bu ekrandaki bilgilendirme, stüdyonun ayrıntılı aydınlatma yükümlülüğünün yerine geçmez; stüdyonun kendi aydınlatma metni ayrıca geçerlidir.

> **Not (sağlık verisi):** Bu platformda sağlık/sakatlık vb. özel nitelikli veri toplanması **kapalıdır**. Lütfen serbest not alanlarına sağlık bilgisi girmeyiniz.

---

## 3. Kullanım Şartları Taslağı

**Son güncelleme: [TARİH] — TASLAK**

### 3.1 Hizmetin Tanımı
Pliaforma, stüdyoların (pilates/fitness vb.) öğrenci, ders, rezervasyon, yoklama, paket/sözleşme ve ödeme kayıtlarını yönetmesini sağlayan bir **SaaS (hizmet olarak yazılım)** stüdyo yönetim platformudur.

### 3.2 Hesaplar ve Roller
- Kullanıcılar **global bir Pliaforma hesabı** açar.
- Stüdyo üyelikleri ayrı kayıtlar (`StudioMembership`) üzerinden yürür; bir kullanıcı **birden fazla stüdyoya** katılabilir.
- Roller: **Owner** (stüdyo sahibi), **StudioAdmin** (stüdyo yöneticisi), **Instructor** (eğitmen), **Student** (öğrenci); ayrıca platform genelinde **PlatformAdmin**.
- **Studio Owner/Admin**, stüdyo adına kayıt olur ve stüdyoyu yönetir.

### 3.3 Yetki Sınırları ve İzolasyon
- Her stüdyo yalnızca **kendi kapsamındaki** verileri görür; stüdyolar arası erişim engellenir.
- Eğitmenler finansal detayları (paket fiyatı, ödeme tutarı, eğitmen ücreti) göremez; öğrenciler yalnızca kendi verilerine erişir.

### 3.4 Ücretlendirme ve Deneme
- Yeni stüdyolara **2 (iki) ay ücretsiz deneme** süresi tanınır.
- İleride **abonelik paketleri** (Netflix benzeri, periyodik ücretli) sunulacaktır.
- **Online ödeme şu anda mevcut değildir.**
- **Öğrenci ödemeleri** Pliaforma üzerinden tahsil edilmez; stüdyo tarafından **manuel olarak** (elle) kaydedilir.
- **Pliaforma, öğrenci ödemelerinden komisyon almaz.** Pliaforma'nın geliri (ileride) stüdyo aboneliklerinden oluşur.

### 3.5 Stüdyonun Sorumluluğu
- Stüdyo; kendi öğrenci ve eğitmenlerine ait kişisel verileri **hukuka uygun** şekilde girmek, gerekli aydınlatmayı yapmak ve hukuki sebebe sahip olmaktan sorumludur.
- **Sağlık verisi girilmemelidir.** Platformda özel nitelikli (sağlık) veri toplama kapalıdır; serbest metin alanlarına sağlık bilgisi yazılmamalıdır.
- Stüdyo, girdiği verilerin doğruluğundan ve güncelliğinden sorumludur.

### 3.6 Hesap Güvenliği
- Şifreler geri döndürülemez şekilde saklanır; **MFA (çok faktörlü doğrulama)** Owner/StudioAdmin için kullanılabilir/zorunlu kılınabilir.
- Kullanıcı, hesap kimlik bilgilerinin gizliliğinden sorumludur.
- Yetkisiz erişim şüphesi derhal bildirilmelidir.

### 3.7 Kabul Edilebilir Kullanım / Fesih / Değişiklik
- *(Standart maddeler — avukat tarafından tamamlanacak: yasak kullanımlar, hizmet kesintileri, sorumluluk sınırı, uygulanacak hukuk, fesih, şartların güncellenmesi.)*

---

## 4. Stüdyo ↔ Pliaforma Veri İşleme Sözleşmesi (DPA) Taslağı

**Taraflar:** [ŞAHIS ŞİRKETİ UNVANI] ("Pliaforma") ve hizmeti kullanan stüdyo ("Stüdyo").

### 4.1 Rol Tanımları *(avukat onayına tabi)*
- **Stüdyo**, kendi öğrenci ve eğitmenlerine ait operasyonel veriler (kimlik/iletişim, ders, yoklama, paket/sözleşme, ödeme) bakımından **veri sorumlusu adayıdır**.
- **Pliaforma**, bu stüdyo operasyon verilerini stüdyo adına ve talimatıyla işleyen **veri işleyen adayıdır**.
- **Pliaforma**, platform hesabı, kimlik doğrulama, güvenlik/denetim kayıtları ve abonelik verileri bakımından **ayrıca veri sorumlusu** olabilir.

### 4.2 İşlemenin Konusu ve Kapsamı
- **Veri kategorileri:** öğrenci/eğitmen kimlik-iletişim, üyelik, ders/rezervasyon/yoklama, paket/sözleşme/ders hakkı, ödeme kaydı, bildirim kayıtları.
- **İlgili kişi grupları:** öğrenciler, eğitmenler, stüdyo yetkilileri.
- **Amaç:** stüdyo operasyonlarının yürütülmesi.

### 4.3 Mimari ve İzolasyon
- Sistem **ortak veritabanı** üzerinde çalışır; ancak **tenant (stüdyo) izolasyonu** uygulanır: her sorgu stüdyo kimliğiyle (`StudioId`) sınırlandırılır, stüdyolar arası erişim engellenir.
- **PlatformAdmin**, bakım, destek ve denetim amacıyla **teknik/denetimli erişime** sahip olabilir; bu erişim sınırlı ve kayıt altındadır.

### 4.4 Teknik ve İdari Tedbirler
Pliaforma asgari olarak şu tedbirleri uygular (ayrıntı: [../security/KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md](../security/KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md)):
- **Erişim kontrolü:** rol bazlı yetkilendirme, en az yetki ilkesi, tenant izolasyonu
- **Kimlik güvenliği:** şifre hash (BCrypt), token hash, **MFA**, hesap kilitleme, parola politikası
- **Şifreleme:** taşımada TLS/HTTPS, MFA gizli anahtarının şifreli saklanması, **şifreli yedek (gpg AES256)**
- **Denetim:** değiştirilemez (append-only) **güvenlik denetim kaydı**, 2 yıl saklama
- **Web sertleştirme:** CSP (nonce), güvenlik başlıkları, rate limit
- **Saklama/silme:** süresi dolan token'ların kalıcı silinmesi, saklama politikası ([../security/RETENTION_AND_DELETION_POLICY.md](../security/RETENTION_AND_DELETION_POLICY.md))
- **Süreklilik:** otomatik yedek + restore drill
- **İhlal yönetimi:** olay müdahale planı + 72 saat bildirim runbook'u

### 4.5 Alt İşleyenler (Sub-processors)
| Kategori | Sağlayıcı | Durum |
|---|---|---|
| Hosting | [HOSTING SAĞLAYICI] | Aktif |
| E-posta (SMTP) | [SMTP SAĞLAYICI] | Aktif (prod) |
| Yedek saklama | [BACKUP LOKASYONU] | Aktif |
| SMS/WhatsApp | [SMS/WHATSAPP SAĞLAYICI - ŞU AN YOK] | **Şu an yok** |
| Ödeme | [ÖDEME SAĞLAYICI - ŞU AN YOK] | **Şu an yok** |

Her alt işleyen için DPA imzalanması ve (varsa) yurt dışı aktarımın değerlendirilmesi gerekir.

### 4.6 Stüdyonun Yükümlülükleri
- Hukuka uygun veri girmek ve gerekli aydınlatmayı yapmak
- **Sağlık verisi girmemek**
- İlgili kişi taleplerinde Pliaforma ile işbirliği yapmak

### 4.7 İlgili Kişi Talepleri, İhlal Bildirimi, Sözleşme Sonu
- Pliaforma, stüdyoya yönelik ilgili kişi taleplerinde makul ölçüde **yardımcı olur**.
- Pliaforma, bir ihlali öğrendiğinde stüdyoyu **gecikmeksizin** bilgilendirir.
- Sözleşme sona erince veriler, mevzuata uygun şekilde **iade/silme/anonimleştirme** ile ele alınır *(yöntem ve süre TBD — avukat)*.

---

## 5. Minimal Çerez Politikası Taslağı

**Son güncelleme: [TARİH] — TASLAK**

Pliaforma, şu anda **analitik (analytics), reklam veya izleme (pixel/tracker) çerezleri KULLANMAMAKTADIR.** Yalnızca hizmetin çalışması için **zorunlu/fonksiyonel** mekanizmalar kullanılır:

| Mekanizma | Tür | Amaç | Saklama |
|---|---|---|---|
| Oturum yenileme token'ı | **httpOnly çerez** (JavaScript erişemez) | Güvenli oturum sürekliliği | Oturum/süre dolana kadar |
| `pf_active_studio` | localStorage | Seçili stüdyo bağlamını hatırlama | Tarayıcıda kalır (kullanıcı silebilir) |
| `pf_theme` | localStorage | Görünüm tercihi (açık/koyu tema) | Tarayıcıda kalır |

- Bu mekanizmalar **kişiselleştirilmiş reklam veya üçüncü taraf takibi için kullanılmaz.**
- Tarayıcı/uygulama localStorage tercihleri kullanıcı tarafından temizlenebilir.

> **İleride analitik/pixel eklenirse:** bu metin güncellenir ve gerekiyorsa **açık rıza / çerez tercih (consent) mekanizması** eklenir (bkz. Bölüm 7).

---

## 6. Veri Sahibi Başvuru Süreci Taslağı

### 6.1 Başvuru Kanalı
İlgili kişi (KVKK m.11) başvurularınızı **[KVKK BAŞVURU E-POSTASI]** adresine iletebilirsiniz. *(İleride yazılı/KEP kanalları eklenebilir — TBD.)*

### 6.2 Kimlik Doğrulama
Başvurunun gerçekten ilgili kişiye ait olduğunun teyidi için, hesabınızla ilişkili e-posta üzerinden ve gerektiğinde ek doğrulama ile **kimlik doğrulaması** yapılır. Kimliği doğrulanamayan talepler işleme alınmaz.

### 6.3 Değerlendirme Süresi
Talepler, niteliğine göre **en geç 30 gün** içinde sonuçlandırılır (KVKK m.13). Ücret gerektiren hâllerde Kurul tarifesine göre ücret alınabilir.

### 6.4 Yönlendirme (Platform ↔ Stüdyo)
- **Stüdyo kapsamındaki verilerle** ilgili talepler (ör. bir stüdyodaki ders/ödeme/yoklama kaydı) **ilgili stüdyoya yönlendirilir**; stüdyo o veriler bakımından veri sorumlusudur.
- **Platform hesabınızla** ilgili talepleri (kimlik, oturum, güvenlik kayıtları, abonelik) **Pliaforma** değerlendirir.

### 6.5 Silme / Anonimleştirme ve İstisnalar
- Silme/yok etme talepleri, **mali kayıt saklama yükümlülükleri**, **güvenlik denetim kayıtları** (değiştirilemez, 2 yıl) ve **yedeklerin rotasyon süresi** dikkate alınarak değerlendirilir.
- Bu durumlarda kimlik/iletişim verisi **anonimleştirilebilir**, mali/denetim kaydı ise yasal süre boyunca kişiyle bağı koparılmış biçimde saklanabilir.
- Tüm bu istisnaların uygulanması **avukat onayına tabidir**. *(Pilot döneminde talepler manuel olarak ele alınır; otomatik silme akışı post-pilot.)*

---

## 7. Açık Rıza — Koşullu Başlıklar (tam metin YAZILMADI)

> Aydınlatma metni ile **açık rıza ayrıdır**. Aşağıdaki durumlar **şu an kapalıdır**; açıldığında her biri için **ayrı, opt-in açık rıza metni** hazırlanmalıdır. Bu başlıklar yalnız hatırlatma amaçlıdır; tam rıza metni bu aşamada yazılmamıştır.

İleride açık rıza gerekecek başlıklar:
1. **HealthNotes / sağlık verisi açılırsa** → KVKK m.6 özel nitelikli veri; **açık rıza zorunlu** + erişim sınırı + denetim + (öneri) şifreleme.
2. **SMS / WhatsApp ticari ileti açılırsa** → ticari elektronik ileti için ayrı **onay (opt-in)**.
3. **Pazarlama bildirimi açılırsa** → pazarlama/profilleme için ayrı açık rıza.
4. **Analytics / pixel eklenirse** → izleme çerezleri için **çerez rızası (consent)** mekanizması + çerez metni güncellemesi.

---

## 8. Eksik Bilgiler / TBD Listesi

> **BU METİNLER TASLAKTIR — AVUKAT ONAYI OLMADAN YAYINLANMAZ.**

Doldurulması gereken bilgiler (ayrıntı: [KVKK-OWNER-QUESTIONNAIRE.md](KVKK-OWNER-QUESTIONNAIRE.md)):

- [ ] Şahıs şirketi **tam unvanı** → `[ŞAHIS ŞİRKETİ UNVANI]`
- [ ] **Resmi adres** → `[ADRES]`
- [ ] **KVKK başvuru e-postası** → `[KVKK BAŞVURU E-POSTASI]`
- [ ] **Telefon** → `[TELEFON]`
- [ ] **Hosting firması / ülke** → `[HOSTING SAĞLAYICI]`
- [ ] **SMTP sağlayıcı / ülke** → `[SMTP SAĞLAYICI]`
- [ ] **Domain adı** → `[DOMAIN ADI]`
- [ ] **Yedek lokasyonu / ülke** → `[BACKUP LOKASYONU]`
- [ ] **VERBİS durumu** → `[VERBİS DURUMU]`
- [ ] **Mali kayıt saklama süresi** (mali müşavir/avukat)
- [ ] **Avukat onayı** (tüm metinler)
- [ ] **Incident contact rolleri** (koordinatör / teknik / hukuki)
- [ ] Yurt dışı aktarım değerlendirmesi (sağlayıcı ülkelerine göre)

---

## 9. HealthNotes Notu (Sağlık Verisi)

> **Pilot kararı: HealthNotes KAPALIDIR (sağlık verisi toplanmıyor).**

- Veri modelinde `StudentProfile.HealthNotes` alanı **fiziksel olarak mevcuttur** (şema), ancak pilotta **kullanılmaması** kararı verilmiştir. Bu bir **ürün/politika** kararıdır; teknik garanti değildir.
- Ayrıca `StudentProfile.Notes`, `Attendance.Notes`, `ClassSession.Notes`, `ClassBooking.CancellationReason`, `MakeupRequest.Description` gibi **serbest metin** alanlarına yanlışlıkla sağlık bilgisi yazılması riski vardır.
- **Serbest metin alanlarına sağlık verisi GİRİLMEMELİDİR.**

**Teknik risk / öneri (ayrı mini sprint olarak ele alınabilir):**
1. `HealthNotes` alanının UI'dan **gizlenmesi/kaldırılması** (pilot kararı netse).
2. Serbest metin alanlarına **"sağlık verisi girmeyiniz" uyarısı** eklenmesi.
3. İleride HealthNotes açılırsa: **açık rıza** (Bölüm 7), erişimin Owner/StudioAdmin ile sınırlanması, erişim/değişikliğin denetim kaydına alınması, alanın **şifrelenmesi** (bugün şifreli değil).

> Bu, kod/migration gerektiren bir iş olduğundan **ayrı bir mini sprint** olarak planlanmalıdır (bu doküman taslağının kapsamı dışındadır).

---

*Bu paket hukuki görüş değildir; avukat kontrolü gerekir. Avukat onayı olmadan yayınlanmaz.*
