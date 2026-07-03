# AGENTS.md — Pliaforma Agent Working Protocol

This repository follows a strict agentic SDLC workflow.

Every agent must read and follow this file before making any change.

This document defines how Builder, Reviewer, and Release/Deploy agents must work in this repository.

---

# 1. Core Principle

Pliaforma is a live production pilot application.

Therefore:

- Do not rush.
- Do not guess.
- Do not silently change unrelated code.
- Do not commit without explicit human approval.
- Do not deploy without explicit human approval.
- Do not touch production secrets, certificates, backups, or database unless the task explicitly says so.

The default mode is:

```text
Analyze → Implement → Validate → Report → Wait for approval
```

Not:

```text
Implement → Commit → Push → Deploy
```

---

# 2. Core Workflow

All feature, bugfix, polish, CI/CD, or deploy work must follow this flow:

```text
1. Sprint specification
2. Builder implementation
3. Builder report
4. Independent Reviewer/Auditor review
5. Human Gatekeeper decision
6. Explicit commit/push/tag approval
7. Optional production deploy approval
```

No agent may skip these steps.

---

# 3. Roles

## 3.1 Builder / Implementer Agent

The Builder implements the approved sprint specification.

### Builder Rules

The Builder must:

- Read the full sprint spec before editing.
- Analyze the existing implementation before changing code.
- Make the smallest safe change that satisfies the spec.
- Stay strictly within scope.
- Avoid broad refactors unless explicitly requested.
- Preserve existing behavior unless the spec explicitly changes it.
- Run relevant validation before reporting.
- Report honestly if something was not tested.

The Builder must not:

- Commit.
- Push.
- Tag.
- Deploy.
- SSH into production.
- Touch production database.
- Touch real secrets.
- Touch cert files.
- Touch backup files.
- Use `git add .`.
- Stage `CLAUDE.md`.
- Stage unrelated local files.
- Create migrations unless the spec requires it.
- Claim “done” without validation.

### Builder Report Format

Every Builder report must include:

```text
1. Summary
2. What changed?
3. Files changed
4. Migration status
5. Backend impact
6. Frontend impact
7. Security impact
8. Production impact
9. Validation results
10. Risks / known limitations
11. Out-of-scope confirmation
12. Suggested commit message
13. Suggested tag, if applicable
14. Explicit statement: commit/push/tag not performed
```

---

## 3.2 Reviewer / Auditor Agent

The Reviewer independently audits the Builder’s work.

The Reviewer must behave as a skeptical auditor, not as a second implementer.

### Reviewer Rules

The Reviewer must:

- Read the sprint spec.
- Read the Builder report.
- Inspect `git diff`.
- Verify changed files are within scope.
- Re-run relevant validation where possible.
- Try to find real defects.
- Be strict.
- Give `GO` only when evidence supports it.

The Reviewer must not:

- Implement fixes in the first review pass.
- Commit.
- Push.
- Tag.
- Deploy.
- SSH into production.
- Touch production database.
- Touch secrets/certs/backups.
- Give GO based only on the Builder’s claims.

### Reviewer Must Check

```text
1. Spec compliance
2. Scope creep
3. Changed files
4. Migration safety
5. Auth/security impact
6. Tenant isolation impact
7. Production env/secret/cert/backup safety
8. Hardcoded secrets
9. Fake public claims
10. UI/UX correctness
11. Edge cases
12. Rollback risk
13. Validation quality
14. Smoke coverage
15. CI impact
```

### Reviewer Report Format

Every Reviewer report must include:

```text
1. GO / NO-GO decision
2. Critical findings
3. High / Medium / Low findings
4. Spec compliance table
5. Changed file analysis
6. Migration analysis
7. Security analysis
8. Validation results
9. Missing tests or smoke checks
10. Commit/tag readiness
11. If NO-GO: exact fixes required
12. If GO: suggested commit/tag wording
```

If unsure, the Reviewer must choose:

```text
NO-GO
```

---

## 3.3 Human Gatekeeper

The human owner is the final decision maker.

Agents cannot decide to commit, push, tag, or deploy without explicit human approval.

Accepted approval phrases must be explicit, for example:

```text
Commit + push onayı veriyorum.
vX.Y.Z-pilot tag onayı veriyorum.
Production deploy onayı veriyorum.
```

