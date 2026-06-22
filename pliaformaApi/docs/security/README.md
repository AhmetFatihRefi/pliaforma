# Pliaforma — Güvenlik & KVKK Kanıt Dosyaları (Sprint 31A)

Bu klasör, Pliaforma'nın **kişisel veri güvenliği** ve **KVKK teknik/idari tedbir** kanıtlarını içerir.
Amaç: bir veri ihlali veya yetkisiz erişim durumunda *"makul teknik ve idari tedbirleri aldık,
riskleri analiz ettik, kanıtlarımız var, ihlal prosedürümüz var"* diyebilmek.

> Bu dokümanlar **hukuki danışmanlık değildir**. Aydınlatma metni, açık rıza metni ve veri işleyen
> sözleşmeleri ayrıca bir KVKK avukatı tarafından gözden geçirilmelidir. Bu klasör teknik/operasyonel
> hazırlığı belgeler.

## İçindekiler

| Doküman | Amaç |
|---|---|
| [DATA_INVENTORY.md](DATA_INVENTORY.md) | Kişisel veri envanteri: hangi entity hangi veriyi tutuyor, hassasiyet, erişim, amaç, retention, audit ihtiyacı |
| [SECURITY_GAP_ANALYSIS.md](SECURITY_GAP_ANALYSIS.md) | Mevcut durum vs. eksik vs. öneri — kanıtlı gap analizi |
| [RISK_REGISTER.md](RISK_REGISTER.md) | P0/P1/P2 risk listesi; her risk hangi sprintte kapatılacak |
| [KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md](KVKK_TECHNICAL_ADMINISTRATIVE_MEASURES.md) | KVKK teknik ve idari tedbir checklist'i |
| [INCIDENT_RESPONSE_PLAN.md](INCIDENT_RESPONSE_PLAN.md) | İhlal tespiti, karar, ilk 24 saat, kök neden, kapanış |
| [BREACH_NOTIFICATION_RUNBOOK.md](BREACH_NOTIFICATION_RUNBOOK.md) | 72 saat Kurul bildirimi + ilgili kişi bildirimi şablonları |
| [RETENTION_AND_DELETION_POLICY.md](RETENTION_AND_DELETION_POLICY.md) | Saklama/silme/anonimleştirme politikası |
| [DATA_PROCESSORS.md](DATA_PROCESSORS.md) | Veri işleyenler / alt-işleyenler listesi (bilinmeyenler TBD) |
| [SECURITY_BASELINE_CHECKLIST.md](SECURITY_BASELINE_CHECKLIST.md) | Pilot öncesi go/no-go güvenlik checkbox listesi |
| [BACKUP_AND_RESTORE_RUNBOOK.md](BACKUP_AND_RESTORE_RUNBOOK.md) | Yedekleme/geri yükleme; mevcut runbook'a referans + güvenlik ekleri |

## Mevcut (çoğaltılmayan) dokümanlar — referans

Bu klasör aşağıdaki **mevcut** dokümanları çoğaltmaz; üzerine inşa eder ve link verir:

- [../SECURITY-REVIEW.md](../SECURITY-REVIEW.md) — Sprint 12 güvenlik denetimi (kimlik, tenant izolasyonu, ağ)
- [../BACKUP-RESTORE.md](../BACKUP-RESTORE.md) — yedek/geri yükleme prosedürü ve cron örneği
- [../PILOT-CHECKLIST.md](../PILOT-CHECKLIST.md) — pilot go-live checklist
- [../PILOT-RUNBOOK.md](../PILOT-RUNBOOK.md) — pilot operasyon runbook'u
- [../PILOT-DEPLOY-DRY-RUN.md](../PILOT-DEPLOY-DRY-RUN.md) — TLS kurulumu dahil deploy provası
- [../RELEASE-CHECKLIST.md](../RELEASE-CHECKLIST.md) — release gate
- [../TECH-DEBT.md](../TECH-DEBT.md) — bilinen teknik borç (CSP, 2FA, audit log vb. ertelenenler)

## Sprint planı (özet)

- **Sprint 31A** *(bu klasör)* — Evidence Baseline: kod yok, sadece kanıt/doküman.
- **Sprint 31B** — App Security Hardening: TLS/HSTS/CSP, /metrics+Grafana kısıtı, secret/seed, hesap kilitleme + parola politikası, login audit.
- **Sprint 31C** — Authorization & Audit Evidence: SecurityAuditLog altyapısı + hassas eylem kapsamı, tenant isolation + object-level otomatik regresyon testleri.
- **Sprint 31D** — Backup/Secrets/Dependency & Incident Drill: backup şifreleme, retention/token purge job, CI güvenlik taraması (gitleaks, npm audit, dotnet vulnerable, CodeQL/Semgrep, Trivy, CycloneDX SBOM), incident tabletop drill.
- **Sprint 31-MFA** *(ayrı mini-sprint)* — Owner/Admin MFA: Owner/StudioAdmin için TOTP MFA (P1-10). 31B'yi şişirmemek için ayrıldı; Instructor/Student MFA post-pilot (P2).

> **Not (sprint numaralandırma):** `Sprint 30A–30E` geçmiş feature işleridir; güvenlik fazları çakışmayı
> önlemek için **31A–31D** olarak numaralandırılmıştır.

_Son güncelleme: Sprint 31A. Statü: TASLAK — owner/KVKK sorumlusu onayı bekliyor._
