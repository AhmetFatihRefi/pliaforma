# KVKK Teknik ve İdari Tedbirler Checklist'i — Pliaforma

Statü: **TASLAK (Sprint 31A)** · Referans: KVKK m.12 + Kurul "Kişisel Veri Güvenliği Rehberi" başlıkları.

> Bu doküman teknik/operasyonel hazırlığı belgeler; **hukuki metinler (aydınlatma, açık rıza, işleyen
> sözleşmeleri) KVKK avukatı onayı gerektirir.** Statü: ✅ var · ⚠️ kısmi · ❌ yok · ⏳ planlı (sprint).

---

## A. Teknik tedbirler

### A.1 Erişim kontrolü & yetki matrisi
| Tedbir | Statü | Kanıt / Plan |
|---|---|---|
| Kimlik doğrulama (her uç korunuyor) | ✅ | Tüm controller `[Authorize]`; bkz. [../SECURITY-REVIEW.md](../SECURITY-REVIEW.md) §2 |
| Rol bazlı yetki | ✅ | `[Authorize(Roles=...)]` + PlatformAdmin policy |
| En az yetki ilkesi | ✅ | Student kendi verisi; Instructor kendi seansı/öğrencisi |
| Yazılı yetki matrisi | ⚠️→✅ | Aşağıdaki matris; [DATA_INVENTORY.md](DATA_INVENTORY.md) erişim sütunu |
| Yetkisiz erişim girişimi tespiti | ❌ ⏳31B | Failed-login audit (P0-2/P1-1) |

**Yetki matrisi (özet):**

| Veri grubu | PlatformAdmin | Owner | StudioAdmin | Instructor | Student |
|---|---|---|---|---|---|
| Platform (tüm tenant) | ✔︎ | — | — | — | — |
| Öğrenci kimlik/iletişim | (platform) | ✔︎ | ✔︎ | kendi öğrencisi | kendisi |
| HealthNotes (özel nitelikli) | (platform) | ✔︎ | ✔︎ | **31C'de karar** | kendisi |
| Ödeme/sözleşme (finans) | (platform) | ✔︎ | ✔︎ | ✖︎ (403) | kendi sözleşmesi (R) |
| Eğitmen ücreti (Rate) | (platform) | ✔︎ | ✔︎ | kendisi | ✖︎ |
| Rol değişikliği | (platform) | ✔︎ | ✖︎ (yalnız Owner) | ✖︎ | ✖︎ |
| Bildirim | — | kendi | kendi | kendi | kendi |

### A.2 Tenant izolasyonu
| Tedbir | Statü | Kanıt |
|---|---|---|
| Handler seviyesi tenant filtresi | ✅ | `Id==x && StudioId==current` → cross-tenant 404 |
| Body'den StudioId güveni yok | ✅ | Daima JWT claim |
| Otomatik regresyon testi | ❌ ⏳31C | P1-9 |

