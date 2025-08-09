export type OidcTokens = {
  access_token: string;
  id_token?: string;
  refresh_token?: string;
  expires_at?: number;
};

export type TelegramAuthPayload = {
  id: number;
  username?: string;          // <- optional
  first_name: string;
  last_name?: string | null;
  photo_url?: string | null;
  auth_date: number;          // unix seconds
  hash: string;
};
