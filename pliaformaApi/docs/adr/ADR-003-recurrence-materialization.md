# ADR-003 — Tekrarlayan dersler materyalize edilir

**Durum:** Önerildi (Sprint 5'te kesinleşecek)

**Bağlam:** Hocalar tekrarlayan slotlar, öğrenciler tekrarlayan rezervasyon ister. Anlık (on-the-fly)
hesap mı, somut kayıt mı?

**Karar:** `RecurrenceRule` ileriye dönük **somut `ClassSession`** kayıtları üretir (yuvarlanan ufuk).
`RecurringBookingPattern` bu oturumlara `Booking` üretir. Üretim **idempotent** background job ile.

**Gerekçe:** Kapasite/çift-rezervasyon kontrolü, iptal ve istisna (tek seferlik değişiklik) somut
kayıt üzerinde çok daha basit ve tutarlı olur. DST, stüdyo zaman diliminde ele alınır.
