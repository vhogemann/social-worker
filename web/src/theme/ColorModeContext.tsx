import React, { createContext, useContext, useState, useEffect, useMemo, type ReactNode } from "react";
import { ThemeProvider } from "@mui/material/styles";
import CssBaseline from "@mui/material/CssBaseline";
import { createAppTheme, type PaletteMode } from "./theme";

const STORAGE_KEY = "social-worker-theme-mode";

interface ColorModeContextType {
  mode: PaletteMode;
  toggleColorMode: () => void;
  setColorMode: (mode: PaletteMode) => void;
}

const ColorModeContext = createContext<ColorModeContextType>({
  mode: "dark",
  toggleColorMode: () => {},
  setColorMode: () => {},
});

export const useColorMode = (): ColorModeContextType => useContext(ColorModeContext);

interface ColorModeProviderProps {
  children: ReactNode;
}

export const ColorModeProvider: React.FC<ColorModeProviderProps> = ({ children }) => {
  const [mode, setMode] = useState<PaletteMode>(() => {
    if (typeof window === "undefined") return "dark";
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === "light" || saved === "dark") return saved;
    if (window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches) {
      return "light";
    }
    return "dark";
  });

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, mode);
    document.documentElement.setAttribute("data-theme", mode);
    if (mode === "dark") {
      document.documentElement.classList.add("dark");
    } else {
      document.documentElement.classList.remove("dark");
    }
  }, [mode]);

  const toggleColorMode = () => {
    setMode((prev) => (prev === "dark" ? "light" : "dark"));
  };

  const setColorMode = (newMode: PaletteMode) => {
    setMode(newMode);
  };

  const theme = useMemo(() => createAppTheme(mode), [mode]);
  const contextValue = useMemo(() => ({ mode, toggleColorMode, setColorMode }), [mode]);

  return (
    <ColorModeContext.Provider value={contextValue}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </ThemeProvider>
    </ColorModeContext.Provider>
  );
};
