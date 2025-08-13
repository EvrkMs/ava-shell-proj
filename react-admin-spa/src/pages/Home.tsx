import React from "react";
import { useAuth } from "../auth/AuthContext";
import { Link } from "react-router-dom";
import {
  Box,
  Button,
  Paper,
  Tab,
  Tabs,
  Typography,
  Stack,
  Divider,
  CircularProgress,
  Alert,
} from "@mui/material";

import { setAuthToken, validateToken } from "../api";

function a11yProps(index: number) {
  return {
    id: `home-tab-${index}`,
    "aria-controls": `home-tabpanel-${index}`,
  };
}

const TabPanel: React.FC<{
  children?: React.ReactNode;
  index: number;
  value: number;
}> = ({ children, value, index }) => {
  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`home-tabpanel-${index}`}
      aria-labelledby={`home-tab-${index}`}
    >
      {value === index && <Box sx={{ pt: 2 }}>{children}</Box>}
    </div>
  );
};

const UsersStub: React.FC = () => {
  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        <Typography variant="h6">Список пользователей</Typography>
        <Typography color="text.secondary">
          Здесь будет таблица пользователей (поиск, фильтры, роли, пагинация).
        </Typography>
        <Divider />
        <Typography variant="body2" color="text.secondary">
          Статус: в разработке. Заглушка интерфейса.
        </Typography>
      </Stack>
    </Paper>
  );
};

const Home: React.FC = () => {
  const { state } = useAuth();
  const [tab, setTab] = React.useState(0);

  // Результат проверки токена
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [whoami, setWhoami] = React.useState<any>(null);

  // При смене токена в контексте — проставим его в axios
  React.useEffect(() => {
    setAuthToken(state.accessToken); // если у тебя другое имя поля — подставь
  }, [state.accessToken]);

  const onCheck = async () => {
    setError(null);
    setWhoami(null);
    setLoading(true);
    try {
      if (!state.isAuthenticated || !state.accessToken) {
        setError("Нет токена. Войдите в систему.");
        return;
      }
      // Вызов эндпоинта валидности; по умолчанию /whoami (см. api.ts)
      const data = await validateToken(state.accessToken);
      setWhoami(data);
    } catch (e: any) {
      // Обработаем типичные статусы
      const status = e?.response?.status;
      if (status === 401) setError("401 Unauthorized — токен невалиден или истёк.");
      else if (status === 403) setError("403 Forbidden — недостаточно прав (scope/role).");
      else setError(e?.message ?? "Ошибка проверки токена.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 2 }}>
        <Box>
          <Typography variant="h4" gutterBottom>Главная</Typography>
        </Box>
        {!state.isAuthenticated && (
          <Button variant="outlined" component={Link} to="/login">
            Войти
          </Button>
        )}
      </Stack>

      <Paper sx={{ width: "100%" }} variant="outlined">
        <Tabs
          value={tab}
          onChange={(_, v) => setTab(v)}
          aria-label="home tabs"
          variant="scrollable"
          scrollButtons="auto"
        >
          <Tab label="Пользователи" {...a11yProps(0)} />
          <Tab label="Проверка сервиса валидности токена" {...a11yProps(1)} />
        </Tabs>

        <Box sx={{ p: 2 }}>
          <TabPanel value={tab} index={0}>
            <UsersStub />
          </TabPanel>

          <TabPanel value={tab} index={1}>
            <Stack spacing={2}>
              <Typography variant="h6">Проверка валидности access-токена</Typography>

              {!state.isAuthenticated && (
                <Alert severity="info">
                  Вы не авторизованы. Нажмите «Войти» вверху, затем повторите проверку.
                </Alert>
              )}

              <Stack direction="row" spacing={1}>
                <Button
                  variant="contained"
                  onClick={onCheck}
                  disabled={loading || !state.accessToken}
                >
                  {loading ? <CircularProgress size={22} /> : "Проверить"}
                </Button>
                <Button
                  variant="outlined"
                  onClick={() => {
                    setWhoami(null);
                    setError(null);
                  }}
                >
                  Очистить
                </Button>
              </Stack>

              {error && <Alert severity="error">{error}</Alert>}

              {whoami && (
                <Paper variant="outlined" sx={{ p: 2 }}>
                  <Typography variant="subtitle1" gutterBottom>Результат</Typography>
                  <pre style={{ margin: 0, whiteSpace: "pre-wrap", wordBreak: "break-word" }}>
                    {JSON.stringify(whoami, null, 2)}
                  </pre>
                </Paper>
              )}
            </Stack>
          </TabPanel>
        </Box>
      </Paper>
    </Box>
  );
};

export default Home;
