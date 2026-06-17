"use client";

import { useAuth } from "./auth";

/** Aktif stüdyo rolüne göre yetki yardımcıları (UI gating). */
export function useRole() {
  const { activeStudio } = useAuth();
  const role = activeStudio?.role;
  const canManage = role === "Owner" || role === "StudioAdmin";
  return { role, canManage };
}
