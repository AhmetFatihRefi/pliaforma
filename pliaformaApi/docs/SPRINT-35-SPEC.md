# SPRINT 35 — Haftalık Çoklu Ders Slotu (haftada N ders) Program Üretimi

> **Durum:** SPEC — onay bekliyor. **Kod YOK · Migration YOK · Commit/push/tag YOK.**
> **Ön koşul:** Sprint 34 (v1.25.0-pilot) — tercih güncelleme + replan. HEAD = `b659987`.
> **İhtiyaç:** Sözleşme satırında "bir gün/bir saat" değil, **haftalık birden fazla ders slotu** (örn. Pzt 10:00 + Per 18:00) ile, toplam `LessonCount` dolana kadar hafta-hafta tekrarlayan program üretimi.

---

## 0. TL;DR — En kritik bulgu

**Bu ihtiyacın %90'ı ZATEN ÇALIŞIYOR.** Veri modeli, üretim motoru ve replan çoklu haftalık slotu tam destekliyor; **tek gerçek eksik, "Programı oluştur" (generate) UI'ının tek-saat kısıtı.**

| Katman | Durum |
|---|---|
| **Veri modeli** (`StudentContractLinePreferredSlot`) | ✅ Çoklu slot zaten var; aynı gün-farklı saat serbest, aynı gün-aynı saat yasak |
| **Üretim motoru** (`BuildCandidates` + `RunAsync`) | ✅ Çoklu slotu hafta-hafta kronolojik dağıtıp `LessonCount`'ta durduruyor |
| **Create UI** (`AddContractLineModal`) | ✅ Çoklu slot ekleyici (gün+saat) zaten var |
| **Edit UI** (Sprint 34 `Tercihleri düzenle`) | ✅ Çoklu slot editörü zaten var |
| **Replan** (Sprint 34) | ✅ Tüm saatli slotları kullanıyor (`DeriveGenerationSlotsAsync`) |
| **Generate UI** (`Programı oluştur`) | ❌ **TEK eksik:** gün-checkbox + **tek saat** → "Pzt 10:00 + Per 18:00" girilemiyor |

**Sonuç:** Sprint 35 ağırlıkla **frontend** (generate'i preferred slots'a bağla) + UI cilası. **Migration yok, WeeklyLessonCount yok, engine değişikliği yok.**

---

## 1. Analiz — senin 10 sorunun (kod-temelli)

### S1. `StudentContractLinePreferredSlot` çoklu slotu zaten destekliyor mu? → ✅ EVET
Ayrı tablo, line başına **çok satır**. Tekillik kuralı `(DayOfWeek, PreferredStartTime)` — yani **Salı 10:00 + Salı 18:00 serbest** (farklı saat), **Salı 10:00 + Salı 10:00 yasak** (`AddLinePreferredSlot` dup-check + `UpdateContractLinePreferences` `HasInternalDuplicates`). Saat opsiyonel (gün-only tercih candidate-matching için; üretimde kullanılmaz).

### S2. Motor çoklu slot verilince haftalara dağıtıyor mu? → ✅ EVET
`ContractLineScheduleEngine.BuildCandidates` (`ContractLineScheduling.cs:174`):
```
for (date = line.StartDate .. line.EndDate)
    foreach slot where slot.DayOfWeek == date.DayOfWeek
        add Candidate(date @ slot.StartTime)
return list.OrderBy(StartAt)   // KRONOLOJİK
```
→ 2 slot (Pzt 10:00, Per 18:00) verilince **her hafta** ikisini de üretir, **kronolojik** sıralar. `RunAsync` bunları `available = LessonCount − reserved` kadar alıp durur. **Hafta-hafta tekrar + toplamda kesme zaten doğal.**

### S3. UI çoklu slotu yeterince güzel destekliyor mu? → ⚠️ KISMEN
- **Create** (`AddContractLineModal`): `prefSlots` çoklu ekleyici (gün+saat, dup-check) **var**. ✅
- **Edit** (Sprint 34 `Tercihleri düzenle`): `editSlots` çoklu editör **var**. ✅
- **Generate** (`Programı oluştur`): `days.map(d => ({dayOfWeek:d, startTime: time}))` — **tüm günler TEK saat** paylaşır → "Pzt 10:00 + Per 18:00" **GİRİLEMEZ**. ❌ **Bu sprintin asıl işi.**

