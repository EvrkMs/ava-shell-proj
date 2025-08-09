import axios from "axios";
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
