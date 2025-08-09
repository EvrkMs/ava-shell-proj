import React from "react";
import { Routes, Route, Navigate, Link } from "react-router-dom";
import Home from "./pages/Home";
import Login from "./pages/Login";
import Callback from "./pages/Callback";
import LogoutCallback from "./pages/LogoutCallback";
import Profile from "./pages/Profile/Profile";
import { useAuth } from "./auth/AuthContext";
import { AppBar, Box, Button, Container, IconButton, Toolbar, Typography, CircularProgress } from "@mui/material";
import Brightness4Icon from "@mui/icons-material/Brightness4";
import Brightness7Icon from "@mui/icons-material/Brightness7";
import { ColorThemeContext } from "./theme/ColorThemeProvider";
import { RequireAuth } from "./routes/RequireAuth";

const AppShell: React.FC<React.PropsWithChildren> = ({ children }) => {
  const { state, signout } = useAuth();
  const { mode, toggle } = React.useContext(ColorThemeContext);

  return (
    <Box sx={{ minHeight: "100vh", display: "flex", flexDirection: "column" }}>
      <AppBar position="static" color="transparent" enableColorOnDark>
        <Toolbar>
          <Typography variant="h6" sx={{ flexGrow: 1 }}>
            Admin Panel
          </Typography>
          <Button component={Link} to="/" color="inherit">Главная</Button>
          
          {state.bootstrapped && (
            <>
              {state.isAuthenticated && (
                <Button component={Link} to="/profile" color="inherit">Профиль</Button>
              )}

              {!state.isAuthenticated ? (
                <Button component={Link} to="/login" color="inherit">Войти</Button>
              ) : (
                <Button onClick={signout} color="inherit">Выйти</Button>
              )}
            </>
          )}

          <IconButton onClick={toggle} color="inherit" sx={{ ml: 1 }}>
            {mode === "dark" ? <Brightness7Icon /> : <Brightness4Icon />}
          </IconButton>
        </Toolbar>
      </AppBar>
      <Container maxWidth="md" sx={{ py: 3, flexGrow: 1 }}>
        {children}
      </Container>
    </Box>
  );
};

const App: React.FC = () => {
  const { state } = useAuth();

  // Показываем загрузку пока не определили состояние аутентификации
  if (!state.bootstrapped) {
    return (
      <AppShell>
        <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", minHeight: 200 }}>
          <CircularProgress />
        </Box>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <Routes>
        <Route path="/" element={<Home />} />
        <Route path="/login" element={<Login />} />

        {/* OIDC callbacks */}
        <Route path="/callback" element={<Callback />} />
        <Route path="/logout-callback" element={<LogoutCallback />} />

        {/* Защищённые */}
        <Route
          path="/profile"
          element={
            <RequireAuth>
              <Profile />
            </RequireAuth>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AppShell>
  );
};

export default App;