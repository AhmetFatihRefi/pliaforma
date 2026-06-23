# Sprint 32 — Ders Tipi Bazlı Ekipman/Envanter Kapasitesi + Boşluk Bulma Motoru

> **Durum:** Onaylı spec. Faz planı kabul edildi; **yalnız 32A uygulanıyor**.
> **Onay tarihi:** 2026-06-24
> **Sonraki tag önerisi (32A yeşil olursa):** `v1.21.0-pilot`

---

## 0. Amaç

Pliaforma'da ders kapasitesi yalnız "dersin grup kapasitesi" ile değil, seçilen ders tipine bağlı **ekipman/envanter kapasitesi** ile de kontrol edilecek. Özellikle Reformer / Mat gibi derslerde önemli:

- Reformer dersi → reformer aleti gerekir. 4 reformer varsa aynı anda en fazla 4 kişi reformer kullanabilir.
- Mat dersi → mat gerekir. 30 mat varsa aynı anda en fazla 30 kişi.

Her ekipman için iki ayrı değer:

1. **Quantity / Adet** — stüdyoda bu ekipmandan kaç tane var.
2. **PerUnitConcurrentUsage** — bir ekipmanı aynı anda kaç kişi kullanabilir (default 1, `>= 1`).

```
TotalCapacity = Quantity * PerUnitConcurrentUsage
```

Örnekler: 4 reformer × 1 = 4 · 30 mat × 1 = 30 · 2 özel ekipman × 2 = 4.

---

## 1. Faz planı (KABUL)

Sprint 32 üçe bölünür. **Şimdi yalnız 32A uygulanır.**

| Faz | Kapsam | Migration | Risk |
|---|---|---|---|
| **32A** | Equipment entity + CRUD backend + `ClassType.EquipmentId` bağ + Envanter UI + ClassType formuna ekipman seçici + TotalCapacity preview + eski davranış korunması. **Enforcement YOK.** | ✅ `Sprint32A_EquipmentInventory` | Düşük, izole |
| **32B** | Equipment capacity guard: create/update/bulk/reschedule availability check + `FOR UPDATE` kilidi + UI kapasite uyarısı + legacy 9999 güvenli geçiş kuralı. | yok | Orta (davranış) |
| **32C** | Availability/slot-finding endpoint + algoritma + "Boşluk Bul" UI + seans-create entegrasyonu. Default çalışma penceresi `07:00–23:00`. | yok | Orta (yeni okuma) |

---

## 2. Binding modeli (KABUL): `ClassType.EquipmentId` nullable FK (N:1)

- Bir **Equipment havuzu birden fazla ClassType** tarafından kullanılabilir.
  - Örn: tek "Reformer" Equipment havuzu → "Reformer Başlangıç" + "Reformer İleri" ClassType'ları aynı `EquipmentId`'ye bağlanır.
- **Join table YOK.** Bir ClassType MVP'de **en fazla bir** Equipment havuzu kullanır.
- Bir ders tipi birden fazla farklı ekipman gerektirirse → **post-MVP** (`EquipmentClassType` N:N join, additive).
- `ClassType.EquipmentId == null` → ekipman kısıtı yok → **eski davranış aynen sürer**.

---

## 3. Equipment domain modeli (KABUL)

**Yeni entity `Equipment` (tenant-scoped, `BaseEntity`):**

| Alan | Tip | Kural |
|---|---|---|
| `Id` | Guid | BaseEntity |
| `StudioId` | Guid | tenant |
| `Name` | string (max 150) | zorunlu |
| `Description` | string? (max 1000) | opsiyonel |
| `Quantity` | int | `>= 0` (0 = cihaz yok/arızalı; TotalCapacity 0 olur) |
| `PerUnitConcurrentUsage` | int | default **1**, `>= 1` (0/negatif **reddedilir**) |
| `IsActive` | bool | default true; `false` → availability hesabına katılmaz |

