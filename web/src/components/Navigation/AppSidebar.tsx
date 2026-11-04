import React from "react";
import { Link, useLocation } from "react-router-dom";
import Box from "@mui/material/Box";
import Drawer from "@mui/material/Drawer";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import Tooltip from "@mui/material/Tooltip";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import Divider from "@mui/material/Divider";
import ChevronLeftIcon from "@mui/icons-material/ChevronLeft";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import LightModeOutlinedIcon from "@mui/icons-material/LightModeOutlined";
import DarkModeOutlinedIcon from "@mui/icons-material/DarkModeOutlined";
import AutoAwesomeOutlinedIcon from "@mui/icons-material/AutoAwesomeOutlined";
import { NAV_ITEMS, type NavItemConfig } from "./navItems";
import { useColorMode } from "../../theme/ColorModeContext";

export const SIDEBAR_WIDTH_EXPANDED = 240;
export const SIDEBAR_WIDTH_COLLAPSED = 68;

export interface AppSidebarProps {
  isCollapsed: boolean;
  onToggleCollapse: () => void;
  mobileOpen: boolean;
  onCloseMobile: () => void;
  items?: NavItemConfig[];
}

export const AppSidebar: React.FC<AppSidebarProps> = ({
  isCollapsed,
  onToggleCollapse,
  mobileOpen,
  onCloseMobile,
  items = NAV_ITEMS,
}) => {
  const location = useLocation();
  const { mode, toggleColorMode } = useColorMode();
  const currentWidth = isCollapsed ? SIDEBAR_WIDTH_COLLAPSED : SIDEBAR_WIDTH_EXPANDED;

  const renderNavList = (isMobile: boolean) => (
    <List sx={{ px: 1, py: 1, flexGrow: 1 }}>
      {items.map((item) => {
        const IconComponent = item.icon;
        const isActive = item.match.some((prefix) =>
          prefix === "/" ? location.pathname === "/" : location.pathname.startsWith(prefix)
        );

        const buttonContent = (
          <ListItemButton
            component={Link}
            to={item.to}
            onClick={isMobile ? onCloseMobile : undefined}
            selected={isActive}
            sx={{
              minHeight: 44,
              borderRadius: 2,
              px: isCollapsed && !isMobile ? 1.5 : 2,
              justifyContent: isCollapsed && !isMobile ? "center" : "flex-start",
              transition: "all 0.15s ease",
              "&.Mui-selected": {
                bgcolor: "action.selected",
                color: "primary.main",
                "&:hover": {
                  bgcolor: "action.hover",
                },
              },
            }}
          >
            <ListItemIcon
              sx={{
                minWidth: 0,
                mr: isCollapsed && !isMobile ? 0 : 2,
                justifyContent: "center",
                color: isActive ? "primary.main" : "text.secondary",
              }}
            >
              <IconComponent fontSize="small" />
            </ListItemIcon>
            {(!isCollapsed || isMobile) && (
              <ListItemText
                primary={item.label}
                primaryTypographyProps={{
                  fontSize: "0.875rem",
                  fontWeight: isActive ? 600 : 500,
                  color: isActive ? "text.primary" : "text.secondary",
                }}
              />
            )}
          </ListItemButton>
        );

        return (
          <ListItem key={item.to} disablePadding sx={{ display: "block", mb: 0.5 }}>
            {isCollapsed && !isMobile ? (
              <Tooltip title={item.label} placement="right" arrow>
                {buttonContent}
              </Tooltip>
            ) : (
              buttonContent
            )}
          </ListItem>
        );
      })}
    </List>
  );

  return (
    <>
      {/* Mobile Drawer (Temporary overlay, only rendered when open) */}
      <Drawer
        variant="temporary"
        open={mobileOpen}
        onClose={onCloseMobile}
        sx={{
          display: { xs: "block", md: "none" },
          "& .MuiDrawer-paper": {
            width: SIDEBAR_WIDTH_EXPANDED,
            boxSizing: "border-box",
            display: "flex",
            flexDirection: "column",
          },
        }}
      >
        <Box sx={{ p: 2, display: "flex", alignItems: "center", gap: 1.5 }}>
          <AutoAwesomeOutlinedIcon color="primary" />
          <Typography variant="subtitle1" fontWeight={700} noWrap>
            Menu
          </Typography>
        </Box>
        <Divider />
        {renderNavList(true)}
        <Divider />
        <Box sx={{ p: 1.5, display: "flex", alignItems: "center", justifyContent: "space-between" }}>
          <Typography variant="caption" color="text.secondary">
            Mode: {mode}
          </Typography>
          <IconButton size="small" onClick={toggleColorMode} aria-label="Toggle theme mode">
            {mode === "dark" ? <LightModeOutlinedIcon fontSize="small" /> : <DarkModeOutlinedIcon fontSize="small" />}
          </IconButton>
        </Box>
      </Drawer>

      {/* Desktop Drawer (Permanent with fluid width transition) */}
      <Drawer
        variant="permanent"
        sx={{
          display: { xs: "none", md: "block" },
          width: currentWidth,
          flexShrink: 0,
          whiteSpace: "nowrap",
          boxSizing: "border-box",
          "& .MuiDrawer-paper": {
            width: currentWidth,
            boxSizing: "border-box",
            overflowX: "hidden",
            display: "flex",
            flexDirection: "column",
            transition: (theme) =>
              theme.transitions.create("width", {
                easing: theme.transitions.easing.sharp,
                duration: theme.transitions.duration.enteringScreen,
              }),
          },
        }}
      >
        {/* Brand Header */}
        <Box
          sx={{
            height: 56,
            px: isCollapsed ? 1.5 : 2,
            display: "flex",
            alignItems: "center",
            justifyContent: isCollapsed ? "center" : "flex-start",
            gap: 1.5,
          }}
        >
          <AutoAwesomeOutlinedIcon color="primary" sx={{ shrink: 0 }} />
          {!isCollapsed && (
            <Typography variant="subtitle1" fontWeight={700} noWrap component={Link} to="/" sx={{ textDecoration: "none", color: "inherit" }}>
              social-worker
            </Typography>
          )}
        </Box>
        <Divider />

        {/* Navigation list */}
        {renderNavList(false)}

        {/* Footer controls: Theme toggle + Collapse button */}
        <Divider />
        <Box
          sx={{
            p: 1,
            display: "flex",
            alignItems: "center",
            justifyContent: isCollapsed ? "center" : "space-between",
            flexDirection: isCollapsed ? "column" : "row",
            gap: 1,
          }}
        >
          <Tooltip title={mode === "dark" ? "Switch to light mode" : "Switch to dark mode"} placement="right" arrow>
            <IconButton
              size="small"
              onClick={toggleColorMode}
              aria-label="Toggle light dark theme"
              sx={{ color: "text.secondary" }}
            >
              {mode === "dark" ? <LightModeOutlinedIcon fontSize="small" /> : <DarkModeOutlinedIcon fontSize="small" />}
            </IconButton>
          </Tooltip>

          <Tooltip title={isCollapsed ? "Expand sidebar" : "Collapse sidebar"} placement="right" arrow>
            <IconButton
              size="small"
              onClick={onToggleCollapse}
              aria-label={isCollapsed ? "Expand sidebar" : "Collapse sidebar"}
              sx={{ color: "text.secondary" }}
            >
              {isCollapsed ? <ChevronRightIcon fontSize="small" /> : <ChevronLeftIcon fontSize="small" />}
            </IconButton>
          </Tooltip>
        </Box>
      </Drawer>
    </>
  );
};