Without explicit approval:

```text
No commit.
No push.
No tag.
No deploy.
```

---

# 4. Git Rules

## 4.1 Before Any Commit Proposal

Always show:

```bash
git status --short
git diff --name-only
git diff --cached --name-only
```

## 4.2 Staging Rules

Never use:

```bash
git add .
```

Always stage files explicitly.

Example:

```bash
git add path/to/file1 path/to/file2
```

## 4.3 Never Stage These Unless Explicitly Requested

```text
CLAUDE.md
tanitim.html
.env
.env.*
deploy/.env
cert files
private keys
backup files
SQL dumps
database exports
.next
node_modules
build output
dist
coverage
logs
temporary files
unrelated files
```

## 4.4 Commit Message Rules

Commit messages must be clear and include the sprint number when applicable.

Examples:

```text
feat: add trusted device (remember me) for MFA (Sprint 50)
ci: add build validation pipeline (Sprint 51)
feat: real landing, contact form, sidebar icons and SEO baseline (Sprint 52)
```

No co-author trailers unless explicitly requested.

---

# 5. Tag Rules

Use tags only for deployable application changes.

CI-only or documentation-only changes do not need a tag unless explicitly approved.

Tags must be annotated.

Example:

```bash
git tag -a v1.42.0-pilot -m "v1.42.0-pilot: landing, contact, sidebar icons and SEO baseline"
git push origin v1.42.0-pilot
```

## Current Tag Pattern

```text
vX.Y.Z-pilot
```

## Tag Rules

- Patch hotfix: `v1.40.1-pilot`
- Feature/pilot release: `v1.41.0-pilot`, `v1.42.0-pilot`
- Do not move existing tags.
- Do not retag old commits.
- Verify tag target after creation.

Required final tag report:

```text
1. Tag object hash
2. Tag target commit
3. Tag push result
4. Previous tag unchanged confirmation
```

---

# 6. Production Context

## 6.1 Production Server

```text
Host: pliaforma-pilot-01
Provider: Radore VPS
Path: /opt/pliaforma
Main domain: https://pliaforma.com
```

## 6.2 Canonical Host

Production uses same-origin architecture.

Canonical host:

```text
https://pliaforma.com
```

These hosts redirect to canonical:

```text
https://www.pliaforma.com  → https://pliaforma.com
https://app.pliaforma.com  → https://pliaforma.com
https://api.pliaforma.com  → https://pliaforma.com
```

Do not split frontend and API into separate origins unless a future sprint explicitly redesigns cookie/CORS/session behavior.

## 6.3 Same-Origin Auth Constraint

The application relies on strict same-origin cookie behavior.

Do not weaken:

```text
SameSite=Strict
HttpOnly auth cookies
host-only cookie assumptions
/api auth path behavior
```

Do not introduce cross-origin auth without explicit design review.

---

# 7. Production Deploy Rules

Production deploy requires explicit approval.

Never run:

```bash
docker compose down -v
```

Never delete the Postgres volume.

Never delete production backups.

Never overwrite production `.env`.

Never expose production `.env` content in reports.

## 7.1 Standard Production Deploy Flow

For approved deployable tags:

```bash
cd /opt/pliaforma

git fetch --tags
git checkout vX.Y.Z-pilot

docker compose --env-file deploy/.env -f docker-compose.prod.yml up -d --build backend frontend
```

Only rebuild services that need rebuilding.

Do not rebuild or recreate Postgres unless explicitly required.

Nginx should not be changed unless the sprint includes nginx changes.

## 7.2 Before Backend/Migration/Data-Impacting Deploy

Always:

```text
1. Confirm registration lock is false
2. Take encrypted DB backup
3. Confirm backup file ends with .sql.gz.gpg
4. Run restore drill if migration/data risk exists
5. Deploy approved tag
6. Verify health
7. Verify registration lock remains false
```

## 7.3 Registration Lock Check

Before and after deploy:

```bash
curl -sS https://pliaforma.com/api/auth/registration-status | jq
```

Expected production value:

```json
{
  "success": true,
  "data": {
    "studioRegistrationEnabled": false
  }
}
```

Public studio registration must remain disabled in production unless explicitly opened for a short controlled onboarding window.

---

