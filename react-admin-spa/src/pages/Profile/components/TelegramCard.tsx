import React, { useEffect, useState } from "react";
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

export const TelegramCard: React.FC = () => {
  const { state } = useAuth();
  const [tg, setTg] = useState<TelegramInfo | null | "loading">("loading");
  const [err, setErr] = useState<string>();

  useEffect(() => {
    if (!state.isAuthenticated) return;
    (async () => {
      setErr(undefined);
      setTg("loading");
      try {
        const r = await api.get<TelegramInfo>("/api/telegram/me");
        setTg(r.data);
      } catch (e: any) {
        if (e?.response?.status === 404) {
          setTg(null); // не привязан — это ок
        } else {
          setErr(e?.response?.data ?? e?.message ?? "Ошибка запроса");
          setTg(null);
        }
      }
    })();
  }, [state.isAuthenticated]);

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

        {err && <Alert severity="error">{String(err)}</Alert>}

        {tg === "loading" && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 2 }}>
            <CircularProgress size={22} />
            <Typography>Загрузка...</Typography>
          </Box>
        )}

        {/* Привязан */}
        {tg && tg !== "loading" && (
          <Stack spacing={2}>
            <Stack direction="row" spacing={2} alignItems="center">
              <Avatar src={tg.photoUrl} alt={tg.username} />
              <Box>
                <Typography>
                  Привязан:{" "}
                  <b>
                    {tg.username
                      ? `@${tg.username}`
                      : `${tg.firstName ?? ""} ${tg.lastName ?? ""}`.trim()}
                  </b>
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  ID: {tg.telegramId}
                </Typography>
              </Box>
            </Stack>

            <Divider />

            <Stack direction="row" spacing={2}>
              <Button
                variant="outlined"
                onClick={async () => {
                  try {
                    await api.post("/api/telegram/unbind");
                    setTg(null);
                  } catch (e: any) {
                    setErr(e?.response?.data ?? e?.message ?? "Unbind failed");
                  }
                }}
              >
                Отвязать Telegram
              </Button>

              <Button
                variant="outlined"
                onClick={async () => {
                  try {
                    const r = await api.get<TelegramInfo>("/api/telegram/me");
                    setTg(r.data);
                  } catch (e: any) {
                    setErr(e?.response?.data ?? e?.message ?? "Refresh failed");
                  }
                }}
              >
                Обновить
              </Button>
            </Stack>
          </Stack>
        )}

        {/* Не привязан */}
        {tg === null && (
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
