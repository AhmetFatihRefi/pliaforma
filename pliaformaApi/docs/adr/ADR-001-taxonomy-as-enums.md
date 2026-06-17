# ADR-001 — "Pilates grupları" enum eksenleriyle modellenir

**Durum:** Kabul edildi (Sprint 1'de uygulanacak)

**Bağlam:** Ürün sahibi taksonomiyi bize bıraktı. Serbest metin "grup" yerine yapılandırılmış model gerek.

**Karar:** `ClassType` dört ortogonal enum ekseniyle tanımlanır: Equipment (Mat/Reformer/Cadillac/Chair/
Barrel), Capacity (Private/Duo/Group), Level (Beginner/Intermediate/Advanced/AllLevels), Purpose
(General/ClinicalRehab/Prenatal) + DefaultDurationMinutes. Stüdyo bunlardan isimli gruplar oluşturur.

**Gerekçe:** Doğrulama (Private⇒kapasite 1), hocayı gruba göre filtreleme, fiyat/rapor üretimi mümkün
olur; stüdyo yine de esnek isimlendirir.
