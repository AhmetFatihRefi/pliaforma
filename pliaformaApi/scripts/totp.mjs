#!/usr/bin/env node
// Pliaforma — Sprint 31-MFA: repo-içi minik TOTP üreteci (RFC 6238, HMAC-SHA1, 6 hane, 30 sn).
// Smoke'larda authenticator yerine kod üretmek için. Node built-in crypto; harici paket/oathtool YOK.
// Kullanım: node scripts/totp.mjs <BASE32_SECRET> [offsetSteps]   → 6 haneli kodu yazdırır.
import crypto from "node:crypto";

const B32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
function base32Decode(s) {
  s = (s || "").replace(/=+$/, "").toUpperCase();
  let bits = 0, value = 0;
  const out = [];
  for (const c of s) {
    const idx = B32.indexOf(c);
    if (idx < 0) continue;
    value = (value << 5) | idx;
    bits += 5;
    if (bits >= 8) { out.push((value >>> (bits - 8)) & 0xff); bits -= 8; }
  }
  return Buffer.from(out);
}

function totp(secretB32, offsetSteps = 0) {
  const key = base32Decode(secretB32);
  let step = Math.floor(Date.now() / 1000 / 30) + Number(offsetSteps || 0);
  const buf = Buffer.alloc(8);
  buf.writeBigInt64BE(BigInt(step));
  const hmac = crypto.createHmac("sha1", key).update(buf).digest();
  const off = hmac[hmac.length - 1] & 0x0f;
  const bin = ((hmac[off] & 0x7f) << 24) | (hmac[off + 1] << 16) | (hmac[off + 2] << 8) | hmac[off + 3];
  return String(bin % 1_000_000).padStart(6, "0");
}

const secret = process.argv[2];
if (!secret) { console.error("kullanım: node totp.mjs <BASE32_SECRET> [offsetSteps]"); process.exit(2); }
process.stdout.write(totp(secret, process.argv[3]));
