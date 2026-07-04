// Bootstrap ekranı: auth durumuna göre yönlendirir. Durum → rota eşlemesi tek yerde.
import { Redirect, type Href } from 'expo-router';
import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { useAuth, type AuthStatus } from '@/auth/AuthContext';
import { Button, Screen, Subtle, Title } from '@/ui';
import { colors } from '@/ui/theme';

const routeFor: Partial<Record<AuthStatus, Href>> = {
  signedOut: '/login',
  mfaChallenge: '/mfa-challenge',
  mfaSetup: '/mfa-setup',
  selectStudio: '/select-studio',
  signedIn: '/dashboard',
};

export default function Index() {
  const { status, retryBootstrap } = useAuth();

  if (status === 'bootstrapping') {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg }}>
        <ActivityIndicator size="large" color={colors.roseDark} />
      </View>
    );
  }

  if (status === 'offline') {
    return (
      <Screen scroll={false}>
        <Title>Bağlantı kurulamadı</Title>
        <Subtle>İnternet bağlantınızı kontrol edip tekrar deneyin. Oturumunuz korunuyor.</Subtle>
        <Button title="Tekrar dene" onPress={retryBootstrap} />
      </Screen>
    );
  }

  return <Redirect href={routeFor[status] ?? '/login'} />;
}
