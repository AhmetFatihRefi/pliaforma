import { NextResponse, type NextRequest } from "next/server";

// Sprint 31C-2 — CSP nonce + enforce. (Next 16: middleware → "proxy".)
// Per-request nonce üretir, request'e x-nonce koyar (Next bunu framework script'lerine OTOMATİK uygular),
// response'a CSP_MODE'a göre TEK CSP header koyar. nginx artık CSP üretmez → çift header yok.
//
// CSP_MODE (runtime env): "report-only" (default) | "enforce" | "disabled".
//   - report-only: yalnız Content-Security-Policy-Report-Only (bloklamaz; staging/prod ilk deploy).
//   - enforce:     yalnız Content-Security-Policy (bloklar). Env-flip + restart ile açılır (rebuild gerekmez).
//   - disabled:    hiç CSP header yok (acil rollback).
// Dev (NODE_ENV=development): HMR/React Refresh için script-src'e 'unsafe-eval', connect-src'e ws: — YALNIZ dev dalında.

export function proxy(request: NextRequest) {
  const mode = process.env.CSP_MODE ?? "report-only";
  const isDev = process.env.NODE_ENV === "development";
  const nonce = Buffer.from(crypto.randomUUID()).toString("base64");

  const csp = [
    "default-src 'self'",
    "base-uri 'self'",
    "object-src 'none'",
    "frame-ancestors 'self'",
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    // style-src: 'unsafe-inline' KALIR (React inline style + Next inline style). Style nonce denemesi yok.
    "style-src 'self' 'unsafe-inline'",
    // script-src: 'unsafe-inline' YOK; nonce + strict-dynamic. Dev'de eval (React Refresh) yalnız isDev.
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${isDev ? " 'unsafe-eval'" : ""}`,
    `connect-src 'self'${isDev ? " ws:" : ""}`,
    "frame-src 'self'",
    "form-action 'self'",
    "report-uri /api/csp-report",
  ].join("; ");

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-nonce", nonce);
  // Request-side CSP (browser GÖRMEZ): Next nonce'u buradan okuyup script'lere uygular. disabled'da gerek yok.
  if (mode !== "disabled") requestHeaders.set("Content-Security-Policy", csp);

  const response = NextResponse.next({ request: { headers: requestHeaders } });

  // Response-side: mode'a göre TEK CSP header (aynı anda enforce + report-only olmaz).
  if (mode === "enforce") response.headers.set("Content-Security-Policy", csp);
  else if (mode !== "disabled") response.headers.set("Content-Security-Policy-Report-Only", csp);

  return response;
}

// api (backend'e proxy) + statik asset'ler hariç tüm sayfa route'larına uygulanır.
export const config = {
  matcher: ["/((?!api|_next/static|_next/image|favicon.ico).*)"],
};
