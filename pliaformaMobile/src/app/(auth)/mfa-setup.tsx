// Minimum güvenli MFA kurulumu (spec K3): manualKey metin + kopyala (QR YOK); kod doğrulama;
// recovery kodları BİR KEZ gösterilir ve CİHAZA KAYDEDİLMEZ — kullanıcı onaylayınca devam edilir.
import * as Clipboard from 'expo-clipboard';
import { Redirect } from 'expo-router';
import React, { useEffect, useState } from 'react';
import { StyleSheet, Text } from 'react-native';
import { ApiError } from '@/api/http';
import { useAuth } from '@/auth/AuthContext';
import { Button, Card, ErrorText, Input, Screen, Subtle, Title } from '@/ui';
import { colors } from '@/ui/theme';

export default function MfaSetupScreen() {
  const { status, startMfaSetup, verifyMfaSetup, completeMfaSetup } = useAuth();
  const [manualKey, setManualKey] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (status !== 'mfaSetup' || manualKey) return;
    startMfaSetup()
      .then((r) => setManualKey(r.manualKey))
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Kurulum başlatılamadı. Tekrar deneyin.'));
  }, [status, manualKey, startMfaSetup]);

  if (status !== 'mfaSetup') return <Redirect href="/" />;

  const copyKey = async () => {
    if (!manualKey) return;
    await Clipboard.setStringAsync(manualKey);
    setCopied(true);
  };

  const submit = async () => {
    if (code.trim().length < 6) {
      setError('Authenticator uygulamasındaki 6 haneli kodu girin.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      setRecoveryCodes(await verifyMfaSetup(code.trim()));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Bağlantı kurulamadı. Lütfen tekrar deneyin.');
    } finally {
      setBusy(false);
    }
  };

  if (recoveryCodes) {
    return (
      <Screen>
        <Title>Kurtarma kodlarınız</Title>
        <Subtle>
          Bu kodlar yalnız BİR KEZ gösterilir ve bu cihaza kaydedilmez. Telefonunuza erişemezseniz
          giriş için tek yol bunlardır — güvenli bir yere yazın.
        </Subtle>
        <Card>
          {recoveryCodes.map((rc) => (
            <Text key={rc} selectable style={styles.codeLine}>
              {rc}
            </Text>
          ))}
        </Card>
        <Button title="Kodları güvenli bir yere kaydettim" onPress={completeMfaSetup} />
      </Screen>
    );
  }

  return (
    <Screen>
      <Title>İki adımlı doğrulama kurulumu</Title>
      <Subtle>
        Hesabınız için iki adımlı doğrulama zorunlu. Aşağıdaki anahtarı authenticator uygulamanıza
        (Google Authenticator, 1Password vb.) elle ekleyin, sonra üretilen kodu girin.
      </Subtle>
      <Card>
        <Text selectable style={styles.manualKey}>
          {manualKey ?? 'Anahtar hazırlanıyor…'}
        </Text>
        <Button
          title={copied ? 'Kopyalandı ✓' : 'Anahtarı kopyala'}
          variant="ghost"
          onPress={copyKey}
          disabled={!manualKey}
        />
      </Card>
      <Input
        label="Doğrulama kodu"
        value={code}
        onChangeText={setCode}
        keyboardType="number-pad"
        maxLength={6}
        placeholder="000000"
        editable={!busy && !!manualKey}
        onSubmitEditing={submit}
      />
      <ErrorText>{error}</ErrorText>
      <Button title="Etkinleştir" onPress={submit} loading={busy} disabled={!manualKey} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  manualKey: {
    fontSize: 16,
    fontVariant: ['tabular-nums'],
    letterSpacing: 1.5,
    color: colors.ink,
    textAlign: 'center',
  },
  codeLine: { fontSize: 16, fontVariant: ['tabular-nums'], color: colors.ink, textAlign: 'center' },
});
