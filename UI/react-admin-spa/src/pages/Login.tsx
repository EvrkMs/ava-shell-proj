import React, { useMemo } from "react";
import { useSearchParams, Navigate, useLocation } from "react-router-dom";
import { Box, Button, Stack, Typography, Alert } from "@mui/material";
import { useAuth } from "../auth/AuthContext";
import { normalizeReturnPath, persistReturnPath } from "../utils/navigation";

const Login: React.FC = () => {
  const { signinPkce, state, clearError } = useAuth();
  const [sp] = useSearchParams();
  const location = useLocation();

  const returnUrl = useMemo(() => {
    const queryParam = sp.get("returnUrl");
    const fromState = (location.state as { from?: string } | null)?.from;
    return normalizeReturnPath(fromState ?? queryParam ?? "/");
  }, [sp, location.state]);
  
  const handleLogin = async () => {
    persistReturnPath(returnUrl);
    await signinPkce();
  };

  // Если уже авторизован — уходим на главную (или returnUrl)
  if (state.isAuthenticated) {
    return <Navigate to={returnUrl} replace />;
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>Вход</Typography>
      
      {state.error && (
        <Alert severity="error" onClose={clearError} sx={{ mb: 2 }}>
          {state.error}
        </Alert>
      )}
      
      
      <Stack spacing={2}>
        <Typography color="text.secondary">
          Для доступа к панели войдите через OIDC (PKCE).
        </Typography>
        <Button 
          variant="contained" 
          onClick={handleLogin}
          disabled={state.isLoading}
        >
          {state.isLoading ? "Вход..." : "Войти"}
        </Button>
      </Stack>
    </Box>
  );
};

export default Login;