### S4. `WeeklyLessonCount` kolonu gerekli mi? → ❌ HAYIR
**Haftalık ders sayısı = aktif (saatli) preferred slot sayısı.** Ayrı kolon = çift kaynak/tutarsızlık riski. (Senin ön kararın doğrulandı.) **Migration yok.**

### S5. `LessonCount=8` + 2 slot → doğal 4 hafta? → ✅ EVET
8 aday: W1Pzt, W1Per, W2Pzt, W2Per, W3Pzt, W3Per, W4Pzt, W4Per → kronolojik 8 → `RunAsync` 8'de durur = **4 tam hafta**.

### S6. Slot sayısı `LessonCount`'a tam bölünmüyorsa? → ✅ Doğal kısmi son hafta
`LessonCount=7`, 2 slot → kronolojik 7. aday = W4Pzt → üretim W4Pzt'te durur = **3 tam hafta + 4. hafta yalnız ilk slot**. (Senin örneğinle birebir.)

### S7. Kullanıcı "haftada 2" dedi ama 1 slot girdi? → Backend slot'u kaynak alır (haftada-1)
"Haftada N" yalnız **UI yardımcısı**; backend kaynağı **preferredSlots**. 1 slot girildiyse haftada-1 düzeni üretilir. **UI uyarmalı** ("Haftada 2 için 2 farklı gün/saat ekleyin; şu an 1 slot var"). Backend zorlamaz (slots = tek doğru kaynak).

### S8. Aynı gün farklı saat (Salı 10:00 + Salı 18:00)? → ✅ EVET (serbest)
Tekillik `(gün, saat)` olduğundan farklı saatler ayrı slot. Motor ikisini de aynı gün üretir (kronolojik: 10:00 sonra 18:00).

### S9. Aynı gün aynı saat duplicate? → ✅ YASAK
`AddLinePreferredSlot` (ConflictException) + `UpdateContractLinePreferences`/`Create` (`HasInternalDuplicates` → 400). Frontend slot ekleyiciler de yerel dup-check yapıyor.

### S10. Replan çoklu slotu doğru kullanıyor mu? → ✅ EVET
Sprint 34 `ContractLineReplanShared.DeriveGenerationSlotsAsync` line'ın **tüm aktif + saatli** slotlarını üretim slotuna çevirir → replan da hafta-hafta çoklu slotu uygular. Değişiklik gerekmez.

---

## 2. Eksik olan tam olarak ne?

**Yalnız "Programı oluştur" (generate) UI'ı** — şu an gün-checkbox + tek saat. İki tasarım seçeneği:

### Seçenek A (ÖNERİLEN, frontend-only, sıfır backend risk)
Generate UI'ını **satırın preferred slots'una bağla** (replan gibi). Gün-checkbox + tek-saat picker'ı kaldır; bunun yerine:
- Üretim, line'ın **kayıtlı haftalık düzenini** (saatli preferred slots) kullanır.
- Buton: *"Haftalık düzene göre {available} ders oluştur"* — frontend `l.preferredSlots` (saatli) → `generateLineSchedule(slots=...)`.
- **Mevcut `generateLineSchedule` endpoint'i zaten `slots` array alıyor** → backend değişmez.
- Tutarlılık: create/edit/generate/replan **hepsi tek kaynak = preferredSlots**.

### Seçenek B (opsiyonel backend kolaylığı)
`GenerateLineScheduleCommand.Slots`'u opsiyonel yap: verilmezse `DeriveGenerationSlotsAsync(line)` kullan. API çağıranlar slot göndermeden üretebilir; generate + replan motoru paylaşır. Küçük backend eki (validator + handler default), **yine migrationsız.** Net ama şart değil.

> **Öneri:** A'yı uygula (frontend), B'yi opsiyonel "nice-to-have" olarak işaretle. İkisi de aynı davranışı verir; A daha az risk.

---

## 3. Önerilen backend davranışı

