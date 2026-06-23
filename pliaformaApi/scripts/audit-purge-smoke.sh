#!/usr/bin/env bash
# Pliaforma — Sprint 31D-1 retention purge + append-only smoke.
# Part 1+2 (psql, deterministik): append-only trigger (UPDATE/DELETE blocked, INSERT ok, GUC-bypass DELETE ok) +
#   token-purge predikatları (expired/revoked silinir, aktif kalır).
# Part 3 (WORKER_CHECK=1, worker'lı stack): dry-run/real purge + AuditRetentionPurge meta event gözlemi.
# Kullanım: PG_CONTAINER=pliaforma-dev-postgres-1 scripts/audit-purge-smoke.sh
set -u
PGC="${PG_CONTAINER:-$(docker ps --format '{{.Names}}' 2>/dev/null | grep -E 'pliaforma-postgres|pliaforma-dev-postgres' | head -1)}"
FAILED=0; TS=$(date +%s)
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
[ -z "$PGC" ] && { echo "postgres yok (PG_CONTAINER ver)"; exit 1; }
# psql çalıştır (ON_ERROR_STOP=1): exit 0 = başarı. q=değer döndür, run=exit kodu.
q(){ docker exec "$PGC" psql -U postgres -d pliaforma -tAc "$1" 2>/dev/null | tr -d '\r'; }
run(){ docker exec "$PGC" psql -U postgres -d pliaforma -v ON_ERROR_STOP=1 -c "$1" >/dev/null 2>&1; }

MARK="purgetest_$TS"
echo "== Part 1: append-only trigger (SecurityAuditLogs) =="
# INSERT serbest:
if run "INSERT INTO \"SecurityAuditLogs\" (\"Id\",\"OccurredAt\",\"EventType\",\"Outcome\",\"Detail\") VALUES (gen_random_uuid(), now(), 'LoginSucceeded','Success','$MARK');"; then ok "INSERT audit serbest"; else bad "INSERT audit engellendi (olmamalı)"; fi
# UPDATE engellenmeli:
if run "UPDATE \"SecurityAuditLogs\" SET \"Detail\"='x' WHERE \"Detail\"='$MARK';"; then bad "UPDATE engellenMEDİ (trigger yok?)"; else ok "UPDATE DB-trigger ile engellendi"; fi
# Normal DELETE (GUC yok) engellenmeli:
if run "DELETE FROM \"SecurityAuditLogs\" WHERE \"Detail\"='$MARK';"; then bad "normal DELETE engellenMEDİ"; else ok "normal DELETE DB-trigger ile engellendi"; fi
# GUC bypass ile DELETE izinli:
if run "BEGIN; SET LOCAL app.audit_purge='on'; DELETE FROM \"SecurityAuditLogs\" WHERE \"Detail\"='$MARK'; COMMIT;"; then ok "GUC bypass (app.audit_purge=on) DELETE izinli"; else bad "GUC bypass DELETE çalışmadı"; fi
LEFT=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\"='$MARK';")
[ "${LEFT:-1}" = "0" ] && ok "bypass sonrası test satırı gerçekten silindi" || bad "test satırı silinmedi (n=$LEFT)"

