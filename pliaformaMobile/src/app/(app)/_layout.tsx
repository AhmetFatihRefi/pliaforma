// Uygulama grubu: yalnız oturum açıkken. UX guard'ı — asıl yetki her istekte backend'de.
import { Redirect, Stack } from 'expo-router';
import React from 'react';
import { useAuth } from '@/auth/AuthContext';
import { colors } from '@/ui/theme';

export default function AppLayout() {
  const { status } = useAuth();
  if (status !== 'signedIn') return <Redirect href="/" />;
  return <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }} />;
}