- **Engine: DEĞİŞMEZ.** `BuildCandidates` + `RunAsync` çoklu slotu zaten doğru işliyor (hafta-hafta, kronolojik, `LessonCount` cap, skip-edileni sonraki haftada telafi).
- **Skip telafisi (önemli, mevcut davranış):** Bir slot bir hafta uygunsuzsa (closure/çakışma) `RunAsync` onu atlar ama `created` saymaz → **sonraki haftalarda** (line tarih aralığı elverdikçe) eksik ders tamamlanır. Aralık yetmezse eksik kalan = `available − created` sonuçta raporlanır (`SkippedCount` / replan `NotPlannedCount`).
- **Slot kaynağı:** generate ve replan **yalnız aktif + saatli** slotları kullanır (saatsiz/gün-only slotlar candidate-matching içindir, üretilemez → uyarı).
- **Guard'lar:** hoca/kapasite/equipment(32B)/closure/öğrenci+hoca çakışması **aynen** (RunAsync). Multi-slot per-candidate mantığı değiştirmez.
- (Seçenek B alınırsa) `GenerateLineScheduleCommand`: `Slots` opsiyonel; boşsa line preferred slots; ikisi de boşsa "üretilebilir gün/saat tercihi yok" (409/400). Validator + handler ufak ek.

---

## 4. Önerilen UI davranışı

**Create + Edit (zaten çoklu slot var → cilala):**
- Bölüm başlığı: **"Haftalık ders gün/saatleri"**. Satır satır gün + saat + sil (mevcut).
- Yardımcı metin: *"Haftada 2 ders için 2 farklı gün/saat ekleyin. Sistem toplam ders hakkı bitene kadar bu haftalık düzeni tekrarlar."*
- Slot sayısı etiketi: 1 → "Haftada 1 ders düzeni", 2 → "Haftada 2 ders düzeni", N → "Haftada N ders düzeni".
- **Önizleme (saf UI hesabı):** `weeks ≈ ceil(LessonCount / saatliSlotSayısı)` → *"8 ders / haftada 2 slot → yaklaşık 4 hafta program oluşturulur."*

**Generate ("Programı oluştur") — Seçenek A:**
- Gün-checkbox + tek-saat picker **kaldırılır**; üretim line'ın haftalık düzenini (preferred slots) kullanır.
- Slot yoksa (veya saatli slot yoksa) buton pasif + uyarı: *"Önce haftalık gün/saat ekleyin."*
- Hoca null / equipment+tercih-null durumlarında mevcut S33 zorunlu alanları (üretim-anı hoca/kapasite) korunur.
- Sonuç: *"{created} ders oluşturuldu; {skipped} ders uygunluk olmadığı için planlanamadı"* (eksik açıkça).

**Önizleme (opsiyonel):** mevcut `previewLineSchedule` zaten `slots` alıyor → haftalık düzenle önizleme gösterilebilir (per-slot plannable/skip).

---

## 5. Replan etkisi

**Replan zaten çoklu slotu doğru kullanıyor** (Sprint 34, `DeriveGenerationSlotsAsync`). Bu sprintte:
- Öğrenci 2 ders aldıktan sonra slot seti değişirse (örn. Pzt+Per → Sal+Cmt): Completed dokunulmaz; future Scheduled regular booking'ler detach; kalan dersler **yeni çoklu slot setine** göre hafta-hafta tekrar üretilir; shared-group güvenliği (yalnız bu öğrenci ayrılır, seans taşınmaz) korunur.
- **Değişiklik gerekmez** — yalnız edit UI'ında çoklu slotun güzel girilmesini sağla (zaten var). Generate Seçenek A ile birleşince create/generate/replan tam tutarlı olur.

---

## 6. Migration ihtiyacı → **YOK**

- Çoklu slot zaten `StudentContractLinePreferredSlot` tablosunda (çok satır). 
- `WeeklyLessonCount` eklenmez (= aktif slot sayısı). 
- Generate'in slot kaynağını değiştirmek şema değil davranış/UI. 
- Seçenek B alınsa bile yalnız komut opsiyonelliği (şema yok).
**→ Sprint 35 additive davranış/UI sprinti; migrationsız.**

---

## 7. Test planı (uygulamada `sprint35-smoke.sh` + regresyon)

