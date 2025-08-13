// src/auth/oidc.ts
import { UserManager, WebStorageStateStore, InMemoryWebStorage, Log } from "oidc-client-ts";
import { ENV } from "../env";

// Включаем логирование в режиме разработки
if (ENV.NODE_ENV === 'development') {
  Log.setLogger(console);
  Log.setLevel(Log.DEBUG);
} else {
  Log.setLevel(Log.WARN);
}

function canUseStorage(s?: Storage): boolean {
  try {
    if (!s) return false;
    const k = "__oidc_test__" + Math.random();
    s.setItem(k, "1");
    s.removeItem(k);
    return true;
  } catch {
    return false;
  }
}

function makeUserStore() {
  let store: Storage | InMemoryWebStorage;

  if (typeof window !== "undefined" && canUseStorage(window.localStorage)) {
    store = window.localStorage;
    console.log('[OIDC] Using localStorage for user store');
  } else if (typeof window !== "undefined" && canUseStorage(window.sessionStorage)) {
    store = window.sessionStorage;
    console.log('[OIDC] Using sessionStorage for user store');
  } else {
    store = new InMemoryWebStorage();
    console.log('[OIDC] Using in-memory storage for user store');
  }

  return new WebStorageStateStore({ store });
}

console.log('[OIDC] Configuration:', {
  authority: ENV.AUTH_AUTHORITY,
  client_id: ENV.AUTH_CLIENT_ID,
  redirect_uri: ENV.AUTH_REDIRECT_URI,
  post_logout_redirect_uri: ENV.AUTH_POST_LOGOUT_REDIRECT_URI,
  current_url: window.location.href,
});

export const userManager = new UserManager({
  // ДОЛЖНО совпадать с SetIssuer(...) на сервере OpenIddict
  authority: ENV.AUTH_AUTHORITY, // например "https://auth.ava-kk.ru"

  client_id: ENV.AUTH_CLIENT_ID, // "react-spa"
  redirect_uri: ENV.AUTH_REDIRECT_URI, // "https://admin.ava-kk.ru/#/callback"
  post_logout_redirect_uri: ENV.AUTH_POST_LOGOUT_REDIRECT_URI, // "https://admin.ava-kk.ru/#/logout-callback"

  // Код + PKCE — именно то, что нужно
  response_type: "code",

  // Скоупы. Если нужны refresh-токены — добавь offline_access.
  scope: "openid profile api", // убрал api:read, оставил базовые

  // OpenIddict отдаёт /connect/userinfo — можно оставлять true
  loadUserInfo: true,

  // Хранилище с fallback
  userStore: makeUserStore(),
  revokeTokensOnSignout: true,
  
  // Обычно для SPA так и оставляют:
  monitorSession: false,

  // Silent renew для HashRouter может работать некорректно, отключаем пока
  automaticSilentRenew: false,
  
  // Дополнительные настройки для лучшей совместимости
  includeIdTokenInSilentRenew: false,
  silent_redirect_uri: ENV.AUTH_SILENT_REDIRECT_URI,
});