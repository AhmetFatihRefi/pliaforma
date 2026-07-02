"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useAuth } from "@/lib/auth";
import { useRole } from "@/lib/useRole";
import { type StudentDto, studentSourceLabels, studentStatusLabels } from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  Input,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  TR,
} from "@/components/ui";
import { GuestFormModal, statusTone } from "./_components";

export default function StudentsPage() {
  const { canManage } = useRole();
  const { pilotSimplePeopleMode } = useAuth();
  const [items, setItems] = useState<StudentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [formOpen, setFormOpen] = useState(false);

  const load = useCallback(async (q: string) => {
    setLoading(true);
    try {
      setItems(q.trim() ? await studentService.search(q.trim()) : await studentService.list());
      setError(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Öğrenciler yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    const t = setTimeout(() => load(search), 300);
    return () => clearTimeout(t);
  }, [search, load]);

  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">Öğrenciler</h1>
          <p className="mt-1 text-sm text-muted">Bir öğrenciye tıklayın; sözleşmeleri, ödemeleri ve katılımı detay sayfasında yönetin.</p>
        </div>
        <Button onClick={() => setFormOpen(true)}>{pilotSimplePeopleMode ? "+ Öğrenci ekle" : "+ Misafir ekle"}</Button>
      </div>

      <Input placeholder="Ara: ad, telefon veya e-posta…" value={search} onChange={(e) => setSearch(e.target.value)} />

      {error && <Alert kind="error">{error}</Alert>}

      <Table>
        <THead>
          <TR>
            <TH>İsim</TH>
            <TH>İletişim</TH>
            <TH>Durum</TH>
            <TH>Kaynak</TH>
            <TH className="text-right">İşlem</TH>
          </TR>
        </THead>
        <tbody>
          {loading ? (
            <TableEmpty colSpan={5}>Yükleniyor…</TableEmpty>
          ) : items.length === 0 ? (
            <TableEmpty colSpan={5}>{search ? "Eşleşen öğrenci yok." : "Henüz öğrenci yok."}</TableEmpty>
          ) : (
            items.map((s) => (
              <TR key={s.id}>
                <TD>
                  <Link href={`/dashboard/students/${s.id}`} className="font-medium text-foreground hover:text-primary hover:underline">
                    {s.fullName}
                  </Link>
                  {s.healthNotes && <span className="ml-2 text-xs text-warning">⚕ sağlık notu</span>}
                  {pilotSimplePeopleMode && !s.userId && <span className="ml-2 text-xs text-muted">· Pilot kayıt · hesap yok</span>}
                </TD>
                <TD className="text-muted">{s.phone || s.email || "—"}</TD>
                <TD><Badge tone={statusTone(s.status)}>{studentStatusLabels[s.status]}</Badge></TD>
                <TD><Badge tone="neutral">{studentSourceLabels[s.source]}</Badge></TD>
                <TD className="text-right">
                  <Link href={`/dashboard/students/${s.id}`} className="text-sm font-medium text-primary hover:underline">Detay →</Link>
                </TD>
              </TR>
            ))
          )}
        </tbody>
      </Table>

      {formOpen && (
        <GuestFormModal student={null} onClose={() => setFormOpen(false)} onSaved={() => { setFormOpen(false); load(search); }} />
      )}
    </div>
  );
}
