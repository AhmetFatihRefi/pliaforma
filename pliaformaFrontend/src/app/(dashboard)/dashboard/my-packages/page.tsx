"use client";

import { useCallback, useEffect, useState } from "react";
import { studentFinanceService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { type StudentPackageDto, packageStatusLabels } from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const tone = (s: StudentPackageDto["status"]) =>
  s === "Active" ? "success" : s === "Cancelled" || s === "Expired" ? "danger" : "neutral";

export default function MyPackagesPage() {
  const [items, setItems] = useState<StudentPackageDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setItems(await studentFinanceService.myPackages());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Paketler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-2xl font-semibold text-foreground">Paketlerim</h1>
        <p className="mt-1 text-sm text-muted">Kalan ders hakkın ve geçerlilik.</p>
      </div>

      {error && <Alert kind="error">{error}</Alert>}

      {loading ? (
        <p className="text-sm text-muted">Yükleniyor…</p>
      ) : items.length === 0 ? (
        <Card><p className="text-sm text-muted">Henüz paketin yok.</p></Card>
      ) : (
        <div className="space-y-3">
          {items.map((p) => (
            <Card key={p.id} className="flex items-center justify-between">
              <div>
                <p className="font-medium text-foreground">{p.name}</p>
                <p className="text-sm text-muted">
                  Geçerlilik: {p.validFrom}{p.validUntil ? ` → ${p.validUntil}` : " → süresiz"}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <Badge tone={p.currentCreditBalance > 0 ? "rose" : "neutral"}>Kalan: {p.currentCreditBalance}/{p.initialCreditCount}</Badge>
                <Badge tone={tone(p.effectiveStatus)}>{packageStatusLabels[p.effectiveStatus]}</Badge>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
