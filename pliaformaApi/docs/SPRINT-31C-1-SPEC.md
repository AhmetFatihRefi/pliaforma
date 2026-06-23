# Sprint 31C-1 — Audit Coverage + Audit Viewer + Tenant Isolation Regression

Statü: **ONAYLANDI — uygulama** · Zemin: 31A→v1.12, 31B-1→v1.13, 31B-2→v1.14, 31-MFA→v1.15 · HEAD `ef20bd8`
Tag önerisi (yeşilse): **v1.16.0-pilot**

> 31C **ikiye bölündü**: **31C-1** (bu doküman — audit kapsam + viewer + tenant izolasyon, **migration YOK**, düşük risk) ve
> **31C-2** (CSP enforce + nonce + report-sink — ayrı, yüksek risk). Bu sprintte **yalnız 31C-1**.

## Kapsam dışı (bu sprintte YAPILMAYACAK)
CSP enforce · nonce middleware · CSP report-sink · audit CSV export · retention/purge · DB-level append-only (REVOKE/trigger) ·
backup encryption · restore drill · CI security scans · incident tabletop · ürün feature değişikliği.
→ Bunların hepsi **31D** veya **31C-2** kapsamında. (Retention purge, DB append-only, backup encryption, restore drill, CI scans = **31D**.)

## Migration
**YOK.** Yeni `SecurityAuditEventType` değerleri enum→**string** kolonunda saklanır (`HasConversion<string>`, maxlen 40);
`SecurityAuditLog.TargetType/TargetId` zaten mevcut; viewer indeksleri (`OccurredAt`, `(ActorUserId,OccurredAt)`,
`(StudioId,OccurredAt)`, `EventType`) zaten mevcut. → **migration üretilmeyecek, `has-pending clean` korunacak.**

---

## Onaylanan kararlar (1–9)
1. **Audit viewer CSV export:** 31C-1'de YOK; viewer **salt-okunur**. Export → 31D (export yetkisi + export-audit + download güvenliği ayrı ele alınacak).
2. **HealthNotes/export audit granülerliği:** **istek-başı tek event** (kayıt-başı YOK). Ör. öğrenci listesinde health gerçekten döndüyse `HealthNotesAccessed Detail="count=23"`; bulk/list (payments/instructor report) `StudentDataListed Detail="count=..."`. **PII/sağlık metni/tutar yazılmaz** — yalnız count/action/targetType gibi maskeli minimal detail.
3. **Okuma audit kapsamı:** bulk/list öğrenci verisi + HealthNotes gerçekten dönen listeler + tekil `GetStudent` (health gerçekten gösteriliyorsa) + `GetMyStudentProfile` (öğrenci kendi sağlık notunu görüyorsa — actor = öğrencinin kendisi). **Kural:** `HealthNotesAccessed` yalnız (a) `CanSeeHealth==true`, (b) response'ta health alanı **gerçekten dolu/dönüyor**, (c) request başına **tek** event. Boş health için audit YAZMA (gürültü yaratma).
4. **Üyelik yaşam döngüsü:** hepsi audit → `MemberInvited`, `MembershipAccepted`, `MembershipRejected`, `MemberRemoved`, `RoleChanged`. Detail: rol değişiminde eski→yeni rol yazılabilir; **ham e-posta yazılmaz** (gerekiyorsa maskeli); kişisel veri minimum.
5. **CSP yolu:** 31C-1'de CSP enforce YOK. 31C-2'de **nonce** (hash değil), Next middleware/nonce mimarisi.
6. **CSP enforce stratejisi:** 31C-2'de önce report-sink → staged enforce → doğrudan enforce yok → full E2E gate zorunlu. 31C-1'de yalnız mevcut Report-Only korunur; **CSP dosyalarına gereksiz dokunulmaz.**
7. **Tenant isolation test:** backend smoke **zorunlu** + **1 E2E**.
8. **Retention/purge:** 31C-1'de YOK → 31D (2 yıl purge job, DB append-only REVOKE/trigger, backup encryption, restore drill, CI scans).
9. **Viewer'da studio adı:** EVET, `Studios` join ile göster. **Ama:** studio scoping uygulanmaz; viewer **PlatformAdmin global** görünüm; current studio ile filtrelenmez; `studioId` query filtresi **opsiyonel**; studio name yalnız UX.

---

## A. Audit event genişletme
`SecurityAuditEventType` (Domain/Enums/SecurityAuditEnums.cs) yeni değerler (string, ≤40 char), mevcut 0–13'ten sonra:
`RoleChanged, MemberInvited, MembershipAccepted, MembershipRejected, MemberRemoved, PaymentCreated, PaymentUpdated, PaymentMarkedPaid, PaymentCancelled, ContractCreated, ContractAmended, ContractCancelled, StudentDataListed, HealthNotesAccessed`. **Migration yok.**

