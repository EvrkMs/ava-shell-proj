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
  CircularProgress,
  Alert,
} from "@mui/material";
import Users from "./HomeComponents/Users"; // путь под твой выбор
import { setAuthToken, validateToken } from "../api";

/** ===== Utils: разбор ролей из access token ===== */
function parseJwt(token: string | null | undefined): any | null {
  if (!token) return null;
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  try {
    const base64 = parts[1].replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64 + "===".slice((base64.length + 3) % 4);
    const json = atob(padded);
    return JSON.parse(json);
  } catch {
    return null;
  }
}

function getRolesFromToken(token: string | null | undefined): string[] {
  const payload = parseJwt(token);
  if (!payload) return [];
  const keys = [
    "role",
    "roles",
    // классический claim для ASP.NET Identity
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
  ];
  const roles: string[] = [];
  for (const k of keys) {
    const v = (payload as any)[k];
    if (!v) continue;
    if (Array.isArray(v)) roles.push(...v.map(String));
    else roles.push(String(v));
  }
  // нормализуем регистр, убираем дубликаты
  return Array.from(new Set(roles.map((r) => r.trim()))).filter(Boolean);
}

function hasRootRole(token: string | null | undefined): boolean {
  const roles = getRolesFromToken(token).map((r) => r.toLowerCase());
  return roles.includes("root");
}

/** ===== a11y-хелперы (со строковыми value) ===== */
function a11yProps(value: string) {
  return {
    id: `home-tab-${value}`,
    "aria-controls": `home-tabpanel-${value}`,
  };
}

/** ===== TabPanel со строковым value ===== */
const TabPanel: React.FC<{
  children?: React.ReactNode;
  current: "users" | "check";
  value: "users" | "check";
}> = ({ children, current, value }) => {
  const hidden = current !== value;
  return (
    <div
      role="tabpanel"
      hidden={hidden}
      id={`home-tabpanel-${value}`}
      aria-labelledby={`home-tab-${value}`}
    >
      {!hidden && <Box sx={{ pt: 2 }}>{children}</Box>}
    </div>
  );
};

const Home: React.FC = () => {
  const { state } = useAuth();

  // Проставляем токен в axios при изменении
  React.useEffect(() => {
    setAuthToken(state.accessToken);
  }, [state.accessToken]);

  // Определяем, есть ли роль Root
  const isRoot = React.useMemo(
    () => hasRootRole(state.accessToken),
    [state.accessToken]
  );

  // Текущее значение таба делаем строковым.
  // Если при монтировании есть роль — по умолчанию "users", иначе — "check".
  const [tab, setTab] = React.useState<"users" | "check">(
    isRoot ? "users" : "check"
  );

  // Если роль пропала, но открыт "users" — переключаем на "check".
  React.useEffect(() => {
    if (!isRoot && tab === "users") setTab("check");
  }, [isRoot, tab]);

  // Результат проверки токена
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState<string | null>(null);
  const [whoami, setWhoami] = React.useState<any>(null);

  const onCheck = async () => {
    setError(null);
    setWhoami(null);
    setLoading(true);
    try {
      if (!state.isAuthenticated || !state.accessToken) {
        setError("Нет токена. Войдите в систему.");
        return;
      }
      const data = await validateToken(state.accessToken);
      setWhoami(data);
    } catch (e: any) {
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
      <Stack
        direction="row"
        alignItems="center"
        justifyContent="space-between"
        sx={{ mb: 2 }}
      >
        <Box>
          <Typography variant="h4" gutterBottom>
            Главная
          </Typography>
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
          {isRoot && <Tab label="Пользователи" value="users" {...a11yProps("users")} />}
          <Tab
            label="Проверка сервиса валидности токена"
            value="check"
            {...a11yProps("check")}
          />
        </Tabs>

        <Box sx={{ p: 2 }}>
          {/* Вкладка Пользователи доступна только Root */}
          {isRoot && (
            <TabPanel current={tab} value="users">
              <Users />
            </TabPanel>
          )}

          <TabPanel current={tab} value="check">
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
                  <Typography variant="subtitle1" gutterBottom>
                    Результат
                  </Typography>
                  <pre
                    style={{ margin: 0, whiteSpace: "pre-wrap", wordBreak: "break-word" }}
                  >
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
