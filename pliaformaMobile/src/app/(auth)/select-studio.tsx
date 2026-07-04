// Koşullu ekran (spec K4): yalnız birden çok üyelik varken görünür; switch-studio cookie'siz/bearer'dır.
import { Redirect } from 'expo-router';
import React, { useState } from 'react';
import { Pressable, StyleSheet, Text } from 'react-native';
import { ApiError } from '@/api/http';
import { useAuth } from '@/auth/AuthContext';
import { ErrorText, Screen, Subtle, Title } from '@/ui';
import { colors, radius, spacing } from '@/ui/theme';

export default function SelectStudioScreen() {
  const { status, memberships, selectStudio } = useAuth();
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (status !== 'selectStudio') return <Redirect href="/" />;

  const pick = async (studioId: string) => {
    setBusyId(studioId);
    setError(null);
    try {
      await selectStudio(studioId);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Bağlantı kurulamadı. Lütfen tekrar deneyin.');
      setBusyId(null);
    }
  };

  return (
    <Screen>
      <Title>Stüdyo seçin</Title>
      <Subtle>Birden fazla stüdyoda üyeliğiniz var. Hangisiyle devam edeceğinizi seçin.</Subtle>
      {memberships.map((m) => (
        <Pressable
          key={m.membershipId}
          onPress={() => pick(m.studioId)}
          disabled={busyId !== null}
          style={({ pressed }) => [styles.item, (pressed || busyId === m.studioId) && styles.itemActive]}
        >
          <Text style={styles.name}>{m.studioName}</Text>
          <Text style={styles.role}>{m.role}</Text>
        </Pressable>
      ))}
      <ErrorText>{error}</ErrorText>
    </Screen>
  );
}

const styles = StyleSheet.create({
  item: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.lg,
    padding: spacing.md,
    gap: spacing.xs,
  },
  itemActive: { borderColor: colors.rose, backgroundColor: colors.roseSoft },
  name: { fontSize: 16, fontWeight: '600', color: colors.ink },
  role: { fontSize: 13, color: colors.muted },
});
