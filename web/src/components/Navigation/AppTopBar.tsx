import React from "react";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import Box from "@mui/material/Box";
import MenuIcon from "@mui/icons-material/Menu";
import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";
import { Breadcrumbs } from "./Breadcrumbs";
import { useColorMode } from "../../theme/ColorModeContext";

export interface AppTopBarProps {
  onOpenMobileMenu: () => void;
}

export const AppTopBar: React.FC<AppTopBarProps> = ({ onOpenMobileMenu }) => {
  const { mode, toggleColorMode } = useColorMode();

  return (
    <AppBar
      position="static"
      elevation={0}
      sx={{
        borderBottom: (theme) => `1px solid ${theme.palette.divider}`,
        bgcolor: "background.paper",
        color: "text.primary",
        zIndex: (theme) => theme.zIndex.drawer - 1,
      }}
    >
      <Toolbar
        variant="dense"
        disableGutters
        sx={{
          px: 2,
          minHeight: 48,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        {/* Left: Mobile menu toggle or desktop breadcrumbs */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, minWidth: 0 }}>
          <IconButton
            edge="start"
            color="inherit"
            aria-label="Open navigation menu"
            onClick={onOpenMobileMenu}
            sx={{ display: { xs: "inline-flex", md: "none" } }}
            size="small"
          >
            <MenuIcon fontSize="small" />
          </IconButton>

          <Box sx={{ display: { xs: "none", md: "block" } }}>
            <Breadcrumbs />
          </Box>
        </Box>

        {/* Right actions: Theme toggle on mobile / contextual actions */}
        <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
          <IconButton
            size="small"
            onClick={toggleColorMode}
            aria-label="Toggle theme mode"
            sx={{ display: { xs: "inline-flex", md: "none" }, color: "text.secondary" }}
          >
            {mode === "dark" ? <LightModeOutlinedIcon fontSize="small" /> : <DarkModeOutlinedIcon fontSize="small" />}
          </IconButton>
        </Box>
      </Toolbar>
    </AppBar>
  );
};
