import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faXmark,
  faEye,
  faPenToSquare,
  faComments,
  faLink,
  faCheck,
  faLayerGroup,
  faFolderOpen,
  faBolt,
} from "@fortawesome/free-solid-svg-icons";

export interface DraftMenuDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  activeView: "preview" | "editor" | "chat";
  onSelectView: (view: "preview" | "editor" | "chat") => void;
  onOpenAdaptModal?: () => void;
  workflowId?: string | null;
  canonicalDraftId?: string | null;
  activeDraftId?: string | null;
}

export const DraftMenuDrawer: React.FC<DraftMenuDrawerProps> = ({
  isOpen,
  onClose,
  activeView,
  onSelectView,
  onOpenAdaptModal,
  workflowId,
}) => {
  const navigate = useNavigate();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Best-effort copy fallback
    }
  };

  const handleSelect = (view: "preview" | "editor" | "chat") => {
    onSelectView(view);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex bg-black/60 backdrop-blur-xs animate-fade-in font-sans"
      onClick={onClose}
      data-testid="draft-menu-drawer-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label="Draft navigation and views"
    >
      <div
        className="w-72 max-w-[85vw] h-full bg-panel border-r border-border shadow-2xl flex flex-col justify-between overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
        data-testid="draft-menu-drawer"
      >
        <div className="p-4 space-y-6">
          <div className="flex items-center justify-between border-b border-border/80 pb-3">
            <span className="text-xs font-mono uppercase tracking-wider text-muted font-semibold">
              Draft Menu
            </span>
            <button
              type="button"
              onClick={onClose}
              className="text-muted hover:text-foreground p-1 rounded-md transition"
              data-testid="draft-menu-close-btn"
              aria-label="Close menu"
            >
              <FontAwesomeIcon icon={faXmark} className="w-4 h-4" />
            </button>
          </div>

          <div className="space-y-1.5">
            <div className="text-[10px] font-mono uppercase tracking-wider text-muted px-2 py-1">
              Views
            </div>
            <button
              type="button"
              data-testid="menu-view-preview"
              onClick={() => handleSelect("preview")}
              className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition cursor-pointer ${
                activeView === "preview"
                  ? "bg-accent text-accent-foreground font-semibold shadow-sm"
                  : "text-foreground hover:bg-border/60"
              }`}
            >
              <div className="flex items-center gap-2.5">
                <FontAwesomeIcon icon={faEye} className="w-4 h-4 opacity-80" />
                <span>Thread Preview</span>
              </div>
              {activeView === "preview" && (
                <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-black/20 text-accent-foreground">
                  Active
                </span>
              )}
            </button>

            <button
              type="button"
              data-testid="menu-view-editor"
              onClick={() => handleSelect("editor")}
              className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition cursor-pointer ${
                activeView === "editor"
                  ? "bg-accent text-accent-foreground font-semibold shadow-sm"
                  : "text-foreground hover:bg-border/60"
              }`}
            >
              <div className="flex items-center gap-2.5">
                <FontAwesomeIcon icon={faPenToSquare} className="w-4 h-4 opacity-80" />
                <span>Markdown Editor</span>
              </div>
              {activeView === "editor" && (
                <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-black/20 text-accent-foreground">
                  Active
                </span>
              )}
            </button>

            <button
              type="button"
              data-testid="menu-view-chat"
              onClick={() => handleSelect("chat")}
              className={`w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm font-medium transition cursor-pointer ${
                activeView === "chat"
                  ? "bg-accent text-accent-foreground font-semibold shadow-sm"
                  : "text-foreground hover:bg-border/60"
              }`}
            >
              <div className="flex items-center gap-2.5">
                <FontAwesomeIcon icon={faComments} className="w-4 h-4 opacity-80" />
                <span>AI Agent Chat</span>
              </div>
              {activeView === "chat" && (
                <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-black/20 text-accent-foreground">
                  Active
                </span>
              )}
            </button>
          </div>

          <div className="space-y-1.5 border-t border-border/80 pt-4">
            <div className="text-[10px] font-mono uppercase tracking-wider text-muted px-2 py-1">
              Actions
            </div>

            <button
              type="button"
              data-testid="menu-copy-draft-link"
              onClick={handleCopyLink}
              className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm text-foreground hover:bg-border/60 transition cursor-pointer"
            >
              <div className="flex items-center gap-2.5">
                <FontAwesomeIcon
                  icon={copied ? faCheck : faLink}
                  className={`w-4 h-4 ${copied ? "text-emerald-400" : "opacity-80"}`}
                />
                <span>{copied ? "Link Copied!" : "Copy Draft Link"}</span>
              </div>
            </button>

            {onOpenAdaptModal && (
              <button
                type="button"
                data-testid="menu-adapt-platforms"
                onClick={() => {
                  onClose();
                  onOpenAdaptModal();
                }}
                className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-sm text-foreground hover:bg-border/60 transition cursor-pointer"
              >
                <div className="flex items-center gap-2.5">
                  <FontAwesomeIcon icon={faLayerGroup} className="w-4 h-4 opacity-80" />
                  <span>Adapt for Platforms</span>
                </div>
              </button>
            )}
          </div>

          <div className="space-y-1.5 border-t border-border/80 pt-4">
            <div className="text-[10px] font-mono uppercase tracking-wider text-muted px-2 py-1">
              Navigation
            </div>

            <button
              type="button"
              data-testid="menu-nav-drafts"
              onClick={() => {
                onClose();
                navigate("/drafts");
              }}
              className="w-full flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm text-foreground hover:bg-border/60 transition cursor-pointer"
            >
              <FontAwesomeIcon icon={faFolderOpen} className="w-4 h-4 opacity-80" />
              <span>Drafts Library</span>
            </button>

            {workflowId && (
              <button
                type="button"
                data-testid="menu-nav-workflow"
                onClick={() => {
                  onClose();
                  navigate(`/workflows/${workflowId}`);
                }}
                className="w-full flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm text-foreground hover:bg-border/60 transition cursor-pointer"
              >
                <FontAwesomeIcon icon={faBolt} className="w-4 h-4 opacity-80 text-accent" />
                <span>Back to Workflow</span>
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
