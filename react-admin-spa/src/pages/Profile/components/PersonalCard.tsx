import React, { useMemo } from "react";
import { useAuth } from "../../../auth/AuthContext";
import { Paper, Stack, Typography, Button, Chip } from "@mui/material";
import { decodeJwtClaims, rolesFromClaims } from "../../../utils/jwt";

type Claims = Record<string, unknown>;

function getString(c: Claims, ...keys: string[]) {
  for (const k of keys) {
    const v = c[k];
    if (typeof v === "string" && v.trim()) return v;
  }
  return "";
}

export const PersonalCard: React.FC = () => {
  const { state } = useAuth();

  const userInfo = useMemo(() => {
    // 1) профильные клеймы из id_token/userinfo
    const profileClaims = (state.profile ?? {}) as Claims;
    // 2) клеймы из access_token (часто там роли)
    const accessClaims = (decodeJwtClaims(state.accessToken) ?? {}) as Claims;

    // 3) объединяем (access перекрывает profile при совпадении ключей)
    const claims: Claims = { ...profileClaims, ...accessClaims };

    // ID: обычно sub; sid — session id, id — произвольный маппинг
    const id =
      getString(
        claims,
        "sub",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        "id"
      ) || "";

    // Роли
    const roles = rolesFromClaims(claims as any);

    // ФИО/имя
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
      <Typography variant="h6" gutterBottom>Персональная информация</Typography>
      <Stack spacing={2}>
        <InfoRow label="ID" value={userInfo.id} copyable />
        <InfoRow label="Имя" value={userInfo.fullName} />
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