# 8. Backup and Restore Rules

Production database backups must be encrypted.

Backup script:

```bash
ops/backup-postgres.sh
```

Restore drill script:

```bash
ops/restore-drill.sh
```

## 8.1 Backup Command Pattern

```bash
cd /opt/pliaforma

set -a
. deploy/.env
set +a

PG_CONTAINER=pliaforma-prod-postgres-1 \
ASPNETCORE_ENVIRONMENT=Production \
bash ops/backup-postgres.sh ops/backups

chmod 600 ops/backups/*.gpg
ls -lh ops/backups
```

Expected output:

```text
ENCRYPTED (gpg AES256)
.sql.gz.gpg
```

## 8.2 Restore Drill Command Pattern

```bash
cd /opt/pliaforma

set -a
. deploy/.env
set +a

PG_CONTAINER=pliaforma-prod-postgres-1 \
bash ops/restore-drill.sh
```

Expected final output:

```text
RESTORE DRILL: TÜM KONTROLLER GEÇTİ ✅
```

## 8.3 Backup Safety

Do not print or commit:

```text
BACKUP_ENCRYPTION_KEY
DB password
JWT secret
MFA key
SMTP/API key
```

Backup file names and sizes may be reported.

Backup file contents must never be printed.

---

# 9. Environment and Secrets

Never commit or print:

```text
JWT_SECRET
POSTGRES_PASSWORD
MFA_ENCRYPTION_KEY
BACKUP_ENCRYPTION_KEY
SMTP_PASSWORD
Resend API key
Cloudflare token
SSH private key
deploy/.env
cert private key
production connection string
```

Committed example files may contain placeholders only.

Examples:

```text
CHANGE_ME
<private-email>
<secret>
```

Production values live only in server-side env files.

## 9.1 Contact Form Recipient

The public landing contact form must not expose the private recipient email.

Recipient is configured only in production env:

```text
CONTACT_TO_EMAIL=<private recipient>
```

Frontend must never include this value.

---

# 10. Security Baseline

Agents must not weaken any of these:

```text
MFA production enforcement
Trusted MFA device security
Public studio registration lock
Tenant isolation
Role-based authorization
Pilot Simple People Mode isolation
SameSite Strict cookie model
Production email fail-fast behavior
Contact form anti-spam/rate-limit protections
Private route noindex protections
```

## 10.1 MFA Trusted Device Rules

Trusted MFA device behavior:

```text
Cookie: HttpOnly + Secure + SameSite=Strict
Cookie path: /api/auth
Raw token: never stored in DB
DB stores: token hash only
Default duration: 30 days
Revoke on: password change, password reset, MFA reset/setup
Recovery code: must not set trusted device cookie
```

Do not weaken this.

## 10.2 Mobile Auth Security Rules (Sprint 53+)

Mobile auth transport lives under:

```text
api/auth/mobile/*
```

Mobile auth rules:

- Refresh tokens and trusted-device tokens may travel in response body only for mobile auth endpoints.
- Mobile auth endpoints must never write cookies.
- `Set-Cookie` is forbidden on mobile auth endpoints.
- Token-bearing mobile auth responses must send:
  - `Cache-Control: no-store`
  - `Pragma: no-cache`
- Refresh tokens are channel-bound by `RefreshTokens.Client`.
- Web refresh tokens must be invalid at mobile endpoints.
- Mobile refresh tokens must be invalid at web endpoints.
- The client/channel must be fixed server-side by the controller/command path.
- The client/channel must never be accepted from request body or untrusted headers.
- `api/auth/mobile/mfa/setup/verify` must be `mfa_pending` only.
- A full normal bearer token must receive 403 on mobile MFA setup verify.
- Recovery-code flows must never create or return a trusted-device token.
- Mobile trusted-device tokens must still follow trusted-device rules:
  - raw token returned once only
  - DB stores hash only
  - revocation on password change/reset and MFA reset/setup
