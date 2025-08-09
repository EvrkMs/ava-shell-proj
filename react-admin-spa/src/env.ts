export const ENV = {
    AUTH_AUTHORITY: import.meta.env.VITE_AUTH_AUTHORITY as string,
    AUTH_CLIENT_ID: import.meta.env.VITE_AUTH_CLIENT_ID as string,
    AUTH_REDIRECT_URI: import.meta.env.VITE_AUTH_REDIRECT_URI as string,
    AUTH_SILENT_REDIRECT_URI: import.meta.env.VITE_AUTH_SILENT_REDIRECT_URI as string,
    AUTH_POST_LOGOUT_REDIRECT_URI: import.meta.env.VITE_AUTH_POST_LOGOUT_REDIRECT_URI as string,
    API_BASE: import.meta.env.VITE_API_BASE as string,
  };
  