echo "== Part 2: token purge predikatları (fiziksel hard-delete) =="
UID0=$(q "SELECT \"Id\" FROM \"Users\" LIMIT 1;")
[ -n "$UID0" ] && ok "test için user bulundu" || bad "user yok (seed gerekli)"
# AuthToken: expired (now-30g) + aktif (now+30g)
run "INSERT INTO \"AuthTokens\" (\"Id\",\"UserId\",\"Purpose\",\"TokenHash\",\"ExpiresAt\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$UID0',0,'exp_$MARK', now()-interval '30 days', now(), false), (gen_random_uuid(),'$UID0',0,'act_$MARK', now()+interval '30 days', now(), false);"
# RefreshToken: expired + revoked-old + aktif
run "INSERT INTO \"RefreshTokens\" (\"Id\",\"UserId\",\"TokenHash\",\"ExpiresAt\",\"RevokedAt\",\"CreatedAt\",\"IsDeleted\") VALUES (gen_random_uuid(),'$UID0','rexp_$MARK', now()-interval '30 days', NULL, now(), false), (gen_random_uuid(),'$UID0','rrev_$MARK', now()+interval '30 days', now()-interval '30 days', now(), false), (gen_random_uuid(),'$UID0','ract_$MARK', now()+interval '30 days', NULL, now(), false);"
# Worker predikatları (cutoff = now - 7 gün tampon):
run "DELETE FROM \"AuthTokens\" WHERE \"ExpiresAt\" < now()-interval '7 days' AND \"TokenHash\" LIKE '%$MARK';"
run "DELETE FROM \"RefreshTokens\" WHERE (\"ExpiresAt\" < now()-interval '7 days' OR (\"RevokedAt\" IS NOT NULL AND \"RevokedAt\" < now()-interval '7 days')) AND \"TokenHash\" LIKE '%$MARK';"
AEXP=$(q "SELECT count(*) FROM \"AuthTokens\" WHERE \"TokenHash\"='exp_$MARK';"); AACT=$(q "SELECT count(*) FROM \"AuthTokens\" WHERE \"TokenHash\"='act_$MARK';")
[ "$AEXP" = "0" ] && ok "AuthToken expired hard-delete oldu" || bad "AuthToken expired silinmedi"
[ "$AACT" = "1" ] && ok "AuthToken AKTİF korundu" || bad "AuthToken aktif YANLIŞLIKLA silindi"
REXP=$(q "SELECT count(*) FROM \"RefreshTokens\" WHERE \"TokenHash\"='rexp_$MARK';"); RREV=$(q "SELECT count(*) FROM \"RefreshTokens\" WHERE \"TokenHash\"='rrev_$MARK';"); RACT=$(q "SELECT count(*) FROM \"RefreshTokens\" WHERE \"TokenHash\"='ract_$MARK';")
[ "$REXP" = "0" ] && [ "$RREV" = "0" ] && ok "RefreshToken expired+revoked hard-delete oldu" || bad "RefreshToken expired/revoked silinmedi (exp=$REXP rev=$RREV)"
[ "$RACT" = "1" ] && ok "RefreshToken AKTİF korundu" || bad "RefreshToken aktif YANLIŞLIKLA silindi"
# temizlik (kalan aktif test tokenları)
run "DELETE FROM \"AuthTokens\" WHERE \"TokenHash\" LIKE '%$MARK';" ; run "DELETE FROM \"RefreshTokens\" WHERE \"TokenHash\" LIKE '%$MARK';"

# ---- Part 3: worker gözlemi (gated) ----
if [ "${WORKER_CHECK:-0}" = "1" ]; then
  LOGC="${PF_LOG_CONTAINER:-pliaforma-dev-backend-1}"
  echo "== Part 3: worker (logs=$LOGC) =="
  # Eski audit satırı (now-800g) ekle → worker (retention 730g) silmeli.
  run "INSERT INTO \"SecurityAuditLogs\" (\"Id\",\"OccurredAt\",\"EventType\",\"Outcome\",\"Detail\") VALUES (gen_random_uuid(), now()-interval '800 days', 'LoginSucceeded','Success','old_$MARK');"
  run "INSERT INTO \"SecurityAuditLogs\" (\"Id\",\"OccurredAt\",\"EventType\",\"Outcome\",\"Detail\") VALUES (gen_random_uuid(), now(), 'LoginSucceeded','Success','new_$MARK');"
  echo "  worker turu bekleniyor (≤100s)..."
  for i in $(seq 1 50); do OLDN=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\"='old_$MARK';"); [ "$OLDN" = "0" ] && break; sleep 2; done
  [ "$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\"='old_$MARK';")" = "0" ] && ok "worker: eski audit silindi" || bad "worker: eski audit silinmedi (worker enabled+DryRun=false+kısa interval?)"
  [ "$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"Detail\"='new_$MARK';")" = "1" ] && ok "worker: yeni audit korundu" || bad "worker: yeni audit yanlışlıkla silindi"
  PN=$(q "SELECT count(*) FROM \"SecurityAuditLogs\" WHERE \"EventType\"='AuditRetentionPurge';")
  [ "${PN:-0}" -gt 0 ] 2>/dev/null && ok "worker: AuditRetentionPurge meta event yazıldı (n=$PN)" || bad "worker: meta event yok"
  docker logs "$LOGC" 2>&1 | grep -qiE "retention purge tamam|\[DRY-RUN\]" && ok "worker: purge log satırı var" || bad "worker: purge log yok"
  run "BEGIN; SET LOCAL app.audit_purge='on'; DELETE FROM \"SecurityAuditLogs\" WHERE \"Detail\"='new_$MARK'; COMMIT;"  # temizlik
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[36mAUDIT PURGE: TÜM KONTROLLER GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mAUDIT PURGE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
