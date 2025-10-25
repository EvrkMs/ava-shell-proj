export function base64UrlDecode(input: string): string {
  let s = input.replace(/-/g, "+").replace(/_/g, "/");
  const pad = s.length % 4;
  if (pad) s += "=".repeat(4 - pad);
  return atob(s);
}

export function decodeJwtClaims(token?: string): Record<string, any> | undefined {
  try {
    if (!token) return undefined;
    const parts = token.split(".");
    if (parts.length < 2) return undefined;
    const json = base64UrlDecode(parts[1]);
    return JSON.parse(json);
  } catch {
    return undefined;
  }
}

function ensureStringArray(value: unknown): string[] {
  if (Array.isArray(value)) return value.map(v => String(v)).filter(Boolean);
  if (typeof value === "string") return [value].filter(Boolean);
  return [];
}

export function rolesFromClaims(claims?: Record<string, any>): string[] {
  if (!claims) return [];
  const sources = [
    claims["role"],
    claims["roles"],
    claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"],
  ];
  const roles = sources.flatMap(ensureStringArray).map(r => r.trim()).filter(Boolean);
  return Array.from(new Set(roles));
}
