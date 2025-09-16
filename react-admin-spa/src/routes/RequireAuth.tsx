import React from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Box, CircularProgress } from "@mui/material";

interface RequireAuthProps {
  children: React.ReactNode;
}

export const RequireAuth: React.FC<RequireAuthProps> = ({ children }) => {
  const { state } = useAuth();
  const location = useLocation();

  // Ждем пока определится состояние аутентификации
  if (!state.bootstrapped) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: 200 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (!state.isAuthenticated) {
    const redirect = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/login?returnUrl=${redirect}`} replace />;
  }

  return <>{children}</>;
};