// Centralised SPA configuration. We embed the production URLs directly so Docker/static hosting does not rely on .env files.
const AUTH_AUTHORITY = "https://auth.ava-kk.ru";
const AUTH_CLIENT_ID = "react-spa";
const AUTH_REDIRECT_URI = "https://admin.ava-kk.ru/callback";
const AUTH_SILENT_REDIRECT_URI = "https://admin.ava-kk.ru/silent-callback.html";
const AUTH_POST_LOGOUT_REDIRECT_URI = "https://admin.ava-kk.ru/logout-callback";
const API_BASE = "https://auth.ava-kk.ru";

export const ENV = {
  AUTH_AUTHORITY,
  AUTH_CLIENT_ID,
  AUTH_REDIRECT_URI,
  AUTH_SILENT_REDIRECT_URI,
  AUTH_POST_LOGOUT_REDIRECT_URI,
  API_BASE,
  // Vite exposes `MODE`, not `NODE_ENV`
  NODE_ENV: (import.meta.env.MODE as string) ?? "production",
} as const;

export function validateOidcEnv() {
  const missing: string[] = [];
  const entries: Array<[string, string | undefined]> = [
    ["VITE_AUTH_AUTHORITY", ENV.AUTH_AUTHORITY],
    ["VITE_AUTH_CLIENT_ID", ENV.AUTH_CLIENT_ID],
    ["VITE_AUTH_REDIRECT_URI", ENV.AUTH_REDIRECT_URI],
    ["VITE_AUTH_POST_LOGOUT_REDIRECT_URI", ENV.AUTH_POST_LOGOUT_REDIRECT_URI],
  ];
  for (const [k, v] of entries) if (!v) missing.push(k);
  return { ok: missing.length === 0, missing };
}
  
