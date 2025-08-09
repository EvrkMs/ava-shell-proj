import React, { useMemo } from "react";
import { useAuth } from "../../../auth/AuthContext";
import { Paper, Stack, Typography, Button, Chip } from "@mui/material";

export const PersonalCard: React.FC = () => {
  const { state } = useAuth();

  const userInfo = useMemo(() => {
    if (!state.profile) {
      return { id: "", roles: [], fullName: "", email: "" };
    }

    const claims = state.profile;
    
    // ID пользователя
    const id = claims.sub || claims.sid || "";
    
    // Роли - могут быть в разных клеймах
    const rolesClaim = claims.role || claims.roles || 
      claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || [];
    const roles = Array.isArray(rolesClaim) ? rolesClaim : [rolesClaim].filter(Boolean);
    
    // Имя
    const fullName = claims.name || 
      [claims.given_name, claims.family_name].filter(Boolean).join(" ") ||
      [claims.first_name, claims.last_name].filter(Boolean).join(" ") ||
      "";
    
    // Email
    const email = claims.email || "";

    return { id, roles, fullName, email };
  }, [state.profile]);

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
            {userInfo.roles.length > 0 ? (
              userInfo.roles.map((role, index) => (
                <Chip key={index} label={role} size="small" variant="outlined" />
              ))
            ) : (
              <Typography>—</Typography>
            )}
          </Stack>
        </Stack>
      </Stack>
    </Paper>
  );
};

const InfoRow: React.FC<{ label: string; value: string; copyable?: boolean }> = ({ 
  label, 
  value, 
  copyable 
}) => (
  <Stack direction="row" spacing={1} alignItems="center">
    <Typography variant="body2" color="text.secondary" sx={{ minWidth: 120 }}>
      {label}:
    </Typography>
    <Typography sx={{ wordBreak: copyable ? "break-all" : "normal" }}>
      {value || "—"}
    </Typography>
    {copyable && value && (
      <Button
        size="small"
        variant="text"
        onClick={() => navigator.clipboard.writeText(value)}
      >
        Копировать
      </Button>
    )}
  </Stack>
);