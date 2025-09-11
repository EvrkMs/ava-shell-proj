import React from "react";
import { useAuth } from "../auth/AuthContext";
import { Box, Paper, Typography, Stack, CircularProgress, Alert } from "@mui/material";
import { rolesFromClaims, decodeJwtClaims } from "../utils/jwt";

const Users = React.lazy(() => import("./HomeComponents/Users"));

const Home: React.FC = () => {
  const { state } = useAuth();
  const isRoot = React.useMemo(() => {
    const claims = (state.profile as any) ?? decodeJwtClaims(state.idToken);
    const roles = rolesFromClaims(claims);
    return roles.map(r => r.toLowerCase()).includes("root");
  }, [state.profile, state.idToken]);

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box>
          <Typography variant="h4" gutterBottom>Главная</Typography>
        </Box>
      </Stack>

      <Paper sx={{ width: "100%", p: 2 }} variant="outlined">
        {isRoot ? (
          <React.Suspense fallback={<Stack alignItems="center" sx={{ py: 3 }}><CircularProgress /></Stack>}>
            <Users />
          </React.Suspense>
        ) : (
          <Alert severity="info">У вас нет прав для просмотра раздела.</Alert>
        )}
      </Paper>
    </Box>
  );
};

export default Home;
