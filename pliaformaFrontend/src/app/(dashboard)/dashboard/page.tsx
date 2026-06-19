"use client";

import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@/lib/auth";
import { dashboardService } from "@/lib/studioApi";
import { ApiError } from "@/lib/api";
import {
  type InstructorDashboardDto,
  type StudentDashboardDto,
  type StudioDashboardDto,
  attendanceStatusLabels,
} from "@/lib/domain-types";
import { Alert, Badge, Card } from "@/components/ui";

const thisMonth = () => new Date().toISOString().slice(0, 7);
const fmt = (iso: string) => `${iso.slice(0, 10)} ${iso.slice(11, 16)}`;
const money = (n: number) => `${n.toLocaleString("tr-TR")} ₺`;

export default function DashboardPage() {
  const { user, activeStudio } = useAuth();
  const role = activeStudio?.role;
  const [month, setMonth] = useState(thisMonth());

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold text-foreground">
            Merhaba, {user?.fullName?.split(" ")[0] ?? "tekrar hoş geldiniz"} 🌸
          </h1>
          <p className="mt-1 text-sm text-muted">
            {activeStudio ? `${activeStudio.studioName} · ${role}` : "Henüz bir stüdyo bağlamı seçili değil."}
          </p>
        </div>
        <input
          type="month"
          value={month}
          onChange={(e) => setMonth(e.target.value)}
          className="rounded-lg border border-border bg-surface px-2 py-1 text-sm text-foreground"
        />
      </div>

      {role === "Owner" || role === "StudioAdmin" ? (
        <StudioPanel month={month} />
      ) : role === "Instructor" ? (
        <InstructorPanel month={month} />
      ) : role === "Student" ? (
        <StudentPanel month={month} />
      ) : (
        <Card><p className="text-sm text-muted">Dashboard için bir stüdyo rolü gerekli.</p></Card>
      )}
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string | number; hint?: string }) {
  return (
    <Card>
      <p className="text-xs font-medium uppercase tracking-wide text-muted">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-foreground">{value}</p>
      {hint && <p className="mt-0.5 text-xs text-muted">{hint}</p>}
    </Card>
  );
}

function AttendanceBar({ a }: { a: StudioDashboardDto["attendance"] }) {
  const rate = a.attendanceRate != null ? Math.round(a.attendanceRate * 100) : 0;
  return (
    <Card>
      <div className="flex items-center justify-between">
        <p className="text-xs font-medium uppercase tracking-wide text-muted">Katılım ({a.totalMarked})</p>
        <Badge tone={rate >= 70 ? "success" : rate >= 40 ? "warning" : "neutral"}>%{a.attendanceRate != null ? rate : "—"}</Badge>
      </div>
      <div className="mt-2 h-2 overflow-hidden rounded-full bg-surface-muted">
        <div className="h-full bg-success" style={{ width: `${rate}%` }} />
      </div>
      <p className="mt-2 text-xs text-muted">Geldi {a.presentCount} · Gelmedi {a.absentCount} · Mazeretli {a.excusedCount} · Geç {a.lateCancelCount}</p>
    </Card>
  );
}

function StudioPanel({ month }: { month: string }) {
  const [d, setD] = useState<StudioDashboardDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await dashboardService.studio(month)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Dashboard yüklenemedi."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { load(); }, [load]);

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;
  if (err) return <Alert kind="error">{err}</Alert>;
  if (!d) return null;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Öğrenci" value={d.totalStudents} hint={`${d.activeStudents} aktif · ${d.guestStudents} misafir`} />
        <Stat label="Bu ay yeni öğrenci" value={d.newStudentsThisMonth} />
        <Stat label="Bu ay ders" value={d.sessionsThisMonth} hint={`${d.completedOrPastSessions} yapıldı · ${d.cancelledSessions} iptal`} />
        <Stat label="Bu ay rezervasyon" value={d.bookingsThisMonth} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <AttendanceBar a={d.attendance} />
        <Stat label="Ödenen" value={money(d.payments.paidAmount)} hint={`${d.payments.paidCount} ödeme`} />
        <Stat label="Bekleyen" value={money(d.payments.pendingAmount)} hint={`${d.payments.pendingCount} ödeme`} />
        <Stat label="Gecikmiş" value={money(d.payments.overdueAmount)} hint={`${d.payments.overdueCount} ödeme`} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Kalan paket hakkı" value={d.packages.remainingCreditTotal} hint={`${d.packages.activePackageCount} aktif paket`} />
        <Stat label="Telafi kredi bakiyesi" value={d.makeupCredits.totalOutstandingBalance} hint={`+${d.makeupCredits.grantedThisMonth} / -${d.makeupCredits.usedThisMonth} bu ay`} />
        <Stat label="Bugünkü dersler" value={d.upcoming.todaySessions} />
        <Stat label="Yaklaşan 7 gün" value={d.upcoming.upcomingSessionsNext7Days} />
      </div>
    </div>
  );
}

