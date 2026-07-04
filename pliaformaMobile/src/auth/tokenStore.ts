// SecureStore erişiminin TEK modülü (mobil AGENTS.md kuralı: refresh + trusted-device token YALNIZ
// OS güvenli deposunda — iOS Keychain / Android Keystore. AsyncStorage'da token SAKLANMAZ).
// Access token burada DURMAZ; yalnız bellekte tutulur (bkz. session.ts).
import * as SecureStore from 'expo-secure-store';

const REFRESH_KEY = 'pf_refresh_token';
const TRUSTED_KEY = 'pf_trusted_device_token';

export const tokenStore = {
  getRefreshToken: () => SecureStore.getItemAsync(REFRESH_KEY),

  /** Rotasyon kuralı: yeni refresh token belleğe alınmadan ÖNCE buraya yazılır (atomik yazım). */
  setRefreshToken: (raw: string) => SecureStore.setItemAsync(REFRESH_KEY, raw),

  getTrustedDeviceToken: () => SecureStore.getItemAsync(TRUSTED_KEY),
  setTrustedDeviceToken: (raw: string) => SecureStore.setItemAsync(TRUSTED_KEY, raw),

  /** Logout / oturum düşmesi: her iki token da silinir (tam temizlik). */
  clearAll: async () => {
    await SecureStore.deleteItemAsync(REFRESH_KEY);
    await SecureStore.deleteItemAsync(TRUSTED_KEY);
  },
};
