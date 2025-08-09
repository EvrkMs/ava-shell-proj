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
    bootstrapped?: boolean; // <-- добавили
  };
  
  type AuthContextType = {
    state: AuthState;
    signinPkce: () => Promise<void>;
    signout: () => Promise<void>;
    completeSignin: () => Promise<void>;
    completeSignout: () => Promise<void>;
    loginWithTelegram: (payload: TelegramAuthPayload) => Promise<void>;
    signoutLocal: () => void; // «жёсткий» локальный выход (телеграмный кейс)
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
    const [state, setState] = useState<AuthState>({ isAuthenticated: false, bootstrapped: false });
    const initializedRef = useRef(false);
  
    const syncFromUser = useCallback((u: User | null) => {
      if (u?.access_token) {
        setState({
          isAuthenticated: true,
          accessToken: u.access_token,
          idToken: u.id_token,
          profile: u.profile,
          authSource: "oidc",
          bootstrapped: true,                    // <-- отмечаем готовность
        });
        setAuthToken(u.access_token);
      } else {
        setState({ isAuthenticated: false, bootstrapped: true }); // <-- готово, но без юзера
        setAuthToken(undefined);
      }
    }, []);
  
    useEffect(() => {
      if (initializedRef.current) return;
      initializedRef.current = true;
  
      // Поднимаем пользователя из стора oidc-client-ts (единый путь и для PKCE, и для telegram_login)
      userManager.getUser().then((u) => {
        syncFromUser(u);
      }).catch(() => {
        setState(s => ({ ...s, bootstrapped: true }));
      });
  
      const onLoaded = (u: User) => syncFromUser(u);
      const onUnloaded = () => syncFromUser(null);
  
      userManager.events.addUserLoaded(onLoaded);
      userManager.events.addUserUnloaded(onUnloaded);
  
      return () => {
        userManager.events.removeUserLoaded(onLoaded);
        userManager.events.removeUserUnloaded(onUnloaded);
      };
    }, [syncFromUser]);
  
    const signinPkce = useCallback(async () => {
      await userManager.signinRedirect();
    }, []);
  
    const signoutLocal = useCallback(() => {
      // Полностью локально сбрасываем состояние и стор
      userManager.removeUser().catch(() => {});
      setState({ isAuthenticated: false });
      setAuthToken(undefined);
    }, []);
  
    const signout = useCallback(async () => {
      // Пытаемся корректно выйти через end_session (если PKCE/есть id_token)
      if (state.idToken) {
        try {
          await userManager.signoutRedirect();
          return;
        } catch {
          // если что-то пошло не так — fallback
        }
      }
      // Fallback: локовый выход (telegram_login кейс)
      signoutLocal();
    }, [state.idToken, signoutLocal]);
  
    const completeSignin = useCallback(async () => {
      const u = await userManager.signinRedirectCallback();
      syncFromUser(u);
    }, [syncFromUser]);
  
    const completeSignout = useCallback(async () => {
      try {
        await userManager.signoutRedirectCallback();
      } catch {
        // ignore
      }
      syncFromUser(null);
    }, [syncFromUser]);
  
    const loginWithTelegram = useCallback(async (p: TelegramAuthPayload) => {
      // 1) меняем payload на токены через кастомный грант
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
  
      // 2) пробуем userinfo (идеально); если не получилось — парсим JWT
      let profile: any = undefined;
      try {
        profile = await fetchUserInfo(tok.access_token);
      } catch {
        profile = parseJwt(tok.access_token);
      }
  
      // 3) складываем всё в объект User и сохраняем в стор oidc-client-ts
      //    expires_at: либо из ответа, либо вычисляем из expires_in (если есть), либо fallback +3600
      const nowSec = Math.floor(Date.now() / 1000);
      const expires_at =
        (tok as any).expires_at ??
        (tok as any).expires_in ? nowSec + Number((tok as any).expires_in) : nowSec + 3600;
  
      const u = new OidcUser({
        access_token: tok.access_token,
        id_token: tok.id_token, // может отсутствовать при кастомном гранте — это ок
        token_type: "Bearer",
        scope: "openid profile api offline_access",
        profile: profile ?? {},
        expires_at,
      });
  
      await userManager.storeUser(u);
  
      // 4) синхронизируем состояние из стора (единый путь)
      const stored = await userManager.getUser();
      syncFromUser(stored);
    }, [syncFromUser]);
  
    const value = useMemo<AuthContextType>(
      () => ({
        state,
        signinPkce,
        signout,
        completeSignin,
        completeSignout,
        loginWithTelegram,
        signoutLocal,
      }),
      [state, signinPkce, signout, completeSignin, completeSignout, loginWithTelegram, signoutLocal]
    );
  
    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
  };
  
  export const useAuth = () => useContext(AuthContext);
  