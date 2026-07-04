import { Redirect } from 'expo-router';
import React, { useState } from 'react';
import { ApiError } from '@/api/http';
import { useAuth } from '@/auth/AuthContext';
import { Button, ErrorText, Input, Screen, Subtle, Title } from '@/ui';

export default function LoginScreen() {
  const { status, login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Login sonucu challenge/setup/studio gerektirirse index üzerinden doğru ekrana akar.
  if (status !== 'signedOut' && status !== 'bootstrapping' && status !== 'offline') {
    return <Redirect href="/" />;
  }

  const submit = async () => {
    if (!email.trim() || !password) {
      setError('E-posta ve parola gerekli.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await login(email.trim(), password);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Bağlantı kurulamadı. Lütfen tekrar deneyin.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen>
      <Title>Pliaforma</Title>
      <Subtle>Stüdyo yönetimine giriş yapın.</Subtle>
      <Input
        label="E-posta"
        value={email}
        onChangeText={setEmail}
        autoCapitalize="none"
        autoComplete="email"
        keyboardType="email-address"
        placeholder="ornek@studyo.com"
        editable={!busy}
      />
      <Input
        label="Parola"
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        autoComplete="current-password"
        placeholder="••••••••"
        editable={!busy}
        onSubmitEditing={submit}
      />
      <ErrorText>{error}</ErrorText>
      <Button title="Giriş yap" onPress={submit} loading={busy} />
    </Screen>
  );
}
