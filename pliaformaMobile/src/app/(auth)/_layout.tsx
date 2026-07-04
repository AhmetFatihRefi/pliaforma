// Auth grubu: giriş öncesi ekranlar. Oturum açıksa app'e yönlendirir (UX guard'ı; asıl yetki backend'de).
import { Redirect, Stack } from 'expo-router';
import React from 'react';
import { useAuth } from '@/auth/AuthContext';
import { colors } from '@/ui/theme';

export default function AuthLayout() {
  const { status } = useAuth();
  if (status === 'signedIn') return <Redirect href="/dashboard" />;
  return <Stack screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.bg } }} />;
}
