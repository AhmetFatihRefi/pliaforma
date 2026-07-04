// Salt-okunur yönetici dashboard'u (spec K5): GET /api/studio/dashboard + bugünün dersleri.
// Hiçbir write çağrısı YOKTUR. Seans saatleri stüdyo duvar saatidir; yalnız GÖRÜNTÜLEME için
// string'den dilimlenir (Date'e parse edilmez — K6 kuralı yalnız expiry için değil, ilkedir).
import { useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import { Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { api, ApiError } from '@/api/client';
import type { ClassSessionDto, StudioDashboardDto } from '@/api/types';
import { useAuth } from '@/auth/AuthContext';
import { Card, ErrorText, Subtle, Title } from '@/ui';
import { colors, radius, spacing } from '@/ui/theme';

/** Stüdyo takvimi Europe/Istanbul duvar saatiyle çalışır; "bugün" cihaza göre değil stüdyoya göredir. */
function istanbulToday(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(new Date());
}

const hhmm = (wallClock: string) => wallClock.slice(11, 16);
const tl = (amount: number) => `${amount.toLocaleString('tr-TR')} ₺`;

export default function DashboardScreen() {
  const { activeStudio } = useAuth();
  const router = useRouter();
  const [dash, setDash] = useState<StudioDashboardDto | null>(null);
  const [today, setToday] = useState<ClassSessionDto[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const day = istanbulToday();
      const [d, sessions] = await Promise.all([
        api<StudioDashboardDto>('/api/studio/dashboard'),
        api<ClassSessionDto[]>(`/api/studio/class-sessions?from=${day}&to=${day}`),
      ]);
      setDash(d);
      setToday(sessions);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Veriler yüklenemedi. Aşağı çekerek yenileyin.');
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }, [load]);

  return (
    <SafeAreaView style={styles.safe}>
      <ScrollView
        contentContainerStyle={styles.body}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.roseDark} />}
      >
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Title>{activeStudio?.studioName ?? 'Stüdyo'}</Title>
            <Subtle>{dash ? `${dash.month} özeti` : 'Yükleniyor…'}</Subtle>
          </View>
          <Pressable onPress={() => router.push('/account')} hitSlop={12} accessibilityRole="button">
            <Text style={styles.accountLink}>Hesap</Text>
          </Pressable>
        </View>
        <ErrorText>{error}</ErrorText>

        {dash && (
          <>
            <View style={styles.grid}>
              <Stat label="Bugünkü ders" value={String(dash.upcoming.todaySessions)} />
              <Stat label="7 günde ders" value={String(dash.upcoming.upcomingSessionsNext7Days)} />
              <Stat label="Aktif öğrenci" value={String(dash.activeStudents)} />
              <Stat label="Bu ay yeni" value={String(dash.newStudentsThisMonth)} />
            </View>

            <Card>
              <Text style={styles.cardTitle}>Ödemeler ({dash.month})</Text>
              <Row label="Tahsil edilen" value={tl(dash.payments.paidAmount)} tone={colors.green} />
              <Row label="Bekleyen" value={tl(dash.payments.pendingAmount)} />
              <Row label="Geciken" value={tl(dash.payments.overdueAmount)} tone={colors.danger} />
            </Card>

            <Card>
              <Text style={styles.cardTitle}>Bu ay</Text>
              <Row label="Ders" value={`${dash.completedOrPastSessions}/${dash.sessionsThisMonth}`} />
              <Row label="Rezervasyon" value={String(dash.bookingsThisMonth)} />
              <Row
                label="Katılım oranı"
                value={dash.attendance.attendanceRate != null ? `%${Math.round(dash.attendance.attendanceRate * 100)}` : '—'}
              />
              <Row label="Bekleyen telafi" value={String(dash.makeupCredits.totalOutstandingBalance)} />
              <Row label="Kalan paket kredisi" value={String(dash.packages.remainingCreditTotal)} />
            </Card>
          </>
        )}

        <Card>
          <Text style={styles.cardTitle}>Bugünün dersleri</Text>
          {today.length === 0 && <Subtle>Bugün planlanmış ders yok.</Subtle>}
          {today.map((s) => (
            <View key={s.id} style={styles.session}>
              <Text style={styles.sessionTime}>{hhmm(s.startAt)}</Text>
              <View style={styles.sessionBody}>
                <Text style={styles.sessionName}>{s.classTypeName}</Text>
                <Text style={styles.sessionMeta}>
                  {s.instructorName} · {s.bookedCount}/{s.capacity}
                  {s.status !== 'Scheduled' ? ` · ${s.status === 'Cancelled' ? 'İptal' : s.status}` : ''}
                </Text>
              </View>
            </View>
          ))}
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.stat}>
      <Text style={styles.statValue}>{value}</Text>
      <Text style={styles.statLabel}>{label}</Text>
    </View>
  );
}

function Row({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={[styles.rowValue, tone ? { color: tone } : null]}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  body: { padding: spacing.md, gap: spacing.md },
  header: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between' },
  headerText: { gap: spacing.xs, flex: 1 },
  accountLink: { color: colors.roseDark, fontWeight: '600', fontSize: 15, paddingTop: spacing.xs },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  stat: {
    flexBasis: '47%',
    flexGrow: 1,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.md,
    gap: 2,
  },
  statValue: { fontSize: 24, fontWeight: '700', color: colors.roseDark },
  statLabel: { fontSize: 13, color: colors.muted },
  cardTitle: { fontSize: 15, fontWeight: '700', color: colors.ink },
  row: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 2 },
  rowLabel: { fontSize: 14, color: colors.muted },
  rowValue: { fontSize: 14, fontWeight: '600', color: colors.ink },
  session: { flexDirection: 'row', gap: spacing.md, alignItems: 'center', paddingVertical: spacing.xs },
  sessionTime: { fontSize: 15, fontWeight: '700', color: colors.roseDark, width: 48 },
  sessionBody: { flex: 1, gap: 1 },
  sessionName: { fontSize: 15, fontWeight: '600', color: colors.ink },
  sessionMeta: { fontSize: 13, color: colors.muted },
});
