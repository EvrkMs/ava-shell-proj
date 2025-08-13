import React, { useMemo } from "react";
import { useAuth } from "../../../auth/AuthContext";
import { Paper, Stack, Typography, Button, Chip } from "@mui/material";

type Claims = Record<string, unknown>;

function b64urlToJson(b64url: string) {
  // base64url -> base64
  const b64 = b64url.replace(/-/g, "+").replace(/_/g, "/");
  const json = decodeURIComponent(
    atob(b64)
      .split("")
      .map(c => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
      .join("")
  );
  return JSON.parse(json);
}

function decodeJwt(token?: string): Claims {
  if (!token) return {};
  const parts = token.split(".");
  if (parts.length < 2) return {};
  try { return b64urlToJson(parts[1]); } catch { return {}; }
}

function getString(c: Claims, ...keys: string[]) {
  for (const k of keys) {
    const v = c[k];
    if (typeof v === "string" && v.trim()) return v;
  }
  return "";
}
function getArray(c: Claims, ...keys: string[]) {
  for (const k of keys) {
    const v = c[k];
    if (Array.isArray(v)) return v.map(x => String(x)).filter(Boolean);
    if (typeof v === "string") {
      const parts = v.split(/[,\s]+/).map(s => s.trim()).filter(Boolean);
      if (parts.length) return parts;
    }
  }
  return [];
}
const uniq = (arr: string[]) => Array.from(new Set(arr));

export const PersonalCard: React.FC = () => {
  const { state } = useAuth();

  const userInfo = useMemo(() => {
    // 1) клеймы из id_token/userinfo
    const profileClaims = (state.profile ?? {}) as Claims;
    // 2) клеймы из access_token (в нём у тебя точно есть role)
    const accessClaims = decodeJwt(state.accessToken);

    // 3) объединяем (access перекроет profile при одинаковых ключах)
    const claims: Claims = { ...profileClaims, ...accessClaims };

    // ID: берём sub; sid — это session id, не user id
    const id =
      getString(
        claims,
        "sub",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        "id"
      ) || "";

    // Роли: поддержка "role"/"roles"/WS-Fed-типа
    const roles = uniq([
      ...getArray(claims, "role"),
      ...getArray(claims, "roles"),
      ...getArray(claims, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"),
    ]);

    // Имя
    const fullName =
      getString(claims, "full_name") ||
      getString(claims, "name", "preferred_username") ||
      [getString(claims, "given_name"), getString(claims, "family_name")].filter(Boolean).join(" ") ||
      [getString(claims, "first_name"), getString(claims, "last_name")].filter(Boolean).join(" ") ||
      "";

    const email = getString(claims, "email");

    return { id, roles, fullName, email };
  }, [state.profile, state.accessToken]);

  if (!state.isAuthenticated) {
    return (
      <Paper sx={{ p: 2 }}>
        <Typography color="warning.main">Вы не авторизованы</Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Персональные данные</Typography>
      <Stack spacing={2}>
        <InfoRow label="ID" value={userInfo.id} copyable />
        <InfoRow label="ФИО" value={userInfo.fullName} />
        <InfoRow label="Email" value={userInfo.email} />
        <Stack direction="row" spacing={1} alignItems="center">
          <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
            Роли:
          </Typography>
          <Stack direction="row" spacing={1} flexWrap="wrap">
            {userInfo.roles.length ? (
              userInfo.roles.map((r, i) => <Chip key={`${r}-${i}`} label={r} size="small" variant="outlined" />)
            ) : (
              <Typography>—</Typography>
            )}
          </Stack>
        </Stack>
      </Stack>
    </Paper>
  );
};

const InfoRow: React.FC<{ label: string; value: string; copyable?: boolean }> = ({ label, value, copyable }) => (
  <Stack direction="row" spacing={1} alignItems="center">
    <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
      {label}:
    </Typography>
    <Typography sx={{ wordBreak: copyable ? "break-all" : "normal" }}>
      {value || "—"}
    </Typography>
    {copyable && value && (
      <Button size="small" variant="text" onClick={() => navigator.clipboard.writeText(value)}>
        Копировать
      </Button>
    )}
  </Stack>
);
