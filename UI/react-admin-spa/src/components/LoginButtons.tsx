import React from "react";
import { useAuth } from "../auth/AuthContext";
import { Button, Paper, Stack, Typography } from "@mui/material";

export const LoginButtons: React.FC = () => {
  const { signinPkce } = useAuth();

  return (
    <Paper sx={{ p: 3 }}>
      <Stack spacing={2} sx={{ maxWidth: 420 }}>
        <Typography variant="h6">Войти</Typography>

        <Button variant="contained" onClick={signinPkce}>
          Войти (PKCE / OIDC)
        </Button>
      </Stack>
    </Paper>
  );
};
