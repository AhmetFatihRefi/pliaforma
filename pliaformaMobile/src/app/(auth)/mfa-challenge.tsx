import { Redirect, useRouter } from 'expo-router';
import React, { useState } from 'react';
import { StyleSheet, Switch, Text, View } from 'react-native';
import { ApiError } from '@/api/http';
import { useAuth } from '@/auth/AuthContext';
import { Button, ErrorText, Input, Screen, Subtle, Title } from '@/ui';
import { colors, spacing } from '@/ui/theme';

export default function MfaChallengeScreen() {
  const { status, verifyMfaCode, cancelChallenge } = useAuth();
  const router = useRouter();
  const [code, setCode] = useState('');
  // Spec kararı: "bu cihazı hatırla" DEFAULT KAPALI; açıksa trusted token SecureStore'a alınır.
  const [rememberDevice, setRememberDevice] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status !== 'mfaChallenge') return <Redirect href="/" />;

  const submit = async () => {
    if (code.trim().length < 6) {
      setError('Authenticator uygulamasındaki 6 haneli kodu girin.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await verifyMfaCode(code.trim(), rememberDevice);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Bağlantı kurulamadı. Lütfen tekrar deneyin.');
      setBusy(false);
    }
  };

  return (
    <Screen>
      <Title>Doğrulama kodu</Title>
      <Subtle>Authenticator uygulamanızdaki 6 haneli kodu girin.</Subtle>
      <Input
        label="Kod"
        value={code}
        onChangeText={setCode}
        keyboardType="number-pad"
        maxLength={6}
        placeholder="000000"
        editable={!busy}
        onSubmitEditing={submit}
        autoFocus
      />
      <View style={styles.row}>
        <Text style={styles.rememberLabel}>Bu cihazı 30 gün hatırla</Text>
        <Switch
          value={rememberDevice}
          onValueChange={setRememberDevice}
          trackColor={{ true: colors.rose }}
          disabled={busy}
        />
      </View>
      <ErrorText>{error}</ErrorText>
      <Button title="Doğrula" onPress={submit} loading={busy} />
      <Button title="Kurtarma kodu kullan" variant="ghost" onPress={() => router.push('/recovery-code')} />
      {/* F2: kod süresi dolduysa/ekran istenmeden açıldıysa çıkış yolu — state sıfırlanır, login'e döner. */}
      <Button title="Girişe dön" variant="ghost" onPress={cancelChallenge} disabled={busy} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: spacing.sm },
  rememberLabel: { fontSize: 15, color: colors.ink },
});
