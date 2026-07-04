import { Redirect, useRouter } from 'expo-router';
import React, { useState } from 'react';
import { ApiError } from '@/api/http';
import { useAuth } from '@/auth/AuthContext';
import { Button, ErrorText, Input, Screen, Subtle, Title } from '@/ui';

export default function RecoveryCodeScreen() {
  const { status, verifyRecoveryCode } = useAuth();
  const router = useRouter();
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (status !== 'mfaChallenge') return <Redirect href="/" />;

  const submit = async () => {
    if (!code.trim()) {
      setError('Kurtarma kodunuzu girin.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      // Kurtarma kodu tek kullanımlıktır; bu yol güvenilir cihaz OLUŞTURMAZ.
      await verifyRecoveryCode(code.trim());
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Bağlantı kurulamadı. Lütfen tekrar deneyin.');
      setBusy(false);
    }
  };

  return (
    <Screen>
      <Title>Kurtarma kodu</Title>
      <Subtle>
        Authenticator uygulamanıza erişemiyorsanız, MFA kurulumunda size verilen tek kullanımlık
        kurtarma kodlarından birini girin.
      </Subtle>
      <Input
        label="Kurtarma kodu"
        value={code}
        onChangeText={setCode}
        autoCapitalize="characters"
        autoCorrect={false}
        placeholder="XXXXX-XXXXX"
        editable={!busy}
        onSubmitEditing={submit}
        autoFocus
      />
      <ErrorText>{error}</ErrorText>
      <Button title="Giriş yap" onPress={submit} loading={busy} />
      <Button title="Geri dön" variant="ghost" onPress={() => router.back()} />
    </Screen>
  );
}
