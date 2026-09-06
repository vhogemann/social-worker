import React, { useEffect, useState, useMemo } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faSpinner, faSearch, faFileLines } from "@fortawesome/free-solid-svg-icons";
import { Modal } from "../common/Modal";
import { Button } from "../common/Button";
import { Badge } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import { fetchDrafts, type DraftDto } from "../../api/drafts";
import { enqueueQueueItem } from "../../api/schedules";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";

export interface DraftPickerModalProps {
  isOpen: boolean;
  onClose: () => void;
  scheduleId: string;
  onDraftQueued: () => void;
}

export const DraftPickerModal: React.FC<DraftPickerModalProps> = ({
  isOpen,
  onClose,
  scheduleId,
  onDraftQueued
}) => {
  const [drafts, setDrafts] = useState<DraftDto[]>([]);
  const [search, setSearch] = useState("");
  const [selectedDraftId, setSelectedDraftId] = useState<string>("");
  const [selectedThreadId, setSelectedThreadId] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [queuing, setQueuing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen) return;

    setLoading(true);
    setError(null);
    setSelectedDraftId("");
    setSelectedThreadId("");
    setSearch("");

    fetchDrafts()
      .then((data) => {
        // Filter drafts that have at least one thread
        const available = data.filter((d) => d.threads && d.threads.length > 0);
        setDrafts(available);
        if (available.length > 0) {
          setSelectedDraftId(available[0].id);
          setSelectedThreadId(available[0].threads[0].id);
        }
      })
      .catch((err) => {
        setError(err?.message || "Failed to load drafts.");
      })
      .finally(() => {
        setLoading(false);
      });
  }, [isOpen]);

  const filteredDrafts = useMemo(() => {
    if (!search.trim()) return drafts;
    const term = search.toLowerCase();
    return drafts.filter(
      (d) =>
        d.title.toLowerCase().includes(term) ||
        (d.content && d.content.toLowerCase().includes(term))
    );
  }, [drafts, search]);

  const handleSelectDraft = (draft: DraftDto) => {
    setSelectedDraftId(draft.id);
    if (draft.threads && draft.threads.length > 0) {
      setSelectedThreadId(draft.threads[0].id);
    } else {
      setSelectedThreadId("");
    }
  };

  const handleQueue = async () => {
    if (!selectedDraftId || !selectedThreadId) {
      setError("Please select a draft and target platform thread.");
      return;
    }

    setQueuing(true);
    setError(null);

    try {
      await enqueueQueueItem(scheduleId, {
        draftId: selectedDraftId,
        platformThreadId: selectedThreadId
      });
      onDraftQueued();
      onClose();
    } catch (err: any) {
      setError(err?.message || "Failed to enqueue draft.");
    } finally {
      setQueuing(false);
    }
  };

  const currentSelectedDraft = drafts.find((d) => d.id === selectedDraftId);

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Queue Draft to Schedule"
      description="Select an existing draft to add to the schedule's publishing queue."
      maxWidth="lg"
      testId="draft-picker-modal"
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={queuing}>
            Cancel
          </Button>
          <Button
            variant="primary"
            icon={queuing ? faSpinner : faPlus}
            disabled={queuing || !selectedDraftId || !selectedThreadId}
            onClick={handleQueue}
            data-testid="confirm-queue-draft-btn"
          >
            Add to Queue
          </Button>
        </>
      }
    >
      <div className="space-y-4 font-sans">
        {error && <AlertBanner variant="error" message={error} />}

        {/* Search Bar */}
        <div className="relative">
          <input
            type="text"
            data-testid="draft-search-input"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search drafts by title or content..."
            className="w-full rounded-lg border border-border bg-bg pl-9 pr-3 py-2 text-xs font-mono text-foreground outline-none focus:border-accent"
          />
          <FontAwesomeIcon
            icon={faSearch}
            className="absolute left-3 top-1/2 -translate-y-1/2 text-muted text-xs"
          />
        </div>

        {/* Draft List */}
        {loading ? (
          <div className="p-8 text-center text-xs font-mono text-muted flex items-center justify-center gap-2">
            <FontAwesomeIcon icon={faSpinner} className="animate-spin text-accent" />
            <span>Loading drafts...</span>
          </div>
        ) : filteredDrafts.length === 0 ? (
          <div className="p-8 rounded-lg border border-dashed border-border text-center">
            <FontAwesomeIcon icon={faFileLines} className="text-muted text-xl mb-2" />
            <p className="text-xs font-mono text-muted">No drafts available to queue.</p>
          </div>
        ) : (
          <div className="max-h-60 overflow-y-auto space-y-2 pr-1">
            {filteredDrafts.map((draft) => {
              const isSelected = draft.id === selectedDraftId;

              return (
                <div
                  key={draft.id}
                  data-testid={`draft-option-${draft.id}`}
                  onClick={() => handleSelectDraft(draft)}
                  className={`p-3 rounded-lg border cursor-pointer transition flex items-center justify-between gap-3 ${
                    isSelected
                      ? "border-accent bg-accent/5 ring-1 ring-accent"
                      : "border-border bg-card hover:bg-zinc-800/30"
                  }`}
                >
                  <div className="min-w-0">
                    <h4 className="text-xs font-semibold text-foreground truncate">
                      {draft.title || "Untitled Draft"}
                    </h4>
                    {draft.content && (
                      <p className="text-[11px] text-muted truncate mt-0.5">
                        {draft.content}
                      </p>
                    )}
                  </div>

                  <div className="flex items-center gap-1 shrink-0">
                    {draft.threads?.map((th) => {
                      const platformKey = th.platform || "Bluesky";
                      const badgeClass =
                        PLATFORM_BADGE_COLORS[platformKey] ||
                        "bg-zinc-800 text-muted border-border";
                      const iconDef = PLATFORM_ICONS[platformKey];

                      return (
                        <span
                          key={th.id}
                          className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono border ${badgeClass}`}
                        >
                          {iconDef && (
                            <FontAwesomeIcon icon={iconDef} className="text-[8px]" />
                          )}
                          {platformKey}
                        </span>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Platform Thread Selector (if chosen draft has multiple platform threads) */}
        {currentSelectedDraft && currentSelectedDraft.threads.length > 1 && (
          <div className="pt-2 border-t border-border">
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
              Select Target Platform Variant
            </label>
            <div className="flex gap-2 flex-wrap">
              {currentSelectedDraft.threads.map((th) => {
                const isSelected = th.id === selectedThreadId;
                const platformKey = th.platform || "Bluesky";
                const iconDef = PLATFORM_ICONS[platformKey];

                return (
                  <button
                    key={th.id}
                    type="button"
                    data-testid={`thread-option-${th.id}`}
                    onClick={() => setSelectedThreadId(th.id)}
                    className={`px-3 py-1.5 rounded-lg border text-xs font-mono transition flex items-center gap-1.5 ${
                      isSelected
                        ? "border-accent bg-accent text-white font-semibold"
                        : "border-border bg-card text-muted hover:text-foreground"
                    }`}
                  >
                    {iconDef && <FontAwesomeIcon icon={iconDef} className="text-[10px]" />}
                    <span>{platformKey}</span>
                  </button>
                );
              })}
            </div>
          </div>
        )}
      </div>
    </Modal>
  );
};
