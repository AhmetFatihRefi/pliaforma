import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useAuth } from '@/auth/AuthContext';
import { Button, Card, Screen, Subtle, Title } from '@/ui';
import { colors, spacing } from '@/ui/theme';

const roleLabels: Record<string, string> = {
  Owner: 'Stüdyo Sahibi',
  StudioAdmin: 'Yönetici',
  Instructor: 'Eğitmen',
  Student: 'Öğrenci',
};

export default function AccountScreen() {
  const { user, activeStudio, logout } = useAuth();
  const router = useRouter();
  const [busy, setBusy] = useState(false);

  const doLogout = async () => {
    setBusy(true);
    await logout(); // best-effort backend çağrısı + SecureStore ve bellek TAM temizlik
  };

  return (
    <Screen>
      <Title>Hesap</Title>
      <Card>
        <Row label="Ad Soyad" value={user?.fullName ?? '—'} />
        <Row label="E-posta" value={user?.email ?? '—'} />
        <Row label="Stüdyo" value={activeStudio?.studioName ?? '—'} />
        <Row label="Rol" value={activeStudio ? (roleLabels[activeStudio.role] ?? activeStudio.role) : '—'} />
      </Card>
      <Subtle>
        Oturumu kapattığınızda bu cihazdaki oturum bilgileri silinir. “Bu cihazı hatırla” tercihi de
        sıfırlanır; bir sonraki girişte yeniden seçebilirsiniz.
      </Subtle>
      <Button title="Çıkış yap" variant="danger" onPress={doLogout} loading={busy} />
      <Button title="Panele dön" variant="ghost" onPress={() => router.back()} />
    </Screen>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <View style={styles.row}>
      <Text style={styles.label}>{label}</Text>
      <Text style={styles.value}>{value}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.md, paddingVertical: spacing.xs },
  label: { fontSize: 14, color: colors.muted },
  value: { fontSize: 14, fontWeight: '600', color: colors.ink, flexShrink: 1, textAlign: 'right' },
});
