const DEFAULT_RETURN_PATH = "/";
const STORAGE_KEY = "post_login_return_url";

function hasWindow(): boolean {
  return typeof window !== "undefined";
}

/**
 * Normalizes a return URL so we never redirect outside of our SPA origin.
 */
export function normalizeReturnPath(candidate?: string | null): string {
  if (!candidate || !candidate.trim()) return DEFAULT_RETURN_PATH;

  const raw = candidate.trim();

  // Reject obvious protocols
  if (/^https?:\/\//i.test(raw)) {
    try {
      const url = new URL(raw);
      if (!hasWindow() || url.origin !== window.location.origin) return DEFAULT_RETURN_PATH;
      return url.pathname + url.search + url.hash || DEFAULT_RETURN_PATH;
    } catch {
      return DEFAULT_RETURN_PATH;
    }
  }

  try {
    if (raw.startsWith("/")) return raw;
    const url = hasWindow() ? new URL(raw, window.location.origin) : null;
    if (!url) return DEFAULT_RETURN_PATH;
    if (url.origin !== window.location.origin) return DEFAULT_RETURN_PATH;
    return url.pathname + url.search + url.hash || DEFAULT_RETURN_PATH;
  } catch {
    return DEFAULT_RETURN_PATH;
  }
}

export function persistReturnPath(path?: string | null) {
  if (!hasWindow()) return;
  const normalized = normalizeReturnPath(path);
  try {
    window.sessionStorage.setItem(STORAGE_KEY, normalized);
  } catch {
    // ignore
  }
}

export function consumePersistedReturnPath(): string | undefined {
  if (!hasWindow()) return undefined;
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (raw) {
      window.sessionStorage.removeItem(STORAGE_KEY);
      return normalizeReturnPath(raw);
    }
  } catch {
    // ignore
  }
  return undefined;
}

export const RETURN_PATH_STORAGE_KEY = STORAGE_KEY;
