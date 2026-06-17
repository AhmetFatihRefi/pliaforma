"use client";

import { useEffect, useState } from "react";
import { studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { type StudentDto, studentSourceLabels, studentStatusLabels } from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

function Row({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="flex justify-between gap-4 border-b border-border py-2 last:border-0">
      <span className="text-sm text-muted">{label}</span>
      <span className="text-sm font-medium text-foreground">{value || "—"}</span>
    </div>
  );
}

export default function MyProfilePage() {
  const [profile, setProfile] = useState<StudentDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setProfile(await studentService.me());
      } catch (e) {
        setError(e instanceof ApiError && e.status === 404 ? "Bu stüdyoda bir öğrenci profiliniz yok." : "Profil yüklenemedi.");
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <h1 className="text-2xl font-semibold text-foreground">Profilim</h1>
      {error && <Alert kind="info">{error}</Alert>}
      {profile && (
        <Card>
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-semibold text-foreground">{profile.fullName}</h2>
            <Badge tone={profile.status === "Active" ? "success" : "neutral"}>{studentStatusLabels[profile.status]}</Badge>
          </div>
          <Row label="Telefon" value={profile.phone} />
          <Row label="E-posta" value={profile.email} />
          <Row label="Kaynak" value={studentSourceLabels[profile.source]} />
          <Row label="Doğum tarihi" value={profile.birthDate} />
          <Row label="Acil durum kişisi" value={profile.emergencyContactName} />
          <Row label="Acil durum telefonu" value={profile.emergencyContactPhone} />
          {profile.healthNotes && <Row label="Sağlık notu" value={profile.healthNotes} />}
          {profile.notes && <Row label="Not" value={profile.notes} />}
        </Card>
      )}
    </div>
  );
}
