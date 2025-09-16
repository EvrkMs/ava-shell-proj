import React, { useMemo } from "react";
import { useSearchParams, Navigate } from "react-router-dom";
import { Box, Button, Stack, Typography, Alert } from "@mui/material";
import { useAuth } from "../auth/AuthContext";

const KEY = "post_login_return_url";

const Login: React.FC = () => {
  const { signinPkce, state, clearError } = useAuth();
  const [sp] = useSearchParams();

  const returnUrl = useMemo(() => {
    const v = sp.get("returnUrl");
    try { 
      return v ? decodeURIComponent(v) : "/"; 
    } catch { 
      return v || "/"; 
    }
  }, [sp]);
  
  const handleLogin = async () => {
    sessionStorage.setItem(KEY, returnUrl);
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