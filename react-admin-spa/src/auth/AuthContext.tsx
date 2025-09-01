import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  useRef,
} from "react";
import type { User } from "oidc-client-ts";
import { User as OidcUser } from "oidc-client-ts";
import { userManager } from "./oidc";
import type { TelegramAuthPayload, OidcTokens } from "./types";
import { setAuthToken } from "../api";
import { ENV } from "../env";

type AuthState = {
  isAuthenticated: boolean;
  accessToken?: string;
  idToken?: string;
  profile?: any;
  authSource?: "oidc" | "telegram";
  bootstrapped: boolean;
  isLoading: boolean;
  error?: string;
};

type AuthContextType = {
  state: AuthState;
  signinPkce: () => Promise<void>;
  signout: () => Promise<void>;
  completeSignin: () => Promise<void>;
  completeSignout: () => Promise<void>;
  loginWithTelegram: (payload: TelegramAuthPayload) => Promise<void>;
  clearError: () => void;
};

const AuthContext = createContext<AuthContextType>(null as any);

// === helpers ===
function base64UrlDecode(input: string): string {
  let s = input.replace(/-/g, "+").replace(/_/g, "/");
  const pad = s.length % 4;
  if (pad) s += "=".repeat(4 - pad);
  return atob(s);
}

function parseJwt(token?: string): Record<string, any> | undefined {
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

async function fetchUserInfo(accessToken: string) {
  const resp = await fetch(`${ENV.AUTH_AUTHORITY}/connect/userinfo`, {
    headers: { Authorization: `Bearer ${accessToken}` },
  });
  if (!resp.ok) {
    throw new Error(`userinfo failed: ${resp.status}`);
  }
  return await resp.json();
}

export const AuthProvider: React.FC<React.PropsWithChildren> = ({ children }) => {
  const [state, setState] = useState<AuthState>({ 
    isAuthenticated: false, 
    bootstrapped: false, 
    isLoading: false 
  });
  const initializedRef = useRef(false);

  const setError = useCallback((error: string) => {
    setState(s => ({ ...s, error, isLoading: false }));
  }, []);

  const clearError = useCallback(() => {
    setState(s => ({ ...s, error: undefined }));
  }, []);

  const setLoading = useCallback((isLoading: boolean) => {
    setState(s => ({ ...s, isLoading }));
  }, []);

  const syncFromUser = useCallback((u: User | null) => {
    if (u?.access_token) {
      setState({
        isAuthenticated: true,
        accessToken: u.access_token,
        idToken: u.id_token,
        profile: u.profile,
        authSource: "oidc",
        bootstrapped: true,
        isLoading: false,
        error: undefined,
      });
      setAuthToken(u.access_token);
    } else {
      setState({ 
        isAuthenticated: false, 
        bootstrapped: true, 
        isLoading: false,
        error: undefined,
      });
      setAuthToken(undefined);
    }
  }, []);

  useEffect(() => {
    if (initializedRef.current) return;
    initializedRef.current = true;

    setLoading(true);

    userManager.getUser()
      .then((u) => {
        syncFromUser(u);
      })
      .catch((error) => {
        console.error("Failed to load user:", error);
        setState(s => ({ ...s, bootstrapped: true, isLoading: false }));
      });

    const onLoaded = (u: User) => syncFromUser(u);
    const onUnloaded = () => syncFromUser(null);

    userManager.events.addUserLoaded(onLoaded);
    userManager.events.addUserUnloaded(onUnloaded);

    return () => {
      userManager.events.removeUserLoaded(onLoaded);
      userManager.events.removeUserUnloaded(onUnloaded);
    };
  }, [syncFromUser, setLoading]);

  const signinPkce = useCallback(async () => {
    try {
      setLoading(true);
      clearError();
      const missing: string[] = [];
      if (!ENV.AUTH_AUTHORITY) missing.push('VITE_AUTH_AUTHORITY');
      if (!ENV.AUTH_CLIENT_ID) missing.push('VITE_AUTH_CLIENT_ID');
      if (!ENV.AUTH_REDIRECT_URI) missing.push('VITE_AUTH_REDIRECT_URI');
      if (missing.length) {
        throw new Error(`OIDC is not configured. Missing: ${missing.join(', ')}`);
      }
      await userManager.signinRedirect();
    } catch (error: any) {
      setError(error?.message || "Ошибка входа через OIDC");
    }
  }, [setLoading, clearError, setError]);

  const signout = useCallback(async () => {
    try {
      setLoading(true);
      clearError();
      
      if (state.idToken) {
        await userManager.signoutRedirect();
      } else {
        // Fallback для telegram_login случая
        await userManager.removeUser();
        syncFromUser(null);
      }
    } catch (error: any) {
      console.error("Signout error:", error);
      // В случае ошибки всё равно пытаемся локально очистить состояние
      await userManager.removeUser();
      syncFromUser(null);
    }
  }, [state.idToken, setLoading, clearError, syncFromUser]);

  const completeSignin = useCallback(async () => {
    try {
      setLoading(true);
      clearError();
      const u = await userManager.signinRedirectCallback();
      syncFromUser(u);
    } catch (error: any) {
      setError(error?.message || "Ошибка завершения входа");
      throw error;
    }
  }, [syncFromUser, setLoading, clearError, setError]);

  const completeSignout = useCallback(async () => {
    try {
      await userManager.signoutRedirectCallback();
    } catch (error) {
      console.error("Signout callback error:", error);
    }
    syncFromUser(null);
  }, [syncFromUser]);

  const loginWithTelegram = useCallback(async (p: TelegramAuthPayload) => {
    try {
      setLoading(true);
      clearError();

      const body = new URLSearchParams();
      body.set("grant_type", "telegram_login");
      body.set("client_id", ENV.AUTH_CLIENT_ID);
      body.set("scope", "openid profile api offline_access");
      body.set("id", String(p.id));
      body.set("auth_date", String(p.auth_date));
      body.set("hash", p.hash);
      if (p.username) body.set("username", p.username);
      if (p.first_name) body.set("first_name", p.first_name);
      if (p.last_name) body.set("last_name", p.last_name);
      if (p.photo_url) body.set("photo_url", p.photo_url);

      const resp = await fetch(`${ENV.AUTH_AUTHORITY}/connect/token`, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body,
        credentials: "include",
      });

      if (!resp.ok) {
        const text = await resp.text();
        throw new Error(`Telegram login failed: ${resp.status} ${text}`);
      }

      const tok = (await resp.json()) as OidcTokens;

      let profile: any = undefined;
      try {
        profile = await fetchUserInfo(tok.access_token);
      } catch {
        profile = parseJwt(tok.access_token);
      }

      const nowSec = Math.floor(Date.now() / 1000);
      const expires_at =
        (tok as any).expires_at ??
        (tok as any).expires_in ? nowSec + Number((tok as any).expires_in) : nowSec + 3600;

      const u = new OidcUser({
        access_token: tok.access_token,
        id_token: tok.id_token,
        token_type: "Bearer",
        scope: "openid profile api offline_access",
        profile: profile ?? {},
        expires_at,
      });

      await userManager.storeUser(u);
      const stored = await userManager.getUser();
      syncFromUser(stored);
    } catch (error: any) {
      setError(error?.message || "Ошибка входа через Telegram");
      throw error;
    }
  }, [syncFromUser, setLoading, clearError, setError]);

  const value = useMemo<AuthContextType>(
    () => ({
      state,
      signinPkce,
      signout,
      completeSignin,
      completeSignout,
      loginWithTelegram,
      clearError,
    }),
    [state, signinPkce, signout, completeSignin, completeSignout, loginWithTelegram, clearError]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => useContext(AuthContext);