- Refresh token reuse detection must remain enabled for web and mobile.
- Family revocation must be triggered only for rotation-consumed tokens.
- Logout/bulk-revoked tokens must not trigger false-positive family revocation.
- Grace-window reuse must be audited even when family revocation is skipped.
- Raw refresh tokens, raw trusted-device tokens, and full hashes must never be logged or audited.
- Mobile refresh lifetime is controlled by `Jwt__MobileRefreshTokenDays`.
- Default mobile refresh lifetime is 45 days unless explicitly changed.
- If refresh-token purge is enabled, token retention must be greater than or equal to the mobile refresh lifetime.
- New mobile contracts with machine-consumed date/time fields should prefer UTC/Z timestamps unless a sprint explicitly documents another format.

Do not weaken these rules without an explicit security design review.

---

# 11. Public Content Rules

Public landing content must be honest and legally safe.

Do not include:

```text
fake studio names
fake person names
fake testimonials
fake customer logos
fake usage metrics
fake revenue numbers
unverified claims
personal email addresses
private recipient addresses
```

Allowed:

```text
pilot-safe product description
feature cards
how-it-works section
FAQ
demo/contact form
representative UI marked as representative
```

All representative/mock UI must be clearly labeled.

---

# 12. SEO Rules

Public landing page may be indexed.

Private/app routes must not be indexed.

Private routes include:

```text
/login
/register
/register-studio
/join-studio
/dashboard
/platform
/api
any authenticated app pages
```

Maintain:

```text
metadata
canonical URL
robots.txt
sitemap.xml
OpenGraph image
Twitter card
JSON-LD without fake claims
X-Robots-Tag: noindex, nofollow for private routes
```

Google Search Console is an external manual step and must not be faked.

---

# 13. CI Rules

The repository has CI validation.

CI must remain green.

Current CI expectations:

```text
backend Release build
frontend build/typecheck
backend Docker build
frontend Docker build
security scans
```

Lint may be advisory until the existing lint backlog is cleaned.

Do not introduce new lint issues in changed files.

Do not change CI/CD workflows unless the sprint is about CI/CD.

---

# 14. Validation Checklist

Validation depends on scope.

## 14.1 Backend

```bash
dotnet build -c Release
```

If tests exist:

```bash
dotnet test -c Release
```

## 14.2 Frontend

```bash
npm ci
npx tsc --noEmit
npm run build
```

## 14.3 Docker

```bash
docker build
```

or project-specific Docker build commands.

## 14.4 EF / Migration

When backend persistence changes:

```bash
dotnet ef migrations has-pending-model-changes
```

or the project’s existing has-pending script.

If migration exists, report:

```text
migration name
additive/destructive
tables/columns changed
rollback risk
prod deploy requirement
```

## 14.5 Runtime Smoke Examples

Run only when relevant:

```text
/health
registration-status
login
MFA
trusted device
contact form
booking
attendance
payment
dashboard
robots.txt
sitemap.xml
OpenGraph image
```

---

# 15. Scope Control

Every sprint must define what is in scope and out of scope.

If an agent discovers an adjacent issue, it must report it separately instead of fixing it silently.

Example:

```text
Found but not fixed:
- Sidebar polish issue
- lint backlog
- future CD automation
```

Do not mix unrelated improvements into the same sprint.

---

# 16. Migrations

Migrations are allowed only when required.

Default preference:

```text
No migration
```

If migration is required:

```text
additive only unless explicitly approved
no destructive changes
no data deletion
no DROP of existing production data
no manual prod DB edits
```

Production migration deploy requires:

```text
fresh encrypted backup
restore drill when risk exists
explicit deploy approval
post-deploy migration verification
```

---

# 17. Contact Form Rules

Contact form must:

```text
send email through backend only
not write to DB unless explicitly requested
not expose recipient email
not expose SMTP config
rate-limit requests
use honeypot or equivalent anti-spam
escape/sanitize user content in email
return generic user-safe errors
avoid logging PII content
support Reply-To only after validation
```

If `CONTACT_TO_EMAIL` is missing in production, endpoint may return safe 503.

Backend startup should not fail only because contact recipient is missing unless the sprint explicitly requests fail-fast.

---

# 18. Sidebar / UI Polish Rules

For sidebar/nav icons:

```text
Do not use emoji/unicode glyphs for app navigation icons.
Use a consistent SVG icon set.
Use fixed-size icon wrapper.
Align icons with flex center.
Preserve active states.
Preserve collapsed/expanded behavior.
Preserve mobile drawer behavior.
```

Do not do large layout refactors for small visual polish.

---

