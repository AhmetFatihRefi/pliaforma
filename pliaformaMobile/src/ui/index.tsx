// Küçük, kütüphanesiz UI seti — web design-system token'larıyla uyumlu (bkz. theme.ts).
import React from 'react';
import {
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { colors, radius, spacing } from './theme';

export function Screen({ children, scroll = true }: { children: React.ReactNode; scroll?: boolean }) {
  const content = scroll ? (
    <ScrollView contentContainerStyle={styles.scrollBody} keyboardShouldPersistTaps="handled">
      {children}
    </ScrollView>
  ) : (
    <View style={styles.scrollBody}>{children}</View>
  );
  return (
    <SafeAreaView style={styles.safe}>
      <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        {content}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

export function Title({ children }: { children: React.ReactNode }) {
  return <Text style={styles.title}>{children}</Text>;
}

export function Subtle({ children }: { children: React.ReactNode }) {
  return <Text style={styles.subtle}>{children}</Text>;
}

export function ErrorText({ children }: { children: React.ReactNode }) {
  return children ? <Text style={styles.error}>{children}</Text> : null;
}

export function Card({ children }: { children: React.ReactNode }) {
  return <View style={styles.card}>{children}</View>;
}

interface ButtonProps {
  title: string;
  onPress: () => void;
  loading?: boolean;
  disabled?: boolean;
  variant?: 'primary' | 'ghost' | 'danger';
}

export function Button({ title, onPress, loading, disabled, variant = 'primary' }: ButtonProps) {
  const inactive = disabled || loading;
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      disabled={inactive}
      style={({ pressed }) => [
        styles.btn,
        variant === 'primary' && styles.btnPrimary,
        variant === 'ghost' && styles.btnGhost,
        variant === 'danger' && styles.btnDanger,
        (pressed || inactive) && styles.btnDim,
      ]}
    >
      {loading ? (
        <ActivityIndicator color={variant === 'ghost' ? colors.roseDark : '#fff'} />
      ) : (
        <Text style={[styles.btnText, variant === 'ghost' && styles.btnGhostText]}>{title}</Text>
      )}
    </Pressable>
  );
}

interface InputProps extends TextInputProps {
  label: string;
  error?: string | null;
}

export function Input({ label, error, ...props }: InputProps) {
  return (
    <View style={styles.field}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        placeholderTextColor={colors.muted}
        style={[styles.input, error ? styles.inputError : null]}
        {...props}
      />
      <ErrorText>{error}</ErrorText>
    </View>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  flex: { flex: 1 },
  scrollBody: { flexGrow: 1, padding: spacing.lg, justifyContent: 'center', gap: spacing.md },
  title: { fontSize: 24, fontWeight: '700', color: colors.ink },
  subtle: { fontSize: 14, color: colors.muted, lineHeight: 20 },
  error: { color: colors.danger, fontSize: 13, marginTop: spacing.xs },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: spacing.md,
    gap: spacing.sm,
  },
  btn: {
    borderRadius: radius.md,
    paddingVertical: 14,
    alignItems: 'center',
    justifyContent: 'center',
  },
  btnPrimary: { backgroundColor: colors.roseDark },
  btnDanger: { backgroundColor: colors.danger },
  btnGhost: { backgroundColor: colors.roseSoft },
  btnDim: { opacity: 0.6 },
  btnText: { color: '#fff', fontWeight: '600', fontSize: 16 },
  btnGhostText: { color: colors.roseDark },
  field: { gap: spacing.xs },
  label: { fontSize: 14, fontWeight: '600', color: colors.ink },
  input: {
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: 12,
    fontSize: 16,
    color: colors.ink,
  },
  inputError: { borderColor: colors.danger },
});