- **Computed (saklanmaz):** `TotalCapacity = Quantity * PerUnitConcurrentUsage`.
- Availability hesabında (32B+) yalnız **aktif** ekipmanların toplam kapasitesi kullanılır.
- Index: `(StudioId, Name)`.
- Soft-delete: `BaseEntity.IsDeleted` (global filtre).

### UI açıklamaları (normal kullanıcı için basit)
- **Quantity** = "Bu ekipmandan kaç adet var?"
- **PerUnitConcurrentUsage** = "1 ekipmanı aynı anda kaç kişi kullanabilir?" → Reformer ve Mat için normalde **1**.
- **TotalCapacity** = canlı preview (`Quantity × PerUnitConcurrentUsage`).

---

## 4. Capacity reservation MVP (KABUL) — Named assignment YOK

- Seans tek tek "Reformer 1 / Reformer 2" atamaz.
- Seans, equipment havuzundan **N kişilik kapasite** rezerve eder.
- Bu N, ilk aşamada **`ClassSession.Capacity` ile aynı** kabul edilir.
- Named equipment assignment → **post-pilot**.

> **Not:** 32A'da hiçbir rezervasyon/guard yok; bu kural 32B'de devreye girer. Burada referans olarak yazılı.

---

## 5. Overlap kuralları (32B+ referansı)

Mevcut yarı-açık `[start, end)` predicate **birebir** yeniden kullanılır:

```
existing.StartAt < candidate.EndAt && candidate.StartAt < existing.EndAt
```

- 13:00–14:00 ile 14:00–15:00 → çakışmaz.
- 13:00–14:00 ile 13:30–14:30 → çakışır.

Ekipman hesabı (32B) aynı predicate'i "per-instructor" yerine **EquipmentId havuzu** üzerinden toplar. Yalnız `Status=Scheduled` seanslar sayılır.

---

## 6. KRİTİK DÜZELTME — Legacy `Capacity=9999` riski

### Sorun
Bir ClassType sonradan Equipment'a bağlanırsa, o tipte **gelecekte Scheduled, `Capacity=9999` (limitsiz)** eski seanslar olabilir. Bunları availability'de yanlış sayarsak ekipman fazla kullandırılabilir.

