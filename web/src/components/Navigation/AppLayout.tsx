import React, { useState, useEffect, type ReactNode } from "react";
import Box from "@mui/material/Box";
import { AppSidebar } from "./AppSidebar";
import { AppTopBar } from "./AppTopBar";

const COLLAPSE_STORAGE_KEY = "social-worker-sidebar-collapsed";

export interface AppLayoutProps {
  children: ReactNode;
}

export const AppLayout: React.FC<AppLayoutProps> = ({ children }) => {
  const [isCollapsed, setIsCollapsed] = useState<boolean>(() => {
    if (typeof window === "undefined") return false;
    return localStorage.getItem(COLLAPSE_STORAGE_KEY) === "true";
  });

  const [mobileOpen, setMobileOpen] = useState(false);

  useEffect(() => {
    localStorage.setItem(COLLAPSE_STORAGE_KEY, String(isCollapsed));
  }, [isCollapsed]);

  const toggleCollapse = () => {
    setIsCollapsed((prev) => !prev);
  };

  return (
    <Box
      sx={{
        display: "flex",
        height: "100vh",
        width: "100vw",
        overflow: "hidden",
        bgcolor: "background.default",
        color: "text.primary",
      }}
    >
      <AppSidebar
        isCollapsed={isCollapsed}
        onToggleCollapse={toggleCollapse}
        mobileOpen={mobileOpen}
        onCloseMobile={() => setMobileOpen(false)}
      />

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          display: "flex",
          flexDirection: "column",
          height: "100%",
          minWidth: 0,
          overflow: "hidden",
        }}
      >
        <AppTopBar onOpenMobileMenu={() => setMobileOpen(true)} />
        <Box
          sx={{
            flexGrow: 1,
            minHeight: 0,
            overflow: "auto",
            position: "relative",
            display: "flex",
            flexDirection: "column",
          }}
        >
          {children}
        </Box>
      </Box>
    </Box>
  );
};
