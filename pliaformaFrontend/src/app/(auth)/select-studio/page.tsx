"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { Badge, Card } from "@/components/ui";

export default function SelectStudioPage() {
  const { memberships, switchStudio } = useAuth();
  const router = useRouter();
  const [busyId, setBusyId] = useState<string | null>(null);

  const choose = async (studioId: string) => {
    setBusyId(studioId);
    try {
      await switchStudio(studioId);
      router.replace("/dashboard");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Card>
      <h1 className="mb-1 text-xl font-semibold text-foreground">Stüdyo seçin</h1>
      <p className="mb-6 text-sm text-muted">Birden fazla stüdyoya üyesiniz. Devam etmek için birini seçin.</p>

      {memberships.length === 0 ? (
        <p className="text-sm text-muted">Henüz aktif bir stüdyo üyeliğiniz yok.</p>
      ) : (
        <ul className="space-y-2">
          {memberships.map((m) => (
            <li key={m.studioId}>
              <button
                onClick={() => choose(m.studioId)}
                disabled={busyId !== null}
                className="flex w-full items-center justify-between rounded-xl border border-border bg-surface px-4 py-3 text-left transition-colors hover:bg-surface-muted disabled:opacity-50"
              >
                <div>
                  <p className="text-sm font-medium text-foreground">{m.studioName}</p>
                  <p className="text-xs text-muted">{m.studioSlug}</p>
                </div>
                <Badge tone="rose">{busyId === m.studioId ? "…" : m.role}</Badge>
              </button>
            </li>
          ))}
        </ul>
      )}
    </Card>
  );
}