### A.3 Şifreleme
| Tedbir | Statü | Kanıt / Plan |
|---|---|---|
| Taşımada şifreleme (TLS) | ❌ ⏳31B | P0-1 (TLS aktif değil) |
| Parola hash (BCrypt) | ✅ | `PasswordHasher` |
| Token saklama (SHA-256 hash) | ✅ | AuthToken/RefreshToken yalnız hash |
| DB at-rest şifreleme | ⚠️ TBD | Disk/volume düzeyinde (hosting'e bağlı; [DATA_PROCESSORS.md](DATA_PROCESSORS.md)) |
| Backup şifreleme | ❌ ⏳31D | P1-7 |

### A.4 Loglama / audit
| Tedbir | Statü | Plan |
|---|---|---|
| Uygulama logu (PII'siz) | ✅ | Prod JSON log; body/token loglanmaz |
| Güvenlik audit log | ❌ ⏳31B/C | P0-2 SecurityAuditLog |
| Login/rol/ödeme/export/sağlık erişimi audit | ❌ ⏳31C | P0-2 kapsam |

### A.5 Yedekleme & süreklilik
| Tedbir | Statü | Kanıt |
|---|---|---|
| Otomatik yedek + retention | ✅ | [../BACKUP-RESTORE.md](../BACKUP-RESTORE.md) |
| Restore drill | ✅ | Dokümante; periyodik kanıt ⏳31D |
| Off-site + şifreli yedek | ❌ ⏳31D | P1-7 |

### A.6 Güvenli geliştirme & zafiyet yönetimi
| Tedbir | Statü | Plan |
|---|---|---|
| Secret hijyeni / .gitignore / fail-fast | ✅ | [../SECURITY-REVIEW.md](../SECURITY-REVIEW.md) §5 |
| Dependency scan | ❌ ⏳31D | npm audit + dotnet vulnerable |
| Secret scan (CI) | ❌ ⏳31D | gitleaks/trufflehog |
| SAST / container scan / SBOM | ❌ ⏳P2 | CodeQL/Semgrep, Trivy, CycloneDX |
| Güvenlik başlıkları (nginx) | ✅ | X-Frame/X-Content/Referrer/Permissions |
| CSP | ❌ ⏳31B | P1-6 |
| Rate limit | ✅ | Auth uçları |

### A.7 Incident response (teknik)
| Tedbir | Statü | Plan |
|---|---|---|
| Tespit kaynakları (health/log) | ⚠️ | `/platform/health` + log; audit ⏳31B |
| Müdahale planı | ❌→✅ | [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) |
| Delil toplama (audit/log) | ⏳ | P0-2 sonrası güçlenir |

---

## B. İdari tedbirler

| Tedbir | Statü | Kanıt / Plan |
|---|---|---|
| Veri envanteri | ✅ | [DATA_INVENTORY.md](DATA_INVENTORY.md) |
| Yetki matrisi | ✅ | Bu doküman A.1 |
| Aydınlatma metni ihtiyacı | ❌ ⏳ (avukat) | Öğrenci kayıt/üyelik aşamasında zorunlu; metin avukat onayı |
| Açık rıza ihtiyacı (özel nitelikli) | ❌ ⏳ (avukat) | HealthNotes tutulacaksa açık rıza; aksi halde toplama (P1-8) |
| Çerez metni ihtiyacı | ⚠️ TBD | Frontend yalnız fonksiyonel storage (`pf_active_studio`, sidebar) kullanıyor; analytics yoksa çerez metni minimal — analytics eklenirse gerekir |
| Saklama & silme politikası | ✅ taslak | [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md) |
| Veri işleyenler listesi | ✅ taslak | [DATA_PROCESSORS.md](DATA_PROCESSORS.md) (TBD'ler dahil) |
| İhlal müdahale prosedürü | ✅ | [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) + [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) |
| Çalışan/işletme gizlilik taahhüdü | ❌ ⏳ (öneri) | Stüdyo personeli için gizlilik taahhütnamesi şablonu; veri işleyen sıfatıyla stüdyo-platform sözleşmesi (avukat) |
| Erişim yetkisi gözden geçirme | ❌ ⏳ (öneri) | Periyodik (örn. 6 ay) membership/rol gözden geçirme |
| VERBİS kayıt yükümlülüğü değerlendirmesi | ⚠️ TBD (avukat) | Eşik/istisna avukatça değerlendirilmeli |

---

## C. Sorumluluklar (RACI — TBD)

| Rol | Sorumluluk | Kişi |
|---|---|---|
| Veri sorumlusu (işletme) | KVKK uyum, Kurul bildirimi | **TBD — owner** |
| Teknik güvenlik sorumlusu | Teknik tedbir uygulama | **TBD** |
| KVKK avukatı | Hukuki metin onayı | **TBD** |
| İhlal müdahale koordinatörü | Incident yönetimi | **TBD** ([INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md)) |

> İsim/iletişim alanları owner tarafından doldurulacaktır (uydurma bilgi yazılmamıştır).
