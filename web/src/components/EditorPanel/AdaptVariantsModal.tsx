import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner, faArrowUpRightFromSquare } from "@fortawesome/free-solid-svg-icons";
import { useDraftStore } from "../../store/draftStore";
import { generateVariants } from "../../api/drafts";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { PLATFORM_ICONS } from "../../config/platformBadges";

const PLATFORMS = ["Bluesky", "Twitter", "LinkedIn", "Facebook", "Instagram"];

interface Props {
  isOpen: boolean;
  onClose: () => void;
  draftId: string | null;
}

export function AdaptVariantsModal({ isOpen, onClose, draftId }: Props) {
  const [selectedAccountIds, setSelectedAccountIds] = useState<string[]>([]);
  const [generating, setGenerating] = useState(false);
  const [result, setResult] = useState<string | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loadingAccounts, setLoadingAccounts] = useState(true);

  const activeDraft = useDraftStore((s) => s.drafts.find((d) => d.id === draftId));
  const sourcePlatform = activeDraft?.targetPlatform || "Bluesky";
  const sourceAccountId = activeDraft?.targetAccountId;

  useEffect(() => {
    if (!isOpen) return;

    let active = true;
    setLoadingAccounts(true);
    setResult(null);

    listAccounts()
      .then((data) => {
        if (!active) return;
        setAccounts(data);
        const available = data.filter((a) =>
          sourceAccountId ? a.id !== sourceAccountId : a.platform.toLowerCase() !== sourcePlatform.toLowerCase()
        );
        setSelectedAccountIds(available.map((a) => a.id));
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
  }, [isOpen, sourcePlatform, sourceAccountId]);

  if (!isOpen) return null;

  const availableAccounts = accounts.filter((a) =>
    sourceAccountId ? a.id !== sourceAccountId : a.platform.toLowerCase() !== sourcePlatform.toLowerCase()
  );

  const toggle = (id: string) => {
    setSelectedAccountIds((s) =>
      s.includes(id) ? s.filter((x) => x !== id) : [...s, id]
    );
  };

  const handleGenerate = async () => {
    if (!draftId || selectedAccountIds.length === 0) return;
    setGenerating(true);
    setResult(null);
    try {
      const data = await generateVariants(draftId, undefined, selectedAccountIds);
      const variants = data.variants || [];
      if (variants.length > 0) {
        setResult(`Created ${variants.length} variant(s): ${variants.map((v: any) => `${v.targetPlatform || v.platform}${v.targetAccountHandle ? ` (@${v.targetAccountHandle})` : ""}`).join(", ")}`);
        useDraftStore.getState().loadDrafts();
      } else {
        setResult("No variants were created (they may already exist).");
      }
    } catch (err: any) {
      setResult(`Error: ${err.message}`);
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-bg/80 backdrop-blur-sm p-4 animate-fade-in font-sans" data-testid="adapt-variants-modal-overlay">
      <div className="bg-panel border border-border rounded-xl shadow-2xl w-96 max-w-full mx-3" data-testid="adapt-variants-modal">
        <div className="px-4 py-3 border-b border-border">
          <h2 className="text-sm font-semibold text-foreground">Adapt to Other Accounts</h2>
          <p className="text-xs text-muted mt-0.5">
            From {sourcePlatform}{activeDraft?.targetAccountHandle ? ` (@${activeDraft.targetAccountHandle})` : ""} to:
          </p>
        </div>
        <div className="px-4 py-3 space-y-2">
          {loadingAccounts ? (
            <div className="py-6 flex flex-col items-center justify-center text-muted gap-2 text-xs font-mono">
              <FontAwesomeIcon icon={faSpinner} className="w-4 h-4 animate-spin text-accent" />
              <span>Loading configured accounts...</span>
            </div>
          ) : availableAccounts.length === 0 ? (
            <div className="p-3 bg-bg/40 border border-border rounded-lg text-center space-y-2.5">
              <p className="text-xs text-muted">
                No other social accounts configured. Connect additional accounts in Settings to adapt this thread.
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
              {availableAccounts.map((acc) => {
                const icon = PLATFORM_ICONS[acc.platform];
                return (
                  <label
                    key={acc.id}
                    className="flex items-center justify-between px-3 py-2 rounded-lg border border-border cursor-pointer hover:bg-border/30 transition-colors bg-bg/20"
                  >
                    <div className="flex items-center gap-2.5 min-w-0">
                      {icon && <FontAwesomeIcon icon={icon} className="w-4 h-4 text-accent shrink-0" />}
                      <div className="min-w-0 truncate">
                        <span className="text-sm font-medium text-foreground mr-1.5">{acc.platform}</span>
                        <span className="text-xs text-muted font-mono truncate">@{acc.handle.replace(/^@/, '')}</span>
                      </div>
                    </div>
                    <input
                      type="checkbox"
                      checked={selectedAccountIds.includes(acc.id)}
                      onChange={() => toggle(acc.id)}
                      className="accent-accent w-4 h-4 rounded"
                    />
                  </label>
                );
              })}
            </div>
          )}
          {result && (
            <div className="mt-2 text-xs text-muted bg-border/30 rounded px-2.5 py-2">
              {result}
            </div>
          )}
        </div>
        <div className="px-4 py-3 border-t border-border flex items-center justify-end gap-2">
          <button
            onClick={onClose}
            className="text-xs font-mono text-muted hover:text-foreground px-3 py-1.5 rounded border border-border hover:border-accent/50 transition-colors"
          >
            Close
          </button>
          <button
            onClick={handleGenerate}
            disabled={generating || loadingAccounts || availableAccounts.length === 0 || selectedAccountIds.length === 0}
            className="text-xs font-mono font-medium text-bg bg-accent hover:opacity-90 px-3 py-1.5 rounded transition-opacity disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {generating ? "Generating..." : "Generate Variants"}
          </button>
        </div>
      </div>
    </div>
  );
}