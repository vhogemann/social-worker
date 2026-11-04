import React from "react";
import HubOutlinedIcon from "@mui/icons-material/HubOutlined";
import SmartToyOutlinedIcon from "@mui/icons-material/SmartToyOutlined";
import OutboxOutlinedIcon from "@mui/icons-material/OutboxOutlined";
import CalendarMonthOutlinedIcon from "@mui/icons-material/CalendarMonthOutlined";
import EditNoteOutlinedIcon from "@mui/icons-material/EditNoteOutlined";
import DashboardCustomizeOutlinedIcon from "@mui/icons-material/DashboardCustomizeOutlined";
import SourceOutlinedIcon from "@mui/icons-material/SourceOutlined";
import TerminalOutlinedIcon from "@mui/icons-material/TerminalOutlined";
import SettingsOutlinedIcon from "@mui/icons-material/SettingsOutlined";

export interface NavItemConfig {
  label: string;
  to: string;
  match: string[];
  icon: React.ComponentType<{ fontSize?: "small" | "inherit" | "medium" | "large" }>;
}

export const NAV_ITEMS: NavItemConfig[] = [
  { label: "Workflows", to: "/", match: ["/", "/workflows"], icon: HubOutlinedIcon },
  { label: "Reply Agents", to: "/reply-agents", match: ["/reply-agents"], icon: SmartToyOutlinedIcon },
  { label: "Outbox", to: "/outbox", match: ["/outbox"], icon: OutboxOutlinedIcon },
  { label: "Calendar", to: "/calendar", match: ["/calendar"], icon: CalendarMonthOutlinedIcon },
  { label: "Drafts", to: "/drafts", match: ["/drafts", "/draft", "/composer"], icon: EditNoteOutlinedIcon },
  { label: "Templates", to: "/templates", match: ["/templates"], icon: DashboardCustomizeOutlinedIcon },
  { label: "Sources", to: "/sources", match: ["/sources"], icon: SourceOutlinedIcon },
  { label: "Logs", to: "/logs", match: ["/logs"], icon: TerminalOutlinedIcon },
  { label: "Settings", to: "/settings", match: ["/settings"], icon: SettingsOutlinedIcon },
];
