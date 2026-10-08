import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner, faArrowUpRightFromSquare } from "@fortawesome/free-solid-svg-icons";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { PLATFORM_ICONS, getPlatformLabel } from "../../config/platformBadges";

export const ALL_PLATFORMS = [
  { id: "Bluesky", label: "Bluesky", description: "Multi-post thread (300 chars/post)" },
  { id: "Twitter", label: "Twitter", description: "Multi-post thread (280 chars/post)" },
  { id: "LinkedIn", label: "LinkedIn", description: "Single long-form post (~3000 chars)" },
  { id: "Facebook", label: "Facebook", description: "Conversational, no hard limit" },
  { id: "Instagram", label: "Instagram", description: "Visual-first, 2200 char caption" },
  { id: "MetaThreads", label: "Threads", description: "Reply-chained thread (500 chars/post)" },
];

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (title?: string, targetPlatform?: string, targetAccountId?: string) => Promise<void>;
}

export function CreateDraftModal({ isOpen, onClose, onCreate }: Props) {
  const [title, setTitle] = useState("");
  const [selectedAccountId, setSelectedAccountId] = useState<string>("");
  const [creating, setCreating] = useState(false);
  const [loadingAccounts, setLoadingAccounts] = useState(true);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);

  useEffect(() => {
    if (!isOpen) return;

    let active = true;
    setLoadingAccounts(true);

    listAccounts()
      .then((data) => {
        if (!active) return;
        setAccounts(data);
        if (data.length > 0) {
          const bskyAcc = data.find((a) => a.platform.toLowerCase() === "bluesky");
          setSelectedAccountId(bskyAcc ? bskyAcc.id : data[0].id);
        } else {
          setSelectedAccountId("");
        }
      })
      .catch(() => {
        if (active) setAccounts([]);
      })
      .finally(() => {
        if (active) setLoadingAccounts(false);
      });

    return () => {
      active = false;
    };
  }, [isOpen]);

  if (!isOpen) return null;

  const selectedAccount = accounts.find((a) => a.id === selectedAccountId);

  const handleCreate = async () => {
    if (!selectedAccount) return;
    setCreating(true);
    try {
      await onCreate(title || undefined, selectedAccount.platform, selectedAccount.id);
      setTitle("");
      onClose();
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-bg/80 backdrop-blur-sm p-4 animate-fade-in font-sans" data-testid="create-draft-modal-overlay">
      <div className="bg-panel border border-border rounded-xl shadow-2xl w-96 max-w-full mx-3" data-testid="create-draft-modal">
        <div className="px-4 py-3 border-b border-border">
          <h2 className="text-sm font-semibold text-foreground">Create New Draft</h2>
        </div>
        <div className="px-4 py-3 space-y-3">
          <div>
            <label className="block text-xs font-mono text-muted mb-1">Title</label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") handleCreate(); }}
              placeholder="Untitled"
              className="w-full bg-bg border border-border rounded px-2 py-1.5 text-sm text-foreground placeholder:text-muted/50 focus:outline-none focus:border-accent"
              autoFocus
            />
          </div>
          <div>
            <label className="block text-xs font-mono text-muted mb-1.5">Target Account</label>
            {loadingAccounts ? (
              <div className="py-6 flex flex-col items-center justify-center text-muted gap-2 text-xs font-mono">
                <FontAwesomeIcon icon={faSpinner} className="w-4 h-4 animate-spin text-accent" />
                <span>Loading connected accounts...</span>
              </div>
            ) : accounts.length === 0 ? (
              <div className="p-3 bg-bg/40 border border-border rounded-lg text-center space-y-2.5">
                <p className="text-xs text-muted">
                  No social accounts configured. Connect an account in Settings to start drafting.
                </p>
                <Link
                  to="/settings/connections"
                  onClick={onClose}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-accent/15 text-accent border border-accent/30 hover:bg-accent/25 rounded text-xs font-mono transition-colors"
                >
                  <span>Configure Connections</span>
                  <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="w-3 h-3" />
                </Link>
              </div>
            ) : (
              <div className="space-y-1.5 max-h-60 overflow-y-auto">
                {accounts.map((acc) => {
                  const icon = PLATFORM_ICONS[acc.platform];
                  const isSelected = selectedAccountId === acc.id;
                  return (
                    <button
                      key={acc.id}
                      type="button"
                      onClick={() => setSelectedAccountId(acc.id)}
                      className={`w-full text-left px-3 py-2 rounded-lg border text-sm transition-colors flex items-center justify-between ${
                        isSelected
                          ? "border-accent bg-accent/10 text-foreground"
                          : "border-border text-muted hover:border-accent/50 hover:text-foreground bg-bg/20"
                      }`}
                    >
                      <div className="flex items-center gap-2.5 min-w-0">
                        {icon && <FontAwesomeIcon icon={icon} className="w-4 h-4 text-accent shrink-0" />}
                        <div className="min-w-0 truncate">
                          <span className="font-medium text-foreground mr-1.5">{getPlatformLabel(acc.platform)}</span>
                          <span className="text-xs text-muted font-mono truncate">@{acc.handle.replace(/^@/, '')}</span>
                        </div>
                      </div>
                      <div className={`w-3.5 h-3.5 rounded-full border flex items-center justify-center shrink-0 ${isSelected ? "border-accent bg-accent" : "border-muted/50"}`}>
                        {isSelected && <div className="w-1.5 h-1.5 rounded-full bg-bg" />}
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </div>
        </div>
        <div className="px-4 py-3 border-t border-border flex items-center justify-end gap-2">
          <button
            onClick={onClose}
            className="text-xs font-mono text-muted hover:text-foreground px-3 py-1.5 rounded border border-border hover:border-accent/50 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleCreate}
            disabled={creating || loadingAccounts || accounts.length === 0 || !selectedAccountId}
            className="text-xs font-mono font-medium text-bg bg-accent hover:opacity-90 px-3 py-1.5 rounded transition-opacity disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {creating ? "Creating..." : "Create Draft"}
          </button>
        </div>
      </div>
    </div>
  );
}