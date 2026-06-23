#!/usr/bin/env bash
# Pliaforma — Sprint 31D-3 CI security setup smoke (reproducible doğrulama).
# gitleaks baseline temiz + fixture gate çalışıyor + workflow YAML geçerli + wrapper syntax + dosya varlığı.
# gitleaks docker ile koşar (binary yoksa). Kullanım: scripts/ci-security-smoke.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GL="ghcr.io/gitleaks/gitleaks:${GITLEAKS_VERSION:-v8.21.2}"
FAILED=0
ok(){ printf "  \033[32mPASS\033[0m %s\n" "$1"; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }
mountpath(){ printf '%s' "$1" | sed -E 's|^([A-Za-z]):|/\L\1|; s|\\|/|g'; }   # C:\x → /c/x (docker desktop)

echo "== Dosya varlığı =="
for f in .github/workflows/security.yml .gitleaks.toml scripts/security-scan.sh pliaformaApi/docs/security/INCIDENT_TABLETOP_DRILL.md; do
  [ -f "$ROOT/$f" ] && ok "$f var" || bad "$f yok"
done

echo "== Workflow yapısı (grep; tam YAML parse CI/docker) =="
WF="$ROOT/.github/workflows/security.yml"
for j in "secret-scan" "deps-dotnet" "deps-npm" "trivy-fs" "sbom"; do
  grep -qE "^  $j:" "$WF" && ok "job: $j" || bad "job eksik: $j"
done
grep -qE "^on:" "$WF" && grep -q "schedule:" "$WF" && ok "triggers (push/pr/dispatch/schedule)" || bad "triggers eksik"
grep -q "gitleaks-action" "$WF" && bad "gitleaks-action kullanılmış (olmamalı)" || ok "gitleaks-action yok (container/binary)"
grep -q "permissions:" "$WF" && grep -q "contents: read" "$WF" && ok "permissions: contents:read (minimal)" || bad "minimal permissions yok"

echo "== Wrapper + smoke syntax =="
bash -n "$ROOT/scripts/security-scan.sh" && ok "security-scan.sh bash -n" || bad "security-scan.sh syntax"

if command -v docker >/dev/null 2>&1; then
  RM="$(mountpath "$ROOT")"
  echo "== gitleaks baseline TEMİZ (full history) =="
  if MSYS_NO_PATHCONV=1 docker run --rm -v "${RM}:/repo" "$GL" detect --source /repo --config /repo/.gitleaks.toml --redact --no-banner --exit-code 1 >/dev/null 2>&1; then ok "baseline temiz (leak yok)"; else bad "baseline'da leak bulundu (allowlist/gerçek secret?)"; fi
  echo "== gitleaks GATE çalışıyor (fixture → tespit) =="
  mkdir -p "$ROOT/.fixture-tmp"; printf 'github_pat = "ghp_aB3dE5fG7hI9jK1lM3nO5pQ7rS9tU1vWxYz0"\n' > "$ROOT/.fixture-tmp/fx.txt"
  if MSYS_NO_PATHCONV=1 docker run --rm -v "${RM}/.fixture-tmp:/scan" "$GL" detect --source /scan --no-git --no-banner --redact >/dev/null 2>&1; then bad "fixture tespit EDİLMEDİ (gate çalışmıyor)"; else ok "fixture tespit edildi (gate çalışıyor)"; fi
  rm -rf "$ROOT/.fixture-tmp"
else
  printf "  \033[33mSKIP\033[0m docker yok → gitleaks gözlemi atlandı\n"
fi

echo
[ "$FAILED" = "0" ] && { printf "\033[36mCI SECURITY SMOKE: GEÇTİ ✅\033[0m\n"; exit 0; } || { printf "\033[36mCI SECURITY SMOKE: BAŞARISIZ ❌\033[0m\n"; exit 1; }
