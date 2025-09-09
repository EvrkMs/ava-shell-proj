import axios from "axios";
import { userManager } from "./auth/oidc";
import { ENV } from "./env";

export const api = axios.create({
  baseURL: ENV.API_BASE,
  withCredentials: false,
});
export const apiValid = axios.create({
  baseURL: "https://cheack.ava-kk.ru",
  withCredentials: false,
});
export function setAuthToken(token?: string) {
  if (token) {
    api.defaults.headers.common["Authorization"] = `Bearer ${token}`;
  } else {
    delete api.defaults.headers.common["Authorization"];
  }
}

// Auto-logout on 401 responses
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    try {
      const status = error?.response?.status;
      if (status === 401) {
        try { await userManager.removeUser(); } catch {}
        // Force reload to clear in-memory state and kick login if needed
        window.location.replace("/");
      }
    } catch {}
    return Promise.reject(error);
  }
);
export async function validateToken(token?: string) {
  const headers: Record<string, string> = {};
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await apiValid.get("/api/users", { headers }); // токен уходит в этом запросе
  return res.data as { sub?: string; scopes?: string | string[]; roles?: string[] };
}
