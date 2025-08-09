import React, { useMemo } from "react";
import { useAuth } from "../../../auth/AuthContext";
import { Paper, Stack, Typography, Button } from "@mui/material";

export const PersonalCard: React.FC = () => {
  const { state } = useAuth();

  // Берём клеймы только из userinfo (state.profile)
  const claims = state.profile ?? {};

  const person = useMemo(() => {
    const sub =
      claims.sub ??
      claims.sid ?? // на всякий
      "";

    // role может быть строкой, массивом или отсутствовать
    const roleClaim =
      claims.role ??
      claims.roles ??
      claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
      "";

    const role = Array.isArray(roleClaim)
      ? roleClaim.join(", ")
      : String(roleClaim || "");

    // Имя: берём name, если нет — собираем из given_name/family_name
    const fullName = claims.name;

    return {
      id: String(sub || ""),
      role,
      fullName: String(fullName || ""),
    };
  }, [claims]);

  if (!state.isAuthenticated) {
    // Обычно эта карточка рендерится внутри защищённой страницы,
    // поэтому просто показываем предупреждение без редиректов.
    return (
      <Paper sx={{ p: 2 }}>
        <Typography color="warning.main">Вы не авторизованы</Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Typography variant="h6" gutterBottom>Персональные данные</Typography>
      <Stack spacing={1.25}>
        <Row label="ID">
          <Typography sx={{ wordBreak: "break-all" }}>{person.id || "—"}</Typography>
          {!!person.id && (
            <Button
              size="small"
              variant="text"
              onClick={() => navigator.clipboard.writeText(person.id)}
            >
              Копировать
            </Button>
          )}
        </Row>

        <Row label="Роль">
          <Typography>{person.role || "—"}</Typography>
        </Row>

        <Row label="ФИО">
          <Typography>{person.fullName || "—"}</Typography>
        </Row>
      </Stack>
    </Paper>
  );
};

const Row: React.FC<React.PropsWithChildren<{ label: string }>> = ({ label, children }) => (
  <Stack direction="row" spacing={1} alignItems="center">
    <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
      {label}:
    </Typography>
    <Stack direction="row" spacing={1} alignItems="center">{children}</Stack>
  </Stack>
);
