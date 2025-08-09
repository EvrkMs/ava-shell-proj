import React, { useMemo } from "react";
import { useSearchParams, Navigate } from "react-router-dom";
import { Box, Button, Stack, Typography } from "@mui/material";
import { useAuth } from "../auth/AuthContext";

// Храним returnUrl в sessionStorage, чтобы пережить редирект на IdP
const KEY = "post_login_return_url";

const Login: React.FC = () => {
  const { signinPkce, state } = useAuth();
  const [sp] = useSearchParams();

  const returnUrl = useMemo(() => {
    const v = sp.get("returnUrl");
    try { return v ? decodeURIComponent(v) : "/"; } catch { return v || "/"; }
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
      <Stack spacing={2}>
        <Typography color="text.secondary">
          Для доступа к панели войдите через OIDC (PKCE).
        </Typography>
        <Button variant="contained" onClick={handleLogin}>
          Войти
        </Button>
      </Stack>
    </Box>
  );
};

export default Login;