function InstructorPanel({ month }: { month: string }) {
  const [d, setD] = useState<InstructorDashboardDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await dashboardService.myInstructor(month)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Dashboard yüklenemedi."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { load(); }, [load]);

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;
  if (err) return <Alert kind="error">{err}</Alert>;
  if (!d) return null;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-3">
        <Stat label="Bu ay ders" value={d.sessionsThisMonth} hint={`${d.cancelledSessions} iptal`} />
        <Stat label="Rezervasyon" value={d.bookingsCount} />
        <Stat label="Öğrenci sayısı" value={d.totalStudentsTaught} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <Stat label="Geldi" value={d.presentCount} />
        <Stat label="Gelmedi" value={d.absentCount} />
        <Stat label="Mazeretli" value={d.excusedCount} />
      </div>
      <Card>
        <p className="mb-2 text-sm font-semibold text-foreground">Ders tipi dağılımı</p>
        {d.classTypeBreakdown.length === 0 ? <p className="text-sm text-muted">Bu ay ders yok.</p> : (
          <div className="space-y-1">
            {d.classTypeBreakdown.map((c) => (
              <div key={c.classTypeId} className="flex justify-between text-sm">
                <span className="text-foreground">{c.classTypeName}</span>
                <span className="text-muted">{c.sessionCount}</span>
              </div>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

function StudentPanel({ month }: { month: string }) {
  const [d, setD] = useState<StudentDashboardDto | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try { setD(await dashboardService.myStudent(month)); setErr(null); }
    catch (e) { setErr(e instanceof ApiError ? e.message : "Dashboard yüklenemedi."); }
    finally { setLoading(false); }
  }, [month]);
  useEffect(() => { load(); }, [load]);

  if (loading) return <p className="text-sm text-muted">Yükleniyor…</p>;
  if (err) return <Alert kind="error">{err}</Alert>;
  if (!d) return null;

  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Stat label="Kalan paket hakkı" value={d.packageSummary.totalRemainingCredits} hint={`${d.packageSummary.activePackages} aktif paket`} />
        <Stat label="Telafi kredisi" value={d.makeupCreditBalance} />
        <Stat label="Bekleyen ödeme" value={d.paymentSummary ? money(d.paymentSummary.pendingAmount) : "—"} hint={d.paymentSummary?.nextDueDate ? `vade ${d.paymentSummary.nextDueDate}` : undefined} />
        <Stat label="Gecikmiş ödeme" value={d.paymentSummary ? money(d.paymentSummary.overdueAmount) : "—"} />
      </div>
      <AttendanceBar a={d.attendanceSummary} />
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <p className="mb-2 text-sm font-semibold text-foreground">Yaklaşan dersler</p>
          {d.upcomingBookings.length === 0 ? <p className="text-sm text-muted">Yaklaşan ders yok.</p> : (
            <div className="space-y-2">
              {d.upcomingBookings.slice(0, 6).map((b) => (
                <div key={b.bookingId} className="flex justify-between text-sm">
                  <span className="text-foreground">{b.classTypeName}</span>
                  <span className="text-muted">{fmt(b.startAt)}</span>
                </div>
              ))}
            </div>
          )}
        </Card>
        <Card>
          <p className="mb-2 text-sm font-semibold text-foreground">Son katılımlar</p>
          {d.recentAttendance.length === 0 ? <p className="text-sm text-muted">Kayıt yok.</p> : (
            <div className="space-y-2">
              {d.recentAttendance.map((a) => (
                <div key={a.id} className="flex justify-between text-sm">
                  <span className="text-foreground">{a.classTypeName}</span>
                  <span className="text-muted">{a.startAt.slice(0, 10)} · {attendanceStatusLabels[a.status]}</span>
                </div>
              ))}
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
