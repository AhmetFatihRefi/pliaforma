#!/usr/bin/env bash
# Pliaforma — Sprint 31D-3 local security scan wrapper. CI (.github/workflows/security.yml) ile AYNI komutlar.
# gitleaks = BLOCKING (bulgu → non-zero). npm/dotnet/trivy/SBOM = advisory (bulgu üretse de default fail etmez).
# Tool yoksa → SKIP + WARNING (CI kendi araçlarını kurar). --strict ile advisory'ler de fail eder.
# Kullanım: scripts/security-scan.sh [--strict]
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GITLEAKS_VERSION="${GITLEAKS_VERSION:-v8.21.2}"
TRIVY_VERSION="${TRIVY_VERSION:-0.58.0}"
STRICT=0; [ "${1:-}" = "--strict" ] && STRICT=1
FAILED=0; ADVISORY=0
section(){ printf "\n\033[36m== %s ==\033[0m\n" "$1"; }
ok(){ printf "  \033[32mOK\033[0m %s\n" "$1"; }
warn(){ printf "  \033[33mSKIP\033[0m %s\n" "$1"; }
adv(){ printf "  \033[33mADVISORY\033[0m %s\n" "$1"; ADVISORY=1; }
bad(){ printf "  \033[31mFAIL\033[0m %s\n" "$1"; FAILED=1; }

section "gitleaks (secret scan — BLOCKING)"
if command -v gitleaks >/dev/null 2>&1; then
  if gitleaks detect --source "$ROOT" --config "$ROOT/.gitleaks.toml" --redact --no-banner --exit-code 1; then ok "secret leak yok"; else bad "SECRET LEAK bulundu"; fi
elif command -v docker >/dev/null 2>&1; then
  if MSYS_NO_PATHCONV=1 docker run --rm -v "$ROOT:/repo" "ghcr.io/gitleaks/gitleaks:${GITLEAKS_VERSION}" detect --source /repo --config /repo/.gitleaks.toml --redact --no-banner --exit-code 1 >/dev/null 2>&1; then ok "secret leak yok (docker gitleaks)"; else bad "SECRET LEAK bulundu (gitleaks)"; fi
else warn "gitleaks/docker yok → atlandı (CI enforce eder)"; fi

section "npm audit (advisory, --omit=dev)"
if command -v npm >/dev/null 2>&1; then
  ( cd "$ROOT/pliaformaFrontend" && npm audit --omit=dev --audit-level=high ) && ok "npm audit temiz" || adv "npm audit bulgular (advisory)"
else warn "npm yok → atlandı"; fi

section "dotnet vulnerable (advisory, transitive dahil)"
if command -v dotnet >/dev/null 2>&1; then
  ( cd "$ROOT/pliaformaApi" && dotnet restore pliaformaApi.sln >/dev/null 2>&1 || true
    OUT=$(dotnet list pliaformaApi.sln package --vulnerable --include-transitive 2>&1 || true)
    echo "$OUT" | grep -qiE "no vulnerable|has no vulnerable" && ok "zafiyetli paket yok" || { echo "$OUT" | tail -8; adv "zafiyetli paket olabilir (advisory)"; } )
else warn "dotnet yok → atlandı"; fi

section "trivy fs (advisory, image build YOK)"
if command -v trivy >/dev/null 2>&1; then
  trivy fs --scanners vuln,misconfig,secret --severity HIGH,CRITICAL --no-progress "$ROOT" >/dev/null 2>&1 && ok "trivy fs çalıştı" || adv "trivy fs bulgular (advisory)"
elif command -v docker >/dev/null 2>&1; then
  MSYS_NO_PATHCONV=1 docker run --rm -v "$ROOT:/repo" "aquasec/trivy:${TRIVY_VERSION}" fs --scanners vuln,misconfig,secret --severity HIGH,CRITICAL --no-progress /repo >/dev/null 2>&1 && ok "trivy fs çalıştı (docker)" || adv "trivy fs bulgular (advisory)"
else warn "trivy/docker yok → atlandı"; fi

section "SBOM (CycloneDX, advisory)"
SBOM_DIR="${SBOM_DIR:-$(mktemp -d)}"
if command -v npm >/dev/null 2>&1; then
  ( cd "$ROOT/pliaformaFrontend" && npx --yes @cyclonedx/cyclonedx-npm@1 --omit dev --output-file "$SBOM_DIR/npm-sbom.json" ) >/dev/null 2>&1 && ok "npm SBOM: $SBOM_DIR/npm-sbom.json" || adv "npm SBOM üretilemedi"
else warn "npm yok → npm SBOM atlandı"; fi
if command -v dotnet >/dev/null 2>&1 && dotnet tool list --global 2>/dev/null | grep -qi cyclonedx; then
  dotnet CycloneDX "$ROOT/pliaformaApi/pliaformaApi.sln" -o "$SBOM_DIR" --filename dotnet-sbom.json --json >/dev/null 2>&1 && ok "dotnet SBOM: $SBOM_DIR/dotnet-sbom.json" || adv "dotnet SBOM üretilemedi"
else warn "dotnet CycloneDX tool yok → CI üretir"; fi

printf "\n"
[ "$STRICT" = "1" ] && [ "$ADVISORY" = "1" ] && FAILED=1 && echo "(--strict: advisory bulgular fail sayıldı)"
[ "$FAILED" = "0" ] && { printf "\033[32mSECURITY SCAN: gitleaks temiz%s\033[0m\n" "$([ "$ADVISORY" = "1" ] && echo " (advisory bulgular var)")"; exit 0; } || { printf "\033[31mSECURITY SCAN: BAŞARISIZ\033[0m\n"; exit 1; }