# 19. Agent Prompt Usage

## 19.1 Builder Prompt Starter

Use this when acting as Builder:

```text
Read AGENTS.md first.
You are the Builder / Implementer agent.
Follow AGENTS.md strictly.
Implement the sprint spec.
Do not commit, push, tag, deploy, or SSH into production.
Report only after validation.
```

## 19.2 Reviewer Prompt Starter

Use this when acting as Reviewer:

```text
Read AGENTS.md first.
You are the Reviewer / Auditor agent.
Follow AGENTS.md strictly.
Do not implement changes in the first pass.
Review the sprint spec, builder report, and git diff.
Run validation where relevant.
Return GO / NO-GO.
```

## 19.3 Release Prompt Starter

Use this when acting as Release agent:

```text
Read AGENTS.md first.
You are handling commit/tag/deploy only after explicit approval.
Do not stage unrelated files.
Never use git add .
Show git status/diff first.
Proceed only with the approved files and approved tag.
```

---

# 20. Standard Builder Report Template

```text
Sprint X — Builder Report

1. Summary
2. Scope implemented
3. Files changed
4. Migration status
5. Backend impact
6. Frontend impact
7. Security impact
8. Production impact
9. Validation results
10. Smoke tests
11. Risks / limitations
12. Out-of-scope items not touched
13. Commit recommendation
14. Tag recommendation
15. Confirmation: commit/push/tag/deploy not performed
```

---

# 21. Standard Reviewer Report Template

```text
Sprint X — Reviewer Report

Decision: GO / NO-GO

1. Critical findings
2. High findings
3. Medium findings
4. Low findings
5. Spec compliance table
6. Changed file analysis
7. Migration analysis
8. Security analysis
9. Production safety analysis
10. Validation results
11. Smoke coverage
12. Missing tests
13. Commit/tag readiness
14. Required fixes if NO-GO
15. Final recommendation
```

---

# 22. Standard Commit/Tag Final Report Template

```text
Sprint X — Commit/Tag Final Report

Git:
- Commit hash
- Commit message
- Push result
- Tag object hash
- Tag target commit
- Tag push result
- main = origin/main?
- git status clean?
- Previous tag unchanged?

Scope:
- Files committed
- Migration?
- Secrets committed?
- deploy/.env committed?
- certs/backups committed?
- CLAUDE.md committed?
- Out-of-scope files?

Validation:
- dotnet build
- frontend build
- smoke tests
- CI status if available

Deploy note:
- Is production deploy required?
- If yes, exact approved deploy steps
```

---

# 23. Standard Production Deploy Report Template

```text
Production Deploy Report

1. Deployed tag
2. Server commit hash
3. Containers status
4. Health check
5. Registration status
6. Migration verification if applicable
7. Backup taken?
8. Restore drill result if applicable
9. Smoke test result
10. Known issues
11. Rollback point
```

---

# 24. Current Important Production Facts

These facts must be respected unless explicitly changed by a later approved sprint:

```text
Production domain: https://pliaforma.com
Canonical host: pliaforma.com
Server path: /opt/pliaforma
Public studio registration: disabled by default
Pilot Simple People Mode: enabled for pilot
Email provider: Resend SMTP
Contact recipient: CONTACT_TO_EMAIL in deploy/.env
Database: PostgreSQL in Docker volume
Deployment: Docker Compose on VPS
CI exists: .github/workflows/ci.yml
Security scan exists: .github/workflows/security.yml
```

---

# 25. Absolute Prohibitions

Never do these without explicit written approval:

```text
docker compose down -v
rm -rf production volumes
delete backup files
print deploy/.env
print private keys
commit secrets
commit certs
commit backups
disable MFA in production
open public studio registration permanently
change canonical host
split frontend/API origins
weaken tenant isolation
force push main
move existing tags
```

---

# 26. Default Decision Bias

When uncertain:

```text
Do not proceed.
Report uncertainty.
Ask for human decision.
```

Prefer:

```text
small safe change
clear validation
explicit approval
```

Avoid:

```text
large silent refactor
implicit deploy
unverified success claim
```

---

# 27. Final Rule

The agent’s job is not only to make the code work.

The agent’s job is to protect the production pilot.

Every change must be:

```text
scoped
reviewable
validated
reversible
approved
```