### Reddedilen yaklaşım
**"9999'u used=0 say"** yaklaşımı **kabul edilmedi** — gerçek guard (32B) için risklidir: availability yanlış görünür, ekipman over-allocate edilir. (En fazla, enforcement öncesi geçici **raporlama** için düşünülebilirdi; guard'da yasak.)

### Seçenek analizi

| Seçenek | Açıklama | Değerlendirme |
|---|---|---|
| **A. Bağlama sırasında engelle** | ClassType'a EquipmentId atanırken, o tipte gelecekte Scheduled `Capacity=9999` session varsa **bağlamayı reddet** → kullanıcı önce o seanslara sonlu kapasite atasın. | Basit, güvenli. Booking'e dokunmaz. |
| **B. Düzeltme akışı sun** | Bağlama sırasında kullanıcıya "şu future seanslara kapasite ata" akışı sunulur, sonra bağlanır. | En kullanıcı dostu; daha çok UI işi. Booking'e dokunmaz. |
| C. Booking'de yeniden hesapla | Equipment availability booking anında tekrar hesaplanır. | **Reddedildi** — booking yolunu değiştirir, karmaşık. |
| D. 9999'u BookedCount kadar say | Limitsiz seansın rezervasyonunu fiili öğrenci sayısı sayar. | **Reddedildi** — "kapasite otomatik büyümez" kuralını bozar, yanıltıcı. |

### Karar
- **Ön tercih: A veya B.** Booking tarafı **mümkün olduğunca değiştirilmez**.
- **32B net kuralı:** `EquipmentId bağlı ClassType + Scheduled future session + Capacity=9999 = GEÇERSİZ durum.` Create / update / bulk / reschedule bu durumu **üretemez** (ekipman-bağlı yeni/güncellenen seans **limitsiz olamaz**, sonlu kapasite zorunlu).
- **Migration/backfill YOK:** var olan legacy 9999 kayıtları migrate edilmez; bunun yerine **bağlama/update anında** güvenli uyarı/engel (A veya B) uygulanır.

### 32A'daki davranış (enforcement yokken)
- 32A'da hiçbir guard yok; EquipmentId atanabilir.
- **Ancak** risk dokümante edilmeli ve mümkünse 32A'da bile bağlama sırasında **uyarı/ön-kontrol** (sadece okuma; future Scheduled 9999 session sayısını gösteren bilgilendirme) yapılabilir. **Engelleme/zorlama 32B'ye bırakılır.**
- Bu spec, 9999 riskini ve 32B geçiş kuralını açıkça kayıt altına alır (bu bölüm).

---

## 7. Diğer kararlar

| Konu | Karar |
|---|---|
| **Çalışma penceresi (32C)** | İlk versiyon default **07:00–23:00**. Stüdyo bazlı `WorkingHours` ayarı **post-MVP**. |
| **Equipment silme** | **Restrict.** Bağlı ClassType varsa silinemez → önce pasifleştir veya bağı kaldır. Hard delete yerine **soft-delete/pasifleştirme UI öncelikli**. |
| **Bireysel ders** | `IsIndividual` session → `Capacity=1`; ekipman bağlıysa **1 ekipman kapasitesi** rezerve eder (32B). |
| **Eski `ClassType.Equipment` enum** | **Dokunma.** Scope dışı. Gerçek bağ artık `ClassType.EquipmentId`. |
| **Concurrency (32B)** | Pilot: create/update transaction'ında ilgili Equipment satırına `SELECT … FOR UPDATE` (mevcut `LockStudentAsync` deseni). Optimistic + retry → post-pilot. |

---

## 8. Endpoint planı

```
GET    /api/studio/equipment             # liste (onlyActive? query)         [Owner, StudioAdmin]
GET    /api/studio/equipment/{id}        # tek kayıt                          [Owner, StudioAdmin]
POST   /api/studio/equipment             # oluştur                            [Owner, StudioAdmin]
PUT    /api/studio/equipment/{id}        # güncelle                           [Owner, StudioAdmin]
DELETE /api/studio/equipment/{id}        # sil (Restrict: bağlı ClassType varsa 409) [Owner, StudioAdmin]

PUT    /api/class-types/{id}             # mevcut + opsiyonel EquipmentId alanı
GET    /api/class-types                  # mevcut + EquipmentId/EquipmentName DTO'da

# 32C (henüz YOK):
GET /api/studio/availability/class-type-slots?classTypeId&date|from&to&durationMinutes&requiredCapacity&instructorId?&incrementMinutes?
```

---

## 9. UI planı

- **Envanter (`/dashboard/equipment`, Owner/StudioAdmin):** CRUD listesi (class-types sayfası iskeletiyle). Alanlar: Name, Description, Quantity, PerUnitConcurrentUsage, **TotalCapacity canlı preview**, IsActive. Bağlı ClassType listesi (salt-okunur). Pasifleştir + sil (bağlıysa engel mesajı). Sidebar `MANAGER`.
- **ClassType formu:** "Ekipman (opsiyonel)" seçici → `EquipmentId` set/clear. Aynı ekipman birden çok ders tipine atanabilir.
- **Boşluk Bul (32C, henüz yok):** ders tipi + süre + kişi + tarih/aralık + (ops.) hoca → sonuç pencereleri.

---

## 10. Availability algoritması (32C — referans, henüz YOK)

Girdi: `classTypeId, durationMinutes, requiredCapacity, date|range, instructorId?, incrementMinutes? (default 15, min 5)`.

Gün başına sweep-line:
1. ClassType → Equipment E, `C = E.TotalCapacity` (yalnız aktif). Ekipman yoksa kısıtsız (yalnız hoca).
2. Çalışma penceresi (`07:00–23:00` default) içinde `incrementMinutes` ızgara.
3. Çakışan Scheduled seanslardan ekipman kullanım step-function'ı: her seans `[start,end)` üzerinde `+Capacity`; `available(t)=C−used(t)`.
4. Aday `t` geçerli ⇔ `[t,t+duration)` boyunca `min available ≥ required` ∧ `t+duration ≤ pencere sonu` ∧ `t>şimdi` ∧ en az bir hoca boş ∧ ClassType+Equipment aktif.
5. Bitişik geçerli başlangıçları **başlangıç penceresi**ne birleştir (`startWindowFrom..startWindowTo`).

Çıktı (window-based): `startWindowFrom, startWindowTo, durationMinutes, availableEquipmentCapacity, availableInstructors[]`.

---

## 11. Migration planı

- **Tek migration: `Sprint32A_EquipmentInventory`** (additive, drop YOK):
  - `Equipment` tablosu (+ `(StudioId, Name)` index, soft-delete kolonları).
  - `ClassTypes.EquipmentId` nullable kolon + FK (`onDelete: Restrict`) + `(StudioId, EquipmentId)` index.
- Backfill **gerekmez** (EquipmentId default null → eski davranış).
- `has-pending` 32A migration sonrası temiz olmalı.

---

## 12. 32A kapsamı (NET)

**32A'DA VAR:**
- `Equipment` entity + EF config + migration.
- Equipment CRUD backend (Create / Update / Delete / Get list+single) + DTO + validator + controller.
- `ClassType.EquipmentId` FK + DTO/command/validator güncellemesi.
- Equipment yönetim UI (CRUD) + bağlı ClassType listesi + TotalCapacity preview.
- ClassType formuna Equipment seçici.
- Eski davranışın korunması (EquipmentId=null).

**32A'DA YOK:**
- Equipment capacity enforcement / guard.
- create/update/bulk/reschedule guard.
- Availability finder.
- Booking logic değişikliği.
- Named equipment assignment.
- Legacy 9999 **engelleme/zorlama** (yalnız dokümante + opsiyonel salt-okuma uyarı).

---

## 13. 32A test planı

- backend build **0/0**
- frontend `tsc` **0**, `next build` **0**
- migration apply + `has-pending` **clean**
- Equipment **CRUD smoke** (create/list/get/update/delete)
- EquipmentId **bind/unbind** smoke (ClassType'a ata/kaldır)
- Bağlı ClassType listesi doğru mu?
- `TotalCapacity == Quantity * PerUnitConcurrentUsage`
- `PerUnitConcurrentUsage` 0/negatif → **reject (400)**
- `Quantity` negatif → **reject (400)**
- **Tenant isolation** (başka stüdyonun ekipmanı görünmez/dokunulmaz)
- Yetki: **Owner/StudioAdmin yetkili; Instructor/Student yetkisiz (403)**
- Eski ClassType/Session/Booking davranışı **bozulmadı**
- **No enforcement yet:** `EquipmentId=null` → eski davranış sürüyor
- Equipment silme: bağlı ClassType varsa **409** (Restrict)

---

## 14. Kapsam dışı (genel)

Otomatik program AI/optimizasyon, paket/abonelik ödeme değişikliği, HealthNotes/sağlık verisi, KVKK/legal, report/export, mobile app, real-time websocket, named individual equipment assignment, SMS/WhatsApp, online ödeme.

---

## 15. Çıktı / süreç kuralları

- Spec kalıcılaştırıldı: bu dosya.
- Sonra **yalnız 32A** implementation.
- **commit/push/tag YOK** · 32B/32C YOK · enforcement YOK · availability finder YOK.
- 32A yeşil olursa sonraki tag önerisi: **`v1.21.0-pilot`** (yalnız ayrı onayla).
