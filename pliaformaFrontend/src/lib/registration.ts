"use client";

import { useEffect, useState } from "react";
import { authService } from "./services";
import type { RegistrationStatus } from "./types";

// Sprint 49 — Herkese açık stüdyo kaydı etkin mi (anonim /auth/registration-status).
// Tek-salon pilotta backend bunu false döner → frontend "Stüdyo oluştur" yüzeylerini gizler.
// GÜVENLİK SINIRI DEĞİL: gerçek koruma register-studio handler'ında (403). Bu yalnız UI ipucu.

// Modül düzeyinde tek uçuş (single-flight): landing'de birden çok bileşen hook'u çağırsa da tek istek gider.
let cached: Promise<RegistrationStatus> | null = null;
function loadStatus(): Promise<RegistrationStatus> {
  cached ??= authService.registrationStatus();
  return cached;
}

/**
 * Dönüş:
 *  - `undefined` → henüz bilinmiyor (yüklenme); "Stüdyo oluştur" sayfası bu sırada loader gösterir.
 *  - `true`  → kayıt açık (tam SaaS/dev).
 *  - `false` → kayıt kapalı (pilot); CTA/linkler gizlenir.
 * Hata/mock durumunda güvenli UX için `true` varsayılır (backend yine 403 ile korur).
 */
export function useStudioRegistrationEnabled(): boolean | undefined {
  const [enabled, setEnabled] = useState<boolean | undefined>(undefined);
  useEffect(() => {
    let cancelled = false;
    loadStatus()
      .then((s) => { if (!cancelled) setEnabled(s.studioRegistrationEnabled); })
      .catch(() => { if (!cancelled) setEnabled(true); });
    return () => { cancelled = true; };
  }, []);
  return enabled;
}
