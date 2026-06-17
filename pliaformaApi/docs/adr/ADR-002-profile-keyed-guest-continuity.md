# ADR-002 — Misafir sürekliliği için profil-anahtarlı veri

**Durum:** Kabul edildi (Sprint 2'de uygulanacak)

**Bağlam:** Hoca, telefonla arayan öğrenci için misafir kaydı açabilmeli; öğrenci gelince bu kayıt bir
User hesabına bağlanmalı, geçmiş rezervasyon/ödemeler kaybolmamalı.

**Karar:** Rezervasyon/ödeme gibi kayıtlar `User`'a değil **`StudentProfile`**'a bağlanır.
`StudentProfile.UserId` **nullable**'dır (null=misafir). Dönüşüm yalnızca User oluşturup `UserId`'yi
doldurur; profile bağlı her şey olduğu gibi kalır.

**Gerekçe:** Misafir→User geçişi veri taşımadan, atomik ve geri-uyumlu olur.
