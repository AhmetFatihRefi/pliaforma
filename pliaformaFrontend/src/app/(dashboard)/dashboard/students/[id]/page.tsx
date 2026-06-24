"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { contractService, studentService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import { useRole } from "@/lib/useRole";
import {
  type StudentContractLineDto,
  type StudentDto,
  studentContractStatusLabels,
  studentLevelLabels,
  studentSourceLabels,
  studentStatusLabels,
} from "@/lib/domain-types";
import {
  Alert,
  Badge,
  Button,
  Card,
  Table,
  TableEmpty,
  TD,
  TH,
  THead,
  TR,
  useToast,
} from "@/components/ui";
import {
  AddContractLineModal,
  AttendanceSummaryModal,
  ContractDetailModal,
  ConvertModal,
  GuestFormModal,
  statusTone,
  StudentFinanceModal,
} from "../_components";

const money = (n: number, cur: string) => `${n.toLocaleString("tr-TR")} ${cur}`;

export default function StudentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { canManage } = useRole();
  const toast = useToast();

  const [student, setStudent] = useState<StudentDto | null>(null);
  const [lines, setLines] = useState<StudentContractLineDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState<string | null>(null);

  const [manageId, setManageId] = useState<string | null>(null); // detay/yönetim modalı
  const [addOpen, setAddOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [convertOpen, setConvertOpen] = useState(false);
  const [financeOpen, setFinanceOpen] = useState(false);
  const [attendanceOpen, setAttendanceOpen] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [s, ls] = await Promise.all([
        studentService.get(id),
        contractService.lines(id).catch(() => [] as StudentContractLineDto[]),
      ]);
      setStudent(s);
      setLines(ls);
      setErr(null);
    } catch (e) {
      setErr(e instanceof ApiError ? e.message : "Öğrenci yüklenemedi.");
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => { load(); }, [load]);

  const toggleActive = async () => {
    if (!student) return;
    try {
      if (student.isActive) await studentService.deactivate(id);
      else await studentService.reactivate(id);
      await load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "İşlem başarısız.");
    }
  };

  // Yönetim modalı her zaman güncel satır verisini gösterir (load sonrası otomatik tazelenir).
  const manageLine = lines.find((l) => l.id === manageId) ?? null;

  if (!canManage)
    return <div className="mx-auto max-w-5xl"><Card><p className="text-sm text-muted">Bu sayfa yalnızca stüdyo yöneticileri içindir.</p></Card></div>;

  if (loading && !student)
    return <div className="mx-auto max-w-5xl"><p className="text-sm text-muted">Yükleniyor…</p></div>;

  if (!student)
    return (
      <div className="mx-auto max-w-5xl space-y-3">
        <Alert kind="error">{err ?? "Öğrenci bulunamadı."}</Alert>
        <Link href="/dashboard/students" className="text-sm text-primary hover:underline">← Öğrenciler listesine dön</Link>
      </div>
    );

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <Link href="/dashboard/students" className="inline-flex items-center gap-1 text-sm text-muted hover:text-foreground">← Öğrenciler</Link>

      {/* Öğrenci başlığı */}
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="min-w-0 space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold text-foreground">{student.fullName}</h1>
              <Badge tone={statusTone(student.status)}>{studentStatusLabels[student.status]}</Badge>
              {student.level && <Badge tone="neutral">{studentLevelLabels[student.level]}</Badge>}
              <Badge tone="neutral">{studentSourceLabels[student.source]}</Badge>
            </div>
            <p className="text-sm text-muted">
              {student.phone || "telefon yok"} · {student.email || "e-posta yok"}
            </p>
            {student.healthNotes && (
              <p className="rounded-lg bg-warning/10 px-3 py-1.5 text-xs text-warning">⚕ Sağlık notu: {student.healthNotes}</p>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="secondary" size="sm" onClick={() => setEditOpen(true)}>Düzenle</Button>
            {student.status === "Guest" && <Button variant="secondary" size="sm" onClick={() => setConvertOpen(true)}>Hesaba bağla</Button>}
            <Button variant="ghost" size="sm" onClick={toggleActive}>{student.isActive ? "Pasifleştir" : "Aktifleştir"}</Button>
          </div>
        </div>
        <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4">
          <Button variant="ghost" size="sm" onClick={() => setFinanceOpen(true)}>₺ Ödemeler</Button>
          <Button variant="ghost" size="sm" onClick={() => setAttendanceOpen(true)}>✔ Katılım özeti</Button>
        </div>
      </Card>

      {err && <Alert kind="error">{err}</Alert>}

      {/* Sözleşmeler */}
      <Card>
        <div className="mb-3 flex items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-foreground">Sözleşmeler</h2>
            <p className="text-xs text-muted">Her satır bir paket/sözleşmedir. Detay için satırı açın; programı oradan oluşturabilirsiniz.</p>
          </div>
          <Button size="sm" onClick={() => setAddOpen(true)}>+ Yeni sözleşme</Button>
        </div>

        <Table>
          <THead>
            <TR>
              <TH>Ders grubu</TH>
              <TH>Hoca</TH>
              <TH>Tarih</TH>
              <TH>Ders (kalan/toplam)</TH>
              <TH>Kalan ücret</TH>
              <TH>Durum</TH>
              <TH className="text-right">İşlem</TH>
            </TR>
          </THead>
          <tbody>
            {lines.length === 0 ? (
              <TableEmpty colSpan={7}>Henüz sözleşme yok. “+ Yeni sözleşme” ile ekleyin.</TableEmpty>
            ) : (
              lines.map((l) => (
                <TR key={l.id}>
                  <TD>
                    <span className="font-medium">{l.classTypeName}</span>
                    {l.preferredMaxCapacity != null && (
                      <Badge tone="warning">{l.preferredMaxCapacity === 1 ? "Bireysel" : `≤${l.preferredMaxCapacity}`}</Badge>
                    )}
                  </TD>
                  <TD className="text-muted">{l.instructorName ?? "Herhangi"}</TD>
                  <TD className="text-muted">{l.startDate} → {l.endDate}</TD>
                  <TD>{l.remainingLessonRight}/{l.lessonCount}</TD>
                  <TD className={l.remaining > 0 ? "text-danger" : "text-success"}>{money(l.remaining, l.currency)}</TD>
                  <TD><Badge tone={l.status === "Active" ? "success" : "neutral"}>{studentContractStatusLabels[l.status]}</Badge></TD>
                  <TD className="text-right">
                    <Button variant="secondary" size="sm" onClick={() => setManageId(l.id)}>Yönet</Button>
                  </TD>
                </TR>
              ))
            )}
          </tbody>
        </Table>
      </Card>

      {addOpen && (
        <AddContractLineModal studentId={id} onClose={() => setAddOpen(false)} onSaved={() => { setAddOpen(false); load(); }} />
      )}
      {manageLine && (
        <ContractDetailModal line={manageLine} onClose={() => setManageId(null)} onChanged={load} />
      )}
      {editOpen && (
        <GuestFormModal student={student} onClose={() => setEditOpen(false)} onSaved={() => { setEditOpen(false); load(); }} />
      )}
      {convertOpen && (
        <ConvertModal student={student} onClose={() => setConvertOpen(false)} onDone={() => { setConvertOpen(false); load(); }} />
      )}
      {financeOpen && <StudentFinanceModal student={student} onClose={() => setFinanceOpen(false)} />}
      {attendanceOpen && <AttendanceSummaryModal student={student} onClose={() => setAttendanceOpen(false)} />}
    </div>
  );
}
