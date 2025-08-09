import React, { createContext, useEffect, useMemo, useState } from "react";
import { createTheme, CssBaseline, ThemeProvider, useMediaQuery } from "@mui/material";

type Mode = "light" | "dark";
type Ctx = { mode: Mode; toggle: () => void; set: (m: Mode) => void; };
export const ColorThemeContext = createContext<Ctx>({ mode: "light", toggle: () => {}, set: () => {} });

const STORAGE_KEY = "ui_color_mode";

export const ColorThemeProvider: React.FC<React.PropsWithChildren> = ({ children }) => {
  const systemPrefersDark = useMediaQuery("(prefers-color-scheme: dark)");
  const [mode, setMode] = useState<Mode>("light");

  // начальная инициализация
  useEffect(() => {
    const saved = (localStorage.getItem(STORAGE_KEY) as Mode | null);
    if (saved === "light" || saved === "dark") setMode(saved);
    else setMode(systemPrefersDark ? "dark" : "light");
  }, [systemPrefersDark]);

  const theme = useMemo(() => createTheme({
    palette: {
      mode,
      primary: { main: mode === "dark" ? "#90caf9" : "#1976d2" },
      secondary: { main: mode === "dark" ? "#f48fb1" : "#9c27b0" },
      background: {
        default: mode === "dark" ? "#0f1115" : "#fafafa",
        paper: mode === "dark" ? "#171a21" : "#fff",
      },
    },
    shape: { borderRadius: 10 },
    components: {
      MuiButton: { styleOverrides: { root: { textTransform: "none", fontWeight: 600 } } },
      MuiPaper: { defaultProps: { elevation: 1 } },
    },
  }), [mode]);

  const ctxValue = useMemo<Ctx>(() => ({
    mode,
    toggle: () => setMode(m => {
      const next = m === "dark" ? "light" : "dark";
      localStorage.setItem(STORAGE_KEY, next);
      return next;
    }),
    set: (m: Mode) => { localStorage.setItem(STORAGE_KEY, m); setMode(m); }
  }), [mode]);

  return (
    <ColorThemeContext.Provider value={ctxValue}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ColorThemeContext.Provider>
  );
};
