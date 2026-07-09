import { useState } from "react";
import { useAuthStore } from "../../store/authStore";
import { AccountTab } from "./AccountTab";
import { UsersTab } from "./UsersTab";
import { ProvidersTab } from "./ProvidersTab";
import { ConnectionsTab } from "./ConnectionsTab";

interface SettingsModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function SettingsModal({ isOpen, onClose }: SettingsModalProps) {
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const [activeTab, setActiveTab] = useState<"account" | "connections" | "users" | "providers">("account");

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-bg/80 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-fade-in font-sans">
      <div className="bg-panel border border-border w-full max-w-4xl h-[600px] rounded-lg shadow-2xl overflow-hidden flex">
        <div className="w-56 bg-bg/40 border-r border-border p-4 flex flex-col justify-between select-none">
          <div className="space-y-4">
            <div className="px-2 text-xs font-mono uppercase tracking-wider text-muted mb-6">
              settings
            </div>
            <div className="space-y-1">
              <button
                onClick={() => setActiveTab("account")}
                className={`w-full text-left px-3 py-1.5 rounded text-sm font-mono uppercase tracking-wider transition-colors ${
                  activeTab === "account"
                    ? "bg-border/60 text-accent font-medium"
                    : "text-muted hover:text-foreground hover:bg-border/20"
                }`}
              >
                account
              </button>
              <button
                onClick={() => setActiveTab("connections")}
                className={`w-full text-left px-3 py-1.5 rounded text-sm font-mono uppercase tracking-wider transition-colors ${
                  activeTab === "connections"
                    ? "bg-border/60 text-accent font-medium"
                    : "text-muted hover:text-foreground hover:bg-border/20"
                }`}
              >
                connections
              </button>
              {user?.role === "Admin" && (
                <button
                  onClick={() => setActiveTab("users")}
                  className={`w-full text-left px-3 py-1.5 rounded text-sm font-mono uppercase tracking-wider transition-colors ${
                    activeTab === "users"
                      ? "bg-border/60 text-accent font-medium"
                      : "text-muted hover:text-foreground hover:bg-border/20"
                  }`}
                >
                  users
                </button>
              )}
              {user?.role === "Admin" && (
                <button
                  onClick={() => setActiveTab("providers")}
                  className={`w-full text-left px-3 py-1.5 rounded text-sm font-mono uppercase tracking-wider transition-colors ${
                    activeTab === "providers"
                      ? "bg-border/60 text-accent font-medium"
                      : "text-muted hover:text-foreground hover:bg-border/20"
                  }`}
                >
                  providers
                </button>
              )}
            </div>
          </div>

          <div className="space-y-3">
            <div className="px-3 py-2 border border-border rounded bg-bg/20 text-xs font-mono">
              <div className="text-muted mb-0.5">SIGNED IN AS</div>
              <div className="truncate text-foreground font-sans font-medium">
                {user?.username}
              </div>
            </div>
            <button
              onClick={async () => {
                onClose();
                await logout();
              }}
              className="w-full text-left px-3 py-2 text-xs font-mono text-red-400 hover:text-red-300 hover:bg-red-950/20 border border-red-900/30 rounded transition-colors uppercase tracking-wider"
            >
              sign out
            </button>
          </div>
        </div>

        <div className="flex-1 flex flex-col h-full overflow-hidden">
          <div className="px-6 py-4 border-b border-border flex items-center justify-between">
            <h2 className="text-sm font-mono uppercase tracking-wider text-foreground">
              {activeTab === "account" ? "account settings" : activeTab === "connections" ? "social connections" : activeTab === "users" ? "user accounts management" : "llm provider configuration"}
            </h2>
            <button
              onClick={onClose}
              className="text-xs font-mono text-muted hover:text-foreground uppercase tracking-widest"
            >
              close
            </button>
          </div>
          <div className="flex-1 p-6 overflow-y-auto min-h-0">
            {activeTab === "account" && <AccountTab />}
            {activeTab === "connections" && <ConnectionsTab />}
            {activeTab === "users" && user?.role === "Admin" && <UsersTab />}
            {activeTab === "providers" && user?.role === "Admin" && <ProvidersTab />}
          </div>
        </div>
      </div>
    </div>
  );
}
