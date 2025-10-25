import React from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Box, CircularProgress } from "@mui/material";
import { normalizeReturnPath } from "../utils/navigation";

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
    const returnPath = normalizeReturnPath(location.pathname + location.search + location.hash);
    return <Navigate to="/login" state={{ from: returnPath }} replace />;
  }

  return <>{children}</>;
};
