import React, { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthContext";
import { Alert, Box, CircularProgress, Typography } from "@mui/material";

const STORAGE_KEY = "post_login_return_url";

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
        const savedUrl = sessionStorage.getItem(STORAGE_KEY);
        const returnUrl = sp.get("returnUrl");
        
        let redirectTo = "/profile";
        
        if (savedUrl) {
          try {
            redirectTo = decodeURIComponent(savedUrl);
          } catch {
            redirectTo = savedUrl;
          }
        } else if (returnUrl) {
          try {
            redirectTo = decodeURIComponent(returnUrl);
          } catch {
            redirectTo = returnUrl;
          }
        }
        
        sessionStorage.removeItem(STORAGE_KEY);
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