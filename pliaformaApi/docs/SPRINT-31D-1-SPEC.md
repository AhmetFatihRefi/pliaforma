# Sprint 31D-1 — Retention Purge + DB-level Append-only

Statü: **ONAYLANDI — uygulama** · Zemin: …31C-2→v1.17 · HEAD `8d9670a` · Tag (yeşilse): **v1.18.0-pilot** · **Migration VAR** (trigger).

> 31D üçe bölündü; bu sprint **yalnız 31D-1**. 31D-2 (backup encryption + restore drill) ve 31D-3 (CI scans + incident tabletop)
> AYRI sprintler. **Kapsam dışı (31D-1):** backup encryption · restore drill · CI scans · incident tabletop · CSP report-to ·
> Notification purge · business-data purge/anonymization · backup retention · ayrı DB maint rolü/REVOKE · product feature.

## Onaylanan kararlar
- **Append-only: trigger + GUC bypass.** SecurityAuditLogs'ta `BEFORE UPDATE OR DELETE` trigger: **UPDATE her zaman blocked**; **DELETE yalnız** `current_setting('app.audit_purge', true) = 'on'` ise allowed. Purge worker kendi transaction'ında `SET LOCAL app.audit_purge='on'` set edip siler. **Dürüst not (docs'a yazılacak):** bu, kaza/bug/normal app update-delete akışına karşı **DB-level guard**'dır; **tam-compromised app'e karşı mutlak koruma DEĞİLDİR** (app GUC set edebilir).
- **Purge: in-app `BackgroundService`** (ReminderBackgroundService deseni: enable flag, interval, scoped DbContext, hata izolasyonu, app'i düşürmez, loglar). Ops cron/script YOK.
- **Dry-run default true.** Dev: `AuditPurge:Enabled=false`. Prod: env ile açılır; **ilk deploy `Enabled=true` + `DryRun=true`** (gözlem), gerçek silme ancak `DryRun=false` ile. Yanlışlıkla destructive purge çalışmaz.
- **Kapsam:** SecurityAuditLog 2yr + AuthToken/RefreshToken expired/revoked+buffer hard-delete.

## A. Migration `Sprint31D_AuditAppendOnly`
Model değişikliği YOK → boş migration + `migrationBuilder.Sql(...)`. `has-pending` clean kalır.
- **Up:** `CREATE FUNCTION pliaforma_audit_append_only() RETURNS trigger` — `TG_OP='UPDATE'` → `RAISE EXCEPTION`; `TG_OP='DELETE'` → `current_setting('app.audit_purge', true) = 'on'` değilse `RAISE EXCEPTION`, değilse `RETURN OLD`. + `CREATE TRIGGER trg_audit_append_only BEFORE UPDATE OR DELETE ON "SecurityAuditLogs" FOR EACH ROW EXECUTE FUNCTION pliaforma_audit_append_only()`.
- **Down:** `DROP TRIGGER` + `DROP FUNCTION`.
- **Dikkat:** veri değişmez · **INSERT serbest** (trigger yalnız UPDATE/DELETE) · audit logger çalışmaya devam eder · identifier casing EF ile birebir (`"SecurityAuditLogs"`, quoted) · apply + down/up test edilir.

## B. Purge worker `AuditRetentionPurgeBackgroundService`
Config `AuditPurge`: `Enabled`(dev false) · `DryRun`(default true) · `IntervalMinutes`(vars. 1440, min clamp) · `BatchSize`(vars. 5000) · `AuditRetentionDays`(730) · `TokenRetentionBufferDays`(vars. 7).
Davranış: `Enabled=false`→çalışmaz · `DryRun=true`→silmez, kaç kayıt silineceğini **sayar+loglar** · `AuditRetentionDays<=0`→**refuse** (yeni kayıt koruması) · cutoff **run başında sabit** (`now-Days`) · **batched** raw DELETE `… WHERE "Id" IN (SELECT "Id" … LIMIT @batch)` döngü (max-iter guard) · exception app'i düşürmez (log+devam) · her run loglanır.

## C. Purge hedefleri (fiziksel silme — soft-delete/interceptor + query filter bypass)
- **SecurityAuditLogs:** `OccurredAt < auditCutoff`. Silme: **her batch kendi kısa transaction'ında** `SET LOCAL app.audit_purge='on'` + batched DELETE. Normal DELETE (GUC yok) trigger'la engellenir; dry-run yalnız COUNT (GUC gerekmez).
- **AuthToken:** `ExpiresAt < tokenCutoff` (expired-beyond-buffer; aktif/ileri-tarihli token MATCH ETMEZ). Raw DELETE (BaseEntity soft-delete'i bypass → fiziksel).
- **RefreshToken:** `ExpiresAt < tokenCutoff OR (RevokedAt IS NOT NULL AND RevokedAt < tokenCutoff)`. Aktif (RevokedAt null + ExpiresAt ileri) MATCH ETMEZ. Raw fiziksel DELETE.
- Token cutoff = `now - TokenRetentionBufferDays`. **Aktif token silinmez.**

## D. `AuditRetentionPurge` event
`SecurityAuditEventType` + `AuditRetentionPurge` (string, migration yok). Run başına **en fazla 1** meta event (gerçek silme turunda; dry-run'da da ops. yazılabilir, `dryRun=true` ile). Detail = `cutoff=…; dryRun=…; audit=N; authToken=N; refreshToken=N` — **PII yok**. **Sonsuz döngü yok:** purge audit yazmak purge tetiklemez; yeni event `OccurredAt=now > cutoff` → aynı turda silinmez. ILogger **birincil** kanıt; audit event opsiyonel-önerilir. (INSERT trigger'a takılmaz.)

## E. Smoke `scripts/audit-purge-smoke.sh`
Eski + yeni SecurityAuditLog satırı (psql ile OccurredAt manipüle) · DryRun=true→hiçbir şey silinmez · DryRun=false→yalnız eski audit silinir · normal `UPDATE`/`DELETE` SecurityAuditLogs → **DB trigger blocked** (hata) · purge bypass DELETE allowed · **INSERT audit hâlâ çalışır** · AuthToken/RefreshToken expired/revoked **hard-delete** · **aktif token silinmez** · AuditRetentionPurge event/log oluşur · purge kendi yeni event'ini silmez · has-pending clean · migration down trigger'ı kaldırır.
(Worker'ı senkron tetiklemek için: ya kısa `IntervalMinutes` + `DryRun` toggle ile bekle, ya da test-only manuel tetik endpoint — reminder deseni gibi. Tercih: smoke psql ile trigger/bypass + token delete'i doğrudan doğrular; worker davranışı `Enabled=true,DryRun=false,Interval kısa` stack'inde gözlenir.)

## F. Docs
`SPRINT-31D-1-SPEC.md` · `RETENTION_AND_DELETION_POLICY.md` (purge UYGULANDI) · `RISK_REGISTER` (P1-3 kısmi) · `SECURITY_BASELINE_CHECKLIST` · `SECURITY_GAP_ANALYSIS`. **Açıkça:** DB-level append-only trigger accidental/normal app update/delete'i engeller; purge için kontrollü GUC bypass; **tam-compromised app'e karşı mutlak değil**; 2yr retention; ilk deploy dry-run=true önerilir.

## H. Doğrulama
backend 0/0 · tsc 0 · next build 0 · migration apply · down/up test · has-pending clean · audit-purge-smoke · security-audit-log smoke · audit-coverage smoke · MFA smoke · 31B-2 smoke · metrics-exposure · full smoke 38/38 · full E2E (mevcut sayı).

## Rapor (bitişte)
değişen dosyalar · migration adı/içeriği · trigger kanıtı · normal update/delete blocked kanıtı · purge bypass allowed kanıtı · dry-run kanıtı · gerçek purge kanıtı · token purge kanıtı · AuditRetentionPurge log/event kanıtı · migration down/up kanıtı · build/test · açık riskler · commit/push/tag yapılmadı kanıtı.
