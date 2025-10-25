import React, { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthContext";
import { Alert, Box, CircularProgress, Typography } from "@mui/material";
import { consumePersistedReturnPath, normalizeReturnPath } from "../utils/navigation";

const Callback: React.FC = () => {
  const { completeSignin } = useAuth();
  const [error, setError] = useState<string>();
  const ranRef = useRef(false);

  useEffect(() => {
    if (ranRef.current) return;
    ranRef.current = true;

    (async () => {
      try {
        await completeSignin();
        
        const sp = new URLSearchParams(window.location.search);
        const persisted = consumePersistedReturnPath();
        const param = sp.get("returnUrl");
        const redirectTo = persisted ?? normalizeReturnPath(param ?? "/profile");
        window.location.replace(redirectTo);
      } catch (e: any) {
        setError(e?.message || "Ошибка завершения входа");
      }
    })();
  }, [completeSignin]);

  if (error) {
    return (
      <Box sx={{ p: 3 }}>
        <Alert severity="error">
          <Typography variant="h6">Ошибка входа</Typography>
          <Typography>{error}</Typography>
        </Alert>
      </Box>
    );
  }

  return (
    <Box sx={{ 
      display: "flex", 
      flexDirection: "column", 
      alignItems: "center", 
      justifyContent: "center",
      minHeight: 200,
      gap: 2
    }}>
      <CircularProgress />
      <Typography>Завершаем вход...</Typography>
    </Box>
  );
};

export default Callback;
