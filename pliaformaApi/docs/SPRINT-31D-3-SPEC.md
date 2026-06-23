# Sprint 31D-3 — CI Security Scans + Incident Tabletop

Statü: **ONAYLANDI — uygulama** · Zemin: …31D-2→v1.19 · HEAD `2d2713f` · Tag (yeşilse): **v1.20.0-pilot**
**App kodu YOK · Migration YOK · Dockerfile YOK** — yalnız CI workflow + config + ops script + docs. Tek sprint.

> Hedef: Repo'ya secret sızıntısı / kritik güvenlik sorunu girmesini CI'da erken yakalamak + KVKK incident tabletop drill.

## Onaylanan kararlar
1. **gitleaks: full-repo history + dar allowlist.** Yalnız bilinen düşük-değerli dev/test değerleri (`Password=postgres`, `Admin!2345`, `Parola123!`, `E2ePass123!`, `CHANGE_ME_*`) regex ile allowlist; **blanket/path-geniş allowlist YOK**. Gerçek prod secret bulunursa allowlist'e ALINMAZ → ayrı incident/rotation/history-cleanup işi (bu sprintte commit edilmez).
2. **Action pinning:** resmi `actions/{checkout,setup-dotnet,setup-node,upload-artifact}@v4` (major) kabul; 3rd-party (gitleaks/trivy/cyclonedx) **pinned release/container**; **`gitleaks-action` KULLANILMAZ** (lisans + 3rd-party-action yüzeyi) → binary/container.
3. **Triggers:** `push` (main) + `pull_request` + `workflow_dispatch` + **haftalık `schedule`** (CVE drift).
4. **Trivy:** yalnız **`fs`-scan** (image build YOK).
5. **npm audit:** `--omit=dev --audit-level=high` (shipped deps).
6. **SBOM:** dotnet + npm → tek artifact klasörü, `upload-artifact`, advisory.
7. **Local wrapper:** tool yoksa **skip + warning** (hard-fail değil); CI araçları kurar.
8. **Fail policy:** **gitleaks tek BLOCKING**; npm/dotnet/trivy/SBOM **advisory** (step summary + artifact).
9. **Tabletop senaryo:** "çalınan ŞİFRELİ prod DB yedeği" (31D-2 bağı: yedek gpg-şifreli + key ayrı → maruziyet azalır ama olay ciddiye alınır, karar kaydı gerekir).

## A. `.github/workflows/security.yml`
- Triggers: push(main) + pull_request + workflow_dispatch + schedule(haftalık cron). `permissions: contents: read` (top-level, minimal).
- **Jobs:** `secret-scan` (gitleaks pinned container, `--redact --config .gitleaks.toml --exit-code 1`, **BLOCKING**) · `deps-dotnet` (restore sln + `dotnet list package --vulnerable --include-transitive` → step summary, advisory) · `deps-npm` (`npm ci` + `npm audit --omit=dev --audit-level=high` → summary, advisory) · `trivy-fs` (`trivy fs`, advisory, image build yok) · `sbom` (dotnet CycloneDX + npm cyclonedx → upload-artifact, advisory).
- working-directory: dotnet→`pliaformaApi/`, npm→`pliaformaFrontend/`, gitleaks/trivy→kök.

## B. `.gitleaks.toml`
- `[extend] useDefault = true` + **dar regex allowlist** (yalnız yukarıdaki bilinen değerler; exact-value → gerçek secret'ı gizlemez). Baseline **TEMİZ** olmalı.
- gitleaks gerçek secret bulursa: **commit edilmez**, raporlanır (ayrı iş).

## C. `scripts/security-scan.sh`
- gitleaks · npm audit · `dotnet list package --vulnerable` · trivy fs · SBOM — CI ile **aynı komutlar**.
- Tool yoksa **skip + WARNING**. gitleaks bulgu → **non-zero**; advisory'ler default 0. **`--strict`** ile advisory'ler de fail.

## D. `pliaformaApi/docs/security/INCIDENT_TABLETOP_DRILL.md`
Mock: **"Çalınan şifreli prod DB yedeği"**. İçerik: senaryo özeti · SEV (SEV-1/2) · 0–72h timeline (IRP'ye **atıf**) · RACI (IRP TBD rollerine) · ilk izolasyon · delil toplama · **audit log kontrolü** (append-only = birincil delil) · **backup encryption/key ayrılığı değerlendirmesi** (key ayrı → maruziyet düşük) · etkilenen veri kategorileri (DATA_INVENTORY: kimlik/iletişim/**sağlık**/finans) · **KVKK bildirim eşiği değerlendirmesi** (karar kaydı) · Kurul + ilgili-kişi bildirim hazırlık (breach runbook **atıf**) · delil zinciri · karar kayıtları · **tabletop sonuç formu** · post-incident aksiyonlar → RISK_REGISTER. **Kopyalama YOK; atıf + somutlaştırma.** IRP/breach-runbook/DATA_INVENTORY/DATA_PROCESSORS/CHECKLIST ile çelişmez.

## E. Docs
`SECURITY_BASELINE_CHECKLIST.md` (CI scan ✅) · `RISK_REGISTER.md` (P1-CI/P1-9 kapanış) · `INCIDENT_RESPONSE_PLAN.md` §7 (tabletop → uygulandı linki) · bu spec. (Ops.) `scripts/ci-security-smoke.sh`.

## F. Kapsam dışı
app kodu · migration · backup/retention/append-only değişikliği · CSP report-to · product feature · gerçek secret/key · **history rewrite/BFG/filter-repo**. gitleaks gerçek secret bulursa → raporla, commit etme, rotation/cleanup ayrı karar.

## G. Doğrulama
workflow YAML parse (actionlint/docker veya yaml-parse) · **gitleaks repo scan (baseline TEMİZ)** · gitleaks **test fixture** ile gate çalışıyor (fixture commit edilmez) · npm audit çalışıyor · dotnet vulnerable çalışıyor · trivy fs çalışıyor · SBOM artifact üretiliyor · `security-scan.sh` bash -n + dry-run (tool-missing skip+warn) · has-pending clean · backend 0/0 (raporla) · tabletop çelişki yok. Full smoke/E2E **gerekmez** (app değişmez).

## Rapor (bitişte)
değişen dosyalar · workflow özeti · gitleaks baseline sonucu · allowlist özeti · advisory scan sonuçları · SBOM kanıtı · tabletop özeti · docs · has-pending clean · app code/migration yok · gerçek secret/key yok · commit/push/tag yapılmadı.
