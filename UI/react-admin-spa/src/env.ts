export const ENV = {
  AUTH_AUTHORITY: (import.meta.env.VITE_AUTH_AUTHORITY as string) ?? "",
  AUTH_CLIENT_ID: (import.meta.env.VITE_AUTH_CLIENT_ID as string) ?? "",
  AUTH_REDIRECT_URI: (import.meta.env.VITE_AUTH_REDIRECT_URI as string) ?? "",
  AUTH_SILENT_REDIRECT_URI: (import.meta.env.VITE_AUTH_SILENT_REDIRECT_URI as string) ?? "",
  AUTH_POST_LOGOUT_REDIRECT_URI: (import.meta.env.VITE_AUTH_POST_LOGOUT_REDIRECT_URI as string) ?? "",
  API_BASE: (import.meta.env.VITE_API_BASE as string) ?? "",
  // Vite exposes `MODE`, not `NODE_ENV`
  NODE_ENV: (import.meta.env.MODE as string) ?? "production",
};

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
  
