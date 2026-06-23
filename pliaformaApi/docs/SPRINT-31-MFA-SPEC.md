# Sprint 31-MFA — Owner/StudioAdmin TOTP MFA

Statü: **UYGULANIYOR** · Önceki: 31B-2 (`v1.14.0-pilot`). Tag önerisi (yeşil): **`v1.15.0-pilot`**. Tek mini-sprint (bölme yok).

## Amaç
Owner/StudioAdmin hesaplarında hesap ele geçirme riskini TOTP MFA ile azaltmak.

## Onaylı kararlar
1. **Kapsam:** Hesap düzeyi — kullanıcının **herhangi bir aktif Owner/StudioAdmin üyeliği** varsa MFA zorunlu (aktif-stüdyo rolüne göre değil). Instructor/Student zorunlu değil.
2. **Şifreleme:** Manuel **AES-256-GCM** + `Mfa:EncryptionKey` (env, ≥32 byte base64; prod fail-fast yok/placeholder).
3. **Setup-pending:** API düzeyi `mfa_pending` claim + authorization enforcement. mfa_pending token yalnız `/api/auth/me`, `/api/auth/logout`, `/api/auth/mfa/setup/start`, `/api/auth/mfa/setup/verify` (+ refresh yalnız mfa_pending üretebilir, full'a dönemez).
4. **Recovery codes:** 10 adet, setup sonrası bir kez gösterilir, **SHA-256 hash'li**, tek kullanımlık (`ConsumedAt`). Regenerate → 31C.
5. **Challenge:** Stateless kısa-ömürlü imzalı JWT (purpose=`mfa_challenge`, sub=userId, aud ayrı, exp ~5dk). Normal middleware full-access kabul etmez. Replay: `LastUsedTimestep`.
6. **`Mfa:Enabled` master switch:** prod true / dev-test false. Mevcut suite/E2E `Mfa__Enabled=false` ile yeşil kalır; MFA smoke'ları `true`. **Production'da false ise fail-fast** (en azından checklist blocker).
7. **Reset:** Bu sprint yalnız `POST /api/auth/mfa/reset` `[Authorize(PlatformAdmin)]`. Owner→StudioAdmin reset 31C; Owner self → runbook.
8. **Smoke TOTP:** Repo-içi küçük Node.js helper (built-in crypto, base32 decode dahil); oathtool yok.
9. **register-studio:** Mfa:Enabled=true + Owner → setup-pending token (full dashboard token DEĞİL).

## DB / migration — `Sprint31MFA_TotpMfa` (additive)
- `User.MfaEnabled bool NOT NULL DEFAULT false`
- `UserMfaSettings`: `UserId PK/FK`, `SecretEncrypted text?`, `PendingSecretEncrypted text?`, `EnrolledAt timestamptz?`, `LastUsedTimestep bigint?`
- `MfaRecoveryCodes`: `Id`, `UserId FK`, `CodeHash`, `ConsumedAt timestamptz?`
- Mevcut kullanıcılar `MfaEnabled=false` → Owner/StudioAdmin bir sonraki login'de setup-pending.

## Şifreleme & key
- AES-256-GCM (nonce|tag|ciphertext base64). Anahtar `Mfa:EncryptionKey` base64 ≥32 byte.
- **Prod fail-fast:** `Mfa:Enabled=true` iken anahtar yok / `CHANGE_ME`/placeholder / <32 byte → açılmaz (31B-1 guard genişletilir). Production'da `Mfa:Enabled=false` → fail-fast (pilot için MFA kapalı olmamalı).
- **Key kaybı:** TOTP secret'lar çözülemez → yeniden enroll; recovery code'lar bağımsız hash'li → key'den bağımsız çalışır (kurtarma). Key'i JWT secret gibi yedekle (BACKUP runbook notu).

## Challenge token
- JWT, `aud=pliaforma-mfa-challenge` (normal `aud=Pliaforma`'dan ayrı → normal `[Authorize]` reddeder), `purpose=mfa_challenge`, sub=userId, exp 5dk. Mevcut JWT secret ile imzalanır; ayrı key scope büyütür, gerekmez.

## API
| Endpoint | Auth | İşlev |
|---|---|---|
| `POST /api/auth/login` (değişir) | anon | 3 durum: full token / `{mfaRequired,challengeToken}` / `{...,mfaSetupRequired}` + mfa_pending token |
| `mfa/setup/start` | mfa_pending OK | secret üret → PendingSecretEncrypted; `otpauth://` URI + manuel secret; audit MFASetupStarted |
| `mfa/setup/verify` | mfa_pending OK | TOTP doğrula → MfaEnabled=true + 10 recovery code (bir kez) + full token; audit MFASetupCompleted |
| `mfa/challenge/verify` | anon + rate-limit | challengeToken+code → full token; audit MFASucceeded/MFAFailed |
| `mfa/recovery-code/verify` | anon + rate-limit | challengeToken+code → consume → full token; audit MFARecoveryCodeUsed |
| `mfa/reset` | PlatformAdmin | hedef MFA sıfırla; audit MFAResetCompleted |

## Frontend
`/mfa-setup` (QR `qrcode` + manuel + verify + recovery codes bir kez), login MFA challenge ekranı (+recovery code), my-profile MFA durumu + kalan recovery sayısı, dashboard guard `mfaSetupRequired` → /mfa-setup.

## Audit events (SecurityAuditEventType +)
MFASetupStarted, MFASetupCompleted, MFAChallengeCreated, MFASucceeded, MFAFailed, MFARecoveryCodeUsed, MFAResetCompleted. **Plain secret/recovery/TOTP/challenge/access/refresh/key ASLA.**

## Test
Yeni: `mfa-setup-smoke.sh`, `mfa-login-smoke.sh`, `mfa-recovery-code-smoke.sh`, `mfa-audit-smoke.sh` (Mfa__Enabled=true + repo TOTP helper). Mevcut full smoke/E2E `Mfa__Enabled=false` ile yeşil.

## Riskler
Switch yoksa tüm suite kırılır (kritik) · key kaybı → re-enroll (recovery code kurtarır) · TOTP brute-force → rate-limit + MFA lockout · challenge replay → timestep · setup bypass → API enforcement.

## Kapsam dışı
SMS/email OTP, WebAuthn, Owner→StudioAdmin reset UI, self-disable, recovery regenerate, audit viewer, CSP enforce, backup encryption, tenant expansion, ürün feature.

## Uygulama sırası
settings+packages → AES-GCM+TOTP+recovery servisleri → prod guard → domain+migration → challenge token → login/refresh redesign → mfa_pending enforcement → endpoints+audit → frontend → smokes → docs → verify.
