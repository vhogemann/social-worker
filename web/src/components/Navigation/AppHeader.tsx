import React from "react";
import { Link, useLocation } from "react-router-dom";
import { Breadcrumbs } from "./Breadcrumbs";

export interface NavItem {
  label: string;
  to: string;
  match: string[];
}

export const defaultNavItems: NavItem[] = [
  { label: "Workflows", to: "/", match: ["/", "/workflows"] },
  { label: "Reply Agents", to: "/reply-agents", match: ["/reply-agents"] },
  { label: "Outbox", to: "/outbox", match: ["/outbox"] },
  { label: "Calendar", to: "/calendar", match: ["/calendar"] },
  { label: "Drafts", to: "/drafts", match: ["/drafts", "/draft", "/composer"] },
  { label: "Templates", to: "/templates", match: ["/templates"] },
  { label: "Settings", to: "/settings", match: ["/settings"] }
];

export interface AppHeaderProps {
  navItems?: NavItem[];
}

export const AppHeader: React.FC<AppHeaderProps> = ({ navItems = defaultNavItems }) => {
  const location = useLocation();

  return (
    <header className="shrink-0 px-4 py-2 border-b border-border text-xs font-mono text-muted flex items-center justify-between">
      <div className="flex items-center gap-2 min-w-0">
        <Link
          to="/"
          className="text-foreground font-semibold hover:text-accent transition shrink-0"
        >
          social-worker
        </Link>
        <Breadcrumbs />
      </div>
      <div className="flex gap-4 items-center shrink-0">
        {navItems.map((item) => {
          const isActive = item.match.some((prefix) =>
            prefix === "/" ? location.pathname === "/" : location.pathname.startsWith(prefix)
          );
          return (
            <Link
              key={item.to}
              to={item.to}
              className={`transition ${
                isActive ? "text-accent font-semibold" : "hover:text-foreground"
              }`}
            >
              {item.label}
            </Link>
          );
        })}
      </div>
    </header>
  );
};
