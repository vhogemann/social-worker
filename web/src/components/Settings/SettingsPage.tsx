import { useMemo } from "react";
import { Link, useParams, Navigate, useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faUser,
  faShareNodes,
  faBullhorn,
  faRss,
  faUsers,
  faMicrochip,
  faRightFromBracket,
} from "@fortawesome/free-solid-svg-icons";
import { useAuthStore } from "../../store/authStore";
import { AccountTab } from "./AccountTab";
import { ConnectionsTab } from "./ConnectionsTab";
import { BrandVoicesTab } from "./BrandVoicesTab";
import { FeedsTab } from "./FeedsTab";
import { UsersTab } from "./UsersTab";
import { ProvidersTab } from "./ProvidersTab";

type SettingsTab = "account" | "connections" | "brand-voices" | "feeds" | "users" | "providers";

interface TabConfig {
  id: SettingsTab;
  label: string;
  icon: typeof faUser;
  adminOnly?: boolean;
  title: string;
  description: string;
}

const TABS: TabConfig[] = [
  {
    id: "account",
    label: "Account",
    icon: faUser,
    title: "Account Settings",
    description: "Manage your personal profile, password, and default AI provider preferences.",
  },
  {
    id: "connections",
    label: "Connections",
    icon: faShareNodes,
    title: "Social Connections",
    description: "Connect and manage your social platform accounts for direct publishing.",
  },
  {
    id: "brand-voices",
    label: "Brand Voices",
    icon: faBullhorn,
    title: "Brand Voice Prompts",
    description: "Configure reusable style guidelines and tone prompts for drafting content.",
  },
  {
    id: "feeds",
    label: "Feeds",
    icon: faRss,
    title: "Feed Subscriptions",
    description: "Manage RSS, Atom, and YouTube feed subscriptions used by automation workflows.",
  },
  {
    id: "users",
    label: "Users",
    icon: faUsers,
    adminOnly: true,
    title: "User Management",
    description: "Manage registered user accounts, roles, and password resets.",
  },
  {
    id: "providers",
    label: "LLM Providers",
    icon: faMicrochip,
    adminOnly: true,
    title: "LLM Provider Configuration",
    description: "Configure custom LLM models, API endpoints, and credentials.",
  },
];

export function SettingsPage() {
  const { tab } = useParams<{ tab?: string }>();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  const isAdmin = user?.role === "Admin";
  const activeTabId = (tab || "account") as SettingsTab;

  const activeTabConfig = useMemo(() => {
    const config = TABS.find((t) => t.id === activeTabId);
    if (!config) return null;
    if (config.adminOnly && !isAdmin) return null;
    return config;
  }, [activeTabId, isAdmin]);

  if (!activeTabConfig) {
    return <Navigate to="/settings/account" replace />;
  }

  const generalTabs = TABS.filter((t) => !t.adminOnly);
  const adminTabs = TABS.filter((t) => t.adminOnly && isAdmin);

  const handleLogout = async () => {
    await logout();
    navigate("/login", { replace: true });
  };

  return (
    <div className="flex-1 min-h-0 flex overflow-hidden font-sans bg-bg" data-testid="settings-page">
      {/* Sidebar navigation */}
      <aside className="w-64 bg-panel border-r border-border p-4 flex flex-col justify-between select-none shrink-0 overflow-y-auto">
        <div className="space-y-6">
          <div className="px-3 text-xs font-mono uppercase tracking-wider text-muted font-bold">
            Settings
          </div>

          <div className="space-y-4">
            <div>
              <div className="px-3 text-[10px] font-mono uppercase tracking-wider text-muted/80 font-semibold mb-2">
                General
              </div>
              <nav className="space-y-1">
                {generalTabs.map((t) => {
                  const isActive = activeTabId === t.id;
                  return (
                    <Link
                      key={t.id}
                      to={`/settings/${t.id}`}
                      className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-xs font-mono transition-colors ${
                        isActive
                          ? "bg-accent/15 text-accent font-medium border border-accent/30"
                          : "text-muted hover:text-foreground hover:bg-border/20"
                      }`}
                    >
                      <FontAwesomeIcon icon={t.icon} className="w-3.5 h-3.5 shrink-0" />
                      <span>{t.label}</span>
                    </Link>
                  );
                })}
              </nav>
            </div>

            {adminTabs.length > 0 && (
              <div>
                <div className="px-3 text-[10px] font-mono uppercase tracking-wider text-muted/80 font-semibold mb-2">
                  Admin
                </div>
                <nav className="space-y-1">
                  {adminTabs.map((t) => {
                    const isActive = activeTabId === t.id;
                    return (
                      <Link
                        key={t.id}
                        to={`/settings/${t.id}`}
                        className={`w-full flex items-center gap-3 px-3 py-2 rounded-lg text-xs font-mono transition-colors ${
                          isActive
                            ? "bg-accent/15 text-accent font-medium border border-accent/30"
                            : "text-muted hover:text-foreground hover:bg-border/20"
                        }`}
                      >
                        <FontAwesomeIcon icon={t.icon} className="w-3.5 h-3.5 shrink-0" />
                        <span>{t.label}</span>
                      </Link>
                    );
                  })}
                </nav>
              </div>
            )}
          </div>
        </div>

        {/* User Session Footer */}
        <div className="space-y-3 pt-4 border-t border-border">
          <div className="px-3 py-2 border border-border rounded-lg bg-bg/40 text-xs font-mono">
            <div className="text-[10px] text-muted uppercase tracking-wider mb-0.5">Signed In As</div>
            <div className="truncate text-foreground font-sans font-medium">{user?.username || "Unknown"}</div>
          </div>
          <button
            onClick={handleLogout}
            className="w-full text-left px-3 py-2 text-xs font-mono text-red-400 hover:text-red-300 hover:bg-red-950/20 border border-red-900/30 rounded-lg transition-colors flex items-center justify-between"
          >
            <span>Sign Out</span>
            <FontAwesomeIcon icon={faRightFromBracket} className="w-3.5 h-3.5" />
          </button>
        </div>
      </aside>

      {/* Main Content Area */}
      <main className="flex-1 flex flex-col h-full overflow-hidden bg-bg">
        <div className="px-8 py-6 border-b border-border bg-panel/30 shrink-0">
          <h1 className="text-xl font-bold text-foreground tracking-tight">{activeTabConfig.title}</h1>
          <p className="text-xs text-muted mt-1">{activeTabConfig.description}</p>
        </div>
        <div className="flex-1 p-8 overflow-y-auto min-h-0">
          <div className="max-w-4xl mx-auto">
            {activeTabId === "account" && <AccountTab />}
            {activeTabId === "connections" && <ConnectionsTab />}
            {activeTabId === "brand-voices" && <BrandVoicesTab />}
            {activeTabId === "feeds" && <FeedsTab />}
            {activeTabId === "users" && isAdmin && <UsersTab />}
            {activeTabId === "providers" && isAdmin && <ProvidersTab />}
          </div>
        </div>
      </main>
    </div>
  );
}
