"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/auth";
import { studioSubscriptionService } from "@/lib/studioApi";
import type { StudioSubscriptionViewDto } from "@/lib/domain-types";

// Lisans/abonelik uyarısı — yalnızca Owner'a, yalnızca dikkat gereken durumlarda. Active'te hiçbir şey göstermez.
export function LicenseBanner() {
  const { activeStudio } = useAuth();
  const isOwner = activeStudio?.role === "Owner";
  const [d, setD] = useState<StudioSubscriptionViewDto | null>(null);

  useEffect(() => {
    if (!isOwner) return;
    let cancelled = false;
    studioSubscriptionService.mine().then((r) => { if (!cancelled) setD(r); }).catch(() => {});
    return () => { cancelled = true; };
  }, [isOwner]);

  if (!isOwner || !d || d.effectiveStatus === "Active") return null;

  const tone =
    d.effectiveStatus === "Expired" ? "border-danger/40 bg-danger/10 text-danger"
    : d.effectiveStatus === "Trialing" || d.effectiveStatus === "FailOpenLicensed" ? "border-primary/30 bg-primary-soft/50 text-primary-hover"
    : "border-warning/40 bg-warning/10 text-warning";

  return (
    <div className={`flex flex-wrap items-center justify-between gap-2 border-b px-6 py-2 text-sm ${tone}`}>
      <span>{d.licenseMessage}</span>
      <Link href="/dashboard/subscription" className="shrink-0 font-medium underline underline-offset-2">Aboneliği görüntüle →</Link>
    </div>
  );
}
