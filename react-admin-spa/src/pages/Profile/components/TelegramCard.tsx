import React, { useEffect, useState, useCallback } from "react";
import { useAuth } from "../../../auth/AuthContext";
import { api } from "../../../api";
import {
  Alert,
  Avatar,
  Box,
  Button,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from "@mui/material";

type TelegramInfo = {
  telegramId: number;
  firstName: string;
  lastName: string;
  username: string;
  photoUrl: string;
  boundAt: string;
  lastLoginDate: string;
};

type TelegramState = {
  data: TelegramInfo | null;
  isLoading: boolean;
  error: string | null;
};

export const TelegramCard: React.FC = () => {
  const { state } = useAuth();
  const [tgState, setTgState] = useState<TelegramState>({
    data: null,
    isLoading: false,
    error: null,
  });

  const fetchTelegramInfo = useCallback(async () => {
    if (!state.isAuthenticated) return;
    
    setTgState(prev => ({ ...prev, isLoading: true, error: null }));
    
    try {
      const response = await api.get<TelegramInfo>("/api/telegram/me");
      setTgState({ data: response.data, isLoading: false, error: null });
    } catch (error: any) {
      if (error?.response?.status === 404) {
        setTgState({ data: null, isLoading: false, error: null });
      } else {
        const errorMessage = error?.response?.data || error?.message || "Ошибка запроса";
        setTgState({ data: null, isLoading: false, error: errorMessage });
      }
    }
  }, [state.isAuthenticated]);

  const handleUnbind = useCallback(async () => {
    setTgState(prev => ({ ...prev, isLoading: true, error: null }));
    
    try {
      await api.post("/api/telegram/unbind");
      setTgState({ data: null, isLoading: false, error: null });
    } catch (error: any) {
      const errorMessage = error?.response?.data || error?.message || "Ошибка отвязки";
      setTgState(prev => ({ ...prev, isLoading: false, error: errorMessage }));
    }
  }, []);

  useEffect(() => {
    fetchTelegramInfo();
  }, [fetchTelegramInfo]);

  if (!state.isAuthenticated) {
    return (
      <Paper sx={{ p: 2 }}>
        <Typography color="warning.main">Вы не авторизованы</Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Typography variant="h6">Telegram</Typography>

        {tgState.error && (
          <Alert severity="error" onClose={() => setTgState(prev => ({ ...prev, error: null }))}>
            {tgState.error}
          </Alert>
        )}

        {tgState.isLoading && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
            <CircularProgress size={22} />
            <Typography>Загрузка...</Typography>
          </Box>
        )}

        {/* Привязан */}
        {tgState.data && !tgState.isLoading && (
          <Stack spacing={2}>
            <Stack direction="row" spacing={2} alignItems="center">
              <Avatar src={tgState.data.photoUrl} alt={tgState.data.username} />
              <Box>
                <Typography>
                  Привязан:{" "}
                  <strong>
                    {tgState.data.username
                      ? `@${tgState.data.username}`
                      : `${tgState.data.firstName ?? ""} ${tgState.data.lastName ?? ""}`.trim()}
                  </strong>
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  ID: {tgState.data.telegramId}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  Привязан: {new Date(tgState.data.boundAt).toLocaleDateString()}
                </Typography>
              </Box>
            </Stack>

            <Divider />

            <Stack direction="row" spacing={2}>
              <Button
                variant="outlined"
                onClick={handleUnbind}
                disabled={tgState.isLoading}
                color="error"
              >
                Отвязать Telegram
              </Button>

              <Button
                variant="outlined"
                onClick={fetchTelegramInfo}
                disabled={tgState.isLoading}
              >
                Обновить
              </Button>
            </Stack>
          </Stack>
        )}

        {/* Не привязан */}
        {!tgState.data && !tgState.isLoading && !tgState.error && (
          <Stack spacing={2}>
            <Typography>Telegram не привязан. Привяжите аккаунт:</Typography>
            <Box>
              <Button
                variant="outlined"
                href={`https://auth.ava-kk.ru/Account/Telegram/TelegramBind?returnUrl=${encodeURIComponent(window.location.origin + "/profile")}`}
              >
                Привязать Telegram
              </Button>
            </Box>
          </Stack>
        )}
      </Stack>
    </Paper>
  );
};