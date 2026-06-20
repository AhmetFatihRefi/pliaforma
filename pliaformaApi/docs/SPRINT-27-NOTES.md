# SPRINT 27 — ÜRÜN NOTU (henüz spec değil): Instructor Makeup Request & Pending Schedule Blocks

> **Durum:** Ürün kararı kaydı. Kod/spec YOK. Bir sonraki sprintte tam spec hazırlanıp onaya sunulacak.
> Bu işin **migration/yeni tablo/status gerektirme ihtimali yüksek** olduğundan **v1.1.x role-hardening
> kapsamına girmez** — ayrı Sprint 27.

## Amaç
Hoca (Instructor), telafi kredisi olan öğrenci için **telafi dersi talebi** oluşturabilsin; ancak bu talep
**Owner/StudioAdmin onaylamadan aktif ders sayılmaz**. Talep, oluşturulduğu anda programda görünür ("Onay
bekliyor"). Onaylanınca aktif telafi dersine döner ve kredi gerçekten tüketilir.

## Pending (onay bekleyen) talep davranışı
- Ders programında görünür; **"Onay bekliyor" / "Talep"** etiketiyle ayrılır.
- Normal ders gibi **aktif sayılmaz**; **yoklama/onay yapılamaz**.
- **Saati BLOKLAR:**
  - Aynı saat aralığına aynı **hoca** için başka ders/talep atanamaz.
  - Aynı saat aralığına aynı **öğrenci** için başka ders/talep atanamaz.
- **Kredi REZERVE:** aynı telafi kredisi başka talep/ders için tekrar kullanılamaz (pending'de kredi "rezerve").

## Approve (Owner/StudioAdmin) davranışı
- Talep onaylanır → programdaki pending kayıt **aktif telafi dersine** dönüşür.
- Telafi kredisi **gerçekten tüketilir** (reserve → consume).
- Öğrenci + hoca programında görünür; **yoklama/onay akışı aktifleşir**.
- Owner/StudioAdmin gerekirse **tarih/saat/hoca düzenleyerek** onaylayabilir.

## Reject davranışı
- Talep reddedilir → programdan kalkar, saat boşa çıkar.
- **Rezerve kredi serbest kalır** (kredi tüketilmez).

## Student görünümü
- Öğrenci kendi programında talebi görür; etiket: **"Onay bekleyen telafi talebi"**.
- Öğrenci **işlem yapamaz** (salt bilgilendirme).

## Yetkiler
**Instructor yapabilir:** kendi öğrencisi/kendi ders bağlamındaki öğrenci için telafi dersi **talebi
oluşturma**; kendi pending taleplerini görme; gerekirse **kendi pending talebini geri çekme** (onaylayamaz).
**Instructor yapamaz:** ek telafi kredisi verme/manuel arttırma; talebi **approve etme**; başka hocanın adına
talep; global telafi kredi yönetimi.
**Owner/StudioAdmin yapabilir:** tüm pending talepleri görme; onaylama/reddetme; tarih/saat/hoca düzenleyerek
onaylama. **Telafi kredisi verme/düzeltme yetkisi yine yalnız manager'da.**

## Yönetim ekranı (Owner/StudioAdmin) — "Telafi Talepleri"
Pending talep listesi: talebi oluşturan hoca · öğrenci · ders tipi · tarih/saat · açıklama/not · çakışma durumu ·
**Onayla / Reddet** (ve düzenleyerek onayla).

## Spec hazırlanırken ANALİZ EDİLECEK sorular
1. **Yeni entity gerekli mi?** (örn. `MakeupRequest`) yoksa mevcut `ClassSession` üzerinde mi?
2. **ClassSession'da `Pending` status** kullanılabilir mi? (mevcut: Scheduled/Cancelled/Completed → yeni status =
   migration). Pending session + `SessionKind=Makeup` + bir `RequestedByUserId`/`ApprovalStatus` alanı mı?
3. **Pending talepler schedule listesine** nasıl dahil edilir (GetClassSessions filtreleri pending'i kapsayacak mı,
   etiket/ayrım nasıl)?
4. **Conflict kontrolleri pending'i de hesaba katacak mı?** `HasConflictAsync`/`StudentConflictAsync` şu an yalnız
   `Scheduled`'a bakıyor → pending bloklaması için Scheduled+Pending'i kapsamalı.
5. **Makeup credit reserve/consume modeli:** mevcut `MakeupCreditTransaction` ledger'ında **reserve** (geçici, -1
   "Reserved") vs **consume** (kalıcı) ayrımı nasıl? Reject'te reserve geri alınır (ters kayıt). Pending bakiyeyi
   düşürmeli ama "kullanıldı" sayılmamalı.
6. **Rejected talepler:** audit için saklanır mı (status=Rejected) yoksa soft-delete mi?
7. **Öğrenci programında** pending talep gösterimi (`my-bookings` / student dashboard'a pending etiketi).
8. **Instructor programında** pending talep gösterimi + "geri çek" aksiyonu.
9. **Owner/StudioAdmin approval ekranı** (yeni sayfa + endpoint'ler: list/approve/reject/edit-approve).
10. **Migration gerekir mi?** (yeni status/alan/tablo → büyük olasılıkla EVET). Migration gerekiyorsa: hangi
    entity/alan, geriye dönük veri bozulmadan, rollback riski.

## Yetki uçları (spec'te netleşecek — ön taslak)
- Instructor: makeup-request **create** (kendi bağlamı) + **list-own** + **withdraw-own**.
- Owner/StudioAdmin: makeup-request **list-all** + **approve** + **reject** + **edit-approve**.
- Hard kurallar korunur: pending hoca/öğrenci çakışma bloğu; kredi reserve tek-kullanım; approve'da gerçek consume.

## Kapsam dışı (Sprint 27)
Telafi expiry zorlaması · gerçek bildirim kanalı · drag/drop grid · çoklu-currency. (Mevcut TECH-DEBT ile uyumlu.)