**A. Çoklu slot üretim (engine — mevcut davranış doğrulama)**
- LessonCount=8, slots=[Pzt 10:00, Per 18:00] → generate → 8 seans; haftada 2; 4 hafta; her hafta Pzt 10:00 + Per 18:00.
- LessonCount=7, 2 slot → 3 tam hafta + 4. hafta yalnız ilk slot (Pzt).
- LessonCount=8, 1 slot → haftada 1, 8 hafta.

**B. Aynı gün farklı saat**
- slots=[Sal 10:00, Sal 18:00], LessonCount=4 → 2 hafta; her hafta Sal 10:00 + Sal 18:00 (kronolojik).

**C. Duplicate**
- aynı gün+saat 2 kez → create/preferences 400; add-slot 409.

**D. Saatsiz slot**
- gün-only slot generate'e dahil edilmez (uyarı); yalnız saatli slotlar üretir.

**E. Skip telafisi**
- bir hafta bir slot closure'a denk → o slot atlanır, eksik ders sonraki hafta tamamlanır (aralık elverdikçe); aralık yetmezse `skipped/notPlanned` raporlanır.

**F. Guard korunumu (regresyon)**
- equipment-bound + çoklu slot → her seans Capacity guard + 32B (9999 yok); hoca/öğrenci çakışması skip; kapasite tercihi (cap≤pref).

**G. Replan + çoklu slot**
- 2 Completed, slot setini değiştir, replan → Completed sabit; future detach; kalan dersler yeni çoklu slot setiyle; shared-group güvenli.

**H. Regresyon**
- backend build 0/0 · tsc 0 · next 0 · has-pending clean (migration yok).
- sprint32a/b/c · 33 · 34 · 18a/22/28c1/28c2/27/29b yeşil.

---

## 8. Açık sorular (karar/öneri)

1. **Generate tamamen preferred slots'a mı bağlansın (Seçenek A), yoksa manuel picker "override" olarak kalsın mı?** Öneri: **tam bağla (A)** — tek kaynak/tutarlılık; farklı düzen isteniyorsa önce tercih düzenlenir (replan ile aynı zihinsel model).
2. **Seçenek B (backend `Slots` opsiyonel) yapılsın mı?** Öneri: opsiyonel; A yeterli. API temizliği isteniyorsa eklenir, yine migrationsız.
3. **"Haftada N ders" UI seçici eklensin mi?** Öneri: yalnız **yardımcı/etiket** (slot sayısından türetilir); ayrı state/persist YOK.
4. **Hafta hizalama:** üretim `StartDate`'in haftasından mı başlasın, yoksa ilk uygun haftadan mı? Mevcut motor `StartDate`'ten itibaren her uygun günü tarar; geçmiş/closure skip. Öneri: **mevcut davranış** (StartDate'ten kronolojik, geçmiş atlanır) — ekstra hafta hizalama gerekmez.
5. **Slot başına farklı süre?** Kapsam dışı — süre `ClassType.DefaultDurationMinutes` (tüm slotlar aynı). Gerekirse ayrı sprint.
6. **Önizleme "≈ N hafta" tam mı tahmini mi?** Öneri: tahmini (`ceil(LessonCount/slotCount)`); kesin sonuç generate/preview'da (skip/çakışma sonrası gerçek).

---

## 9. Önerilen kapsam özeti (uygulama onaylanırsa — şimdi DEĞİL)

- **Backend:** engine **değişmez**. (Opsiyonel B: `GenerateLineScheduleCommand.Slots` opsiyonel + default preferred slots.)
- **Frontend:** generate'i preferred slots'a bağla (Seçenek A); create/edit slot bölümlerine "Haftalık ders gün/saatleri" başlığı + yardımcı metin + slot-sayısı etiketi + "≈ N hafta" önizleme; sonuçta eksik planlanan ders sayısı.
- **Test:** `sprint35-smoke.sh` (A–G) + regresyon.
- **Doğrulama:** build/tsc/next 0 · has-pending clean (migration yok) · smoke + regresyon yeşil.
- **Migration YOK.** Commit/push/tag yalnız ayrı onayla. Önerilen sonraki tag: `v1.26.0-pilot`.

> **Bu dosya kalıcı spec'tir.** Uygulama yalnız onaylanan kapsamla başlar. **Şu an kod/migration/commit YOK.**