## B. Audit yazılacak handler'lar (inline `ISecurityAuditLogger`, mevcut auth/MFA deseni)
- **Rol/üyelik:** `Members/ChangeMemberRole.cs` (RoleChanged), `InviteMember.cs` (MemberInvited), `DecideMembership.cs` (MembershipAccepted/Rejected), `RemoveMember.cs` (MemberRemoved).
- **Ödeme:** `Payments/PaymentFeature.cs` — Create/Update/MarkPaid/Cancel.
- **Sözleşme:** `Contracts/StudentContractFeature.cs` (Create/Cancel), `ContractAmendment.cs` (Add→ContractAmended).
- **Okuma:** `Students/GetStudents.cs` (StudentDataListed + health döndüyse HealthNotesAccessed), `GetStudent.cs` (health gösteriliyorsa HealthNotesAccessed), `GetMyStudentProfile.cs` (kendi sağlık notu), `Reports/PaymentsReport.cs` + `Reports/InstructorsReport.cs` (StudentDataListed).

**Kural:** yazma audit'i **SaveChanges SONRASI**; `TargetType`/`TargetId` dolu; `Detail` minimal/maskeli; **tutar değeri, health metni, ham e-posta, token/secret YAZILMAZ**. Audit best-effort (akışı kırmaz).

## C. Audit viewer API
`GET /api/platform/security-audit` — `[Authorize(Policy = AuthPolicies.PlatformAdmin)]`.
Query: `from, to, eventType, actorUserId, studioId, outcome, page, pageSize`. Default newest-first, `pageSize` cap'li (ör. 20, max 100).
Yeni `GetSecurityAuditLogsQuery` (IQuery) → `SecurityAuditLogs` (BaseEntity değil; soft-delete yok). **Studio scoping UYGULANMAZ** (PlatformAdmin global). `Studios` left-join → `StudioName` (UX). Salt-okunur, export yok. PII zaten yazımda maskeli; viewer ek işlem yapmaz.

## D. Audit viewer UI
`(platform)/security-audit` — tablo + filtreler (tarih aralığı, event type dropdown, actor arama, studio filtresi, outcome) + sayfalama + **studio adı** gösterimi. Salt-okunur. PlatformAdmin nav'a eklenir.

## E. Tenant isolation regression
**Zorunlu** backend smoke `scripts/tenant-isolation-smoke.sh`:
- Studio A owner + Studio B owner seed (her birinde student/payment/contract).
- A-owner token ile B student/payment/contract **GET → 404/boş**; B write/cancel/amend **→ 404**.
- A-owner `switch-studio B` (üyelik yok) **→ 403**.
- `mfa_pending` token ile cross-tenant **→ 403** (middleware).
- `role-permission-smoke.sh` regresyon olarak yeşil kalır.

**1 E2E** `tests/e2e/18-tenant-isolation.spec.ts`: başka stüdyonun nesne URL'sine doğrudan gidince **bulunamadı/erişim yok**; UI leakage yok.

## F. Audit coverage smoke
`scripts/audit-coverage-smoke.sh`: RoleChanged, MemberInvited, MembershipAccepted/Rejected, MemberRemoved, PaymentCreated/Updated/MarkedPaid/Cancelled, ContractCreated/Amended/Cancelled, StudentDataListed, HealthNotesAccessed **DB'de yazıldı**; audit kayıtlarında **tutar/sağlık metni/token/secret YOK**; **ham e-posta YOK**; gereken yerlerde **TargetType/TargetId dolu**; PlatformAdmin viewer **erişebilir**; Owner/StudioAdmin/Instructor/Student viewer'a **erişemez (403)**.

## G. Doğrulama (hepsi çalıştırılacak)
backend build 0/0 · frontend tsc 0 · next build 0 · **has-pending clean** · full smoke suite · tenant-isolation-smoke · audit-coverage-smoke · role-permission-smoke · security-audit-smoke · metrics-exposure-smoke · MFA smoke (4) · 31B-2 smoke (5) · **full E2E (mevcut sayı + 1 yeni tenant = 61)**.

## H. Açık riskler (peşinen)
- Inline audit 12+ handler'a dokunur → regresyon için full smoke+E2E kapısı.
- Okuma audit gürültüsü → istek-başı tek event + sayım ile sınırlandı.
- Viewer'ın yanlış studio-scope'lanması / yetkisiz erişim → policy + smoke ile doğrulanır.
- Tenant smoke 2-stüdyo seed çakışması → izole e-postalar/temiz seed.

## Rapor (bitişte)
değişen dosyalar · migration yok kanıtı · has-pending clean kanıtı · audit event coverage kanıtı · PII sızıntısı yok kanıtı ·
viewer API/UI kanıtı · tenant isolation smoke kanıtı · E2E kanıtı · build/test sonuçları · açık riskler · commit/push/tag yapılmadı kanıtı.
