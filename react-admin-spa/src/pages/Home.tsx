import React from "react";
import { useAuth } from "../auth/AuthContext";
import { Box, Paper, Typography, Stack, CircularProgress, Alert } from "@mui/material";

import { hasRootRole } from "../utils/jwt";

const Users = React.lazy(() => import("./HomeComponents/Users"));

const Home: React.FC = () => {
  const { state } = useAuth();

    const isRoot = React.useMemo(() => hasRootRole(state.accessToken), [state.accessToken]);

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box>
          <Typography variant="h4" gutterBottom>
            Панель администратора
          </Typography>
        </Box>\r\n      </Stack>

      <Paper sx={{ width: "100%", p: 2 }} variant="outlined">
        {isRoot ? (
          <React.Suspense fallback={<Stack alignItems="center" sx={{ py: 3 }}><CircularProgress /></Stack>}>
            <Users />
          </React.Suspense>
        ) : (
          <Alert severity="info">У вас нет доступа к разделу пользователей.</Alert>
        )}
      </Paper>
    </Box>
  );
};

export default Home;










