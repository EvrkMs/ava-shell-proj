import { UserManager, WebStorageStateStore, Log, User } from "oidc-client-ts";
import { ENV } from "../env";

Log.setLogger(console);
Log.setLevel(Log.WARN);

export const userManager = new UserManager({
  authority: ENV.AUTH_AUTHORITY,
  client_id: ENV.AUTH_CLIENT_ID,
  redirect_uri: ENV.AUTH_REDIRECT_URI,
  post_logout_redirect_uri: ENV.AUTH_POST_LOGOUT_REDIRECT_URI,
  response_type: "code",
  scope: "openid profile api",
  loadUserInfo: true,
  userStore: new WebStorageStateStore({ store: window.sessionStorage }),
  monitorSession: true,
  // silent_redirect_uri: ENV.AUTH_SILENT_REDIRECT_URI, // при необходимости
  automaticSilentRenew: false,
});

export async function getCurrentUser(): Promise<User | null> {
  return await userManager.getUser();
}
