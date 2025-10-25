import axios from "axios";
import { userManager } from "./auth/oidc";
import { ENV } from "./env";

export const api = axios.create({
  baseURL: ENV.API_BASE,
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
        try {
          await userManager.removeUser();
        } catch {
          // ignore cleanup issues; auth context listens for unload events
        }
      }
    } catch {
      // ignore secondary failures
    }
    return Promise.reject(error);
  }
);
