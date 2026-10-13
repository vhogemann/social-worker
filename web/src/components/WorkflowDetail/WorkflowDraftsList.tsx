import React, { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBolt, faFileLines, faPlus, faRotateRight } from "@fortawesome/free-solid-svg-icons";
import { type WorkflowDraftSummaryDto } from "../../api/workflows";
import { Button } from "../common/Button";
import { Input } from "../common/Input";
import { EmptyState } from "../common/EmptyState";
import { Badge } from "../common/Badge";
import { getPlatformLabel } from "../../config/platformBadges";

type FilterTab = "all" | "discarded" | "archived";

export interface WorkflowDraftsListProps {
  workflowId?: string;
  drafts: WorkflowDraftSummaryDto[];
  isTriggering?: boolean;
  onTrigger: () => void;
  isRetryingFailed?: boolean;
  retryingDraftId?: string | null;
  onRetryFailed?: () => void;
  onRetryDraft?: (draftId: string) => void;
}

export const WorkflowDraftsList: React.FC<WorkflowDraftsListProps> = ({
  workflowId,
  drafts,
  isTriggering = false,
  onTrigger,
  isRetryingFailed = false,
  retryingDraftId = null,
  onRetryFailed,
  onRetryDraft,
}) => {
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<FilterTab>("all");
  const [search, setSearch] = useState("");

  const failedDraftsCount = drafts.filter((d) => d.status === "Failed").length;
  const activeCount = useMemo(
    () => drafts.filter((d) => d.status !== "Archived" && d.status !== "Discarded").length,
    [drafts]
  );
  const discardedCount = useMemo(
    () => drafts.filter((d) => d.status === "Discarded").length,
    [drafts]
  );
  const archivedCount = useMemo(
    () => drafts.filter((d) => d.status === "Archived").length,
    [drafts]
  );

  const filteredDrafts = useMemo(() => {
    return drafts.filter((d) => {
      if (d.status === "Deleted") return false;

      // Tab filter
      if (activeTab === "discarded") {
        if (d.status !== "Discarded") return false;
      } else if (activeTab === "archived") {
        if (d.status !== "Archived") return false;
      } else {
        if (d.status === "Archived" || d.status === "Discarded") return false;
      }

      // Search filter
      if (search.trim()) {
        const q = search.toLowerCase();
        const matchesTitle = d.title.toLowerCase().includes(q);
        const matchesContent = d.content?.toLowerCase().includes(q) ?? false;
        const matchesDiscardReason = d.discardReason?.toLowerCase().includes(q) ?? false;
        const matchesPlatform = d.threads?.some((t) =>
          t.platform.toLowerCase().includes(q) || (t.accountHandle?.toLowerCase().includes(q) ?? false)
        ) ?? false;
        return matchesTitle || matchesContent || matchesDiscardReason || matchesPlatform;
      }

      return true;
    });
  }, [drafts, activeTab, search]);

  const handleOpenDraft = (draftId: string) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${draftId}`);
    } else {
      navigate(`/draft/${draftId}`);
    }
  };

  const handleOpenVariant = (e: React.MouseEvent, draftId: string, variantId?: string | null) => {
    e.stopPropagation();
    if (variantId) {
      if (workflowId) {
        navigate(`/workflows/${workflowId}/draft/${draftId}/variant/${variantId}`);
      } else {
        navigate(`/draft/${draftId}/variant/${variantId}`);
      }
    } else {
      handleOpenDraft(draftId);
    }
  };

  const handleCreateNewDraft = () => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft`);
    } else {
      navigate(`/drafts`);
    }
  };

  return (
    <div className="flex flex-col gap-4" data-testid="workflow-drafts-list">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <h2 className="text-sm font-bold text-foreground">
          Generated Drafts <span className="text-muted font-normal">({filteredDrafts.length})</span>
        </h2>
        <div className="flex items-center gap-2">
          {failedDraftsCount > 0 && onRetryFailed && (
            <Button
              variant="secondary"
              size="sm"
              icon={faRotateRight}
              loading={isRetryingFailed}
              onClick={onRetryFailed}
              data-testid="workflow-retry-failed-btn"
              className="border-amber-500/40 text-amber-300 hover:bg-amber-500/10"
            >
              Retry Failed ({failedDraftsCount})
            </Button>
          )}
          <Button
            variant="secondary"
            size="sm"
            icon={faPlus}
            onClick={handleCreateNewDraft}
          >
            New Draft
          </Button>
        </div>
      </div>

      {/* Filter and Search Bar (matching Drafts Library) */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 pb-2 border-b border-border">
        <div className="flex items-center gap-1 bg-panel border border-border rounded-lg p-1 w-full sm:w-auto text-xs font-medium">
          <button
            type="button"
            onClick={() => setActiveTab("all")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "all"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
            data-testid="filter-tab-all"
          >
            All Active <span className="opacity-60 text-[11px] font-mono ml-0.5">({activeCount})</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("discarded")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "discarded"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
            data-testid="filter-tab-discarded"
          >
            Discarded <span className="opacity-60 text-[11px] font-mono ml-0.5">({discardedCount})</span>
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("archived")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "archived"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
            data-testid="filter-tab-archived"
          >
            Archived <span className="opacity-60 text-[11px] font-mono ml-0.5">({archivedCount})</span>
          </button>
        </div>

        <div className="w-full sm:w-72">
          <Input
            placeholder="Search drafts..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            data-testid="workflow-drafts-search-input"
          />
        </div>
      </div>

      {drafts.length === 0 ? (
        <EmptyState
          icon={faFileLines}
          title="No drafts generated yet"
          description="When new feed items are ingested, generated canonical threads and platform adaptations will appear here."
          action={
            <Button
              variant="primary"
              size="sm"
              icon={faBolt}
              loading={isTriggering}
              onClick={onTrigger}
            >
              Trigger Ingestion Now
            </Button>
          }
        />
      ) : filteredDrafts.length === 0 ? (
        <EmptyState
          icon={faFileLines}
          title={
            search.trim()
              ? "No matching drafts"
              : activeTab === "discarded"
              ? "No discarded drafts"
              : activeTab === "archived"
              ? "No archived drafts"
              : "No active drafts"
          }
          description={
            search.trim()
              ? `No drafts matched "${search}". Try adjusting your search query.`
              : activeTab === "discarded"
              ? "No drafts have been discarded by the relevance filter for this workflow."
              : activeTab === "archived"
              ? "No drafts in this workflow have been archived."
              : "There are currently no active drafts for this workflow."
          }
          action={
            search.trim() ? (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setSearch("")}
              >
                Clear Search
              </Button>
            ) : activeTab !== "all" ? (
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setActiveTab("all")}
              >
                View All Active
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredDrafts.map((draft) => {
            const isFailed = draft.status === "Failed";
            const isDiscarded = draft.status === "Discarded";
            const isRetryingThis = retryingDraftId === draft.id;

            return (
              <div
                key={draft.id}
                onClick={() => handleOpenDraft(draft.id)}
                className={`bg-panel border rounded-xl p-5 flex flex-col justify-between transition hover:shadow-md cursor-pointer group ${
                  isFailed
                    ? "border-red-500/30 hover:border-red-500/60 bg-red-950/10"
                    : isDiscarded
                    ? "border-amber-500/30 hover:border-amber-500/50 bg-amber-950/10"
                    : "border-border hover:border-zinc-600"
                }`}
                data-testid="workflow-draft-card"
              >
                <div className="flex flex-col gap-3">
                  <div className="flex justify-between items-start gap-2">
                    <h3 className="text-sm font-bold text-foreground line-clamp-2 group-hover:text-accent transition">
                      {draft.title}
                    </h3>
                    {isFailed && onRetryDraft && (
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          onRetryDraft(draft.id);
                        }}
                        disabled={isRetryingThis}
                        data-testid={`workflow-draft-retry-btn-${draft.id}`}
                        className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-mono font-medium text-amber-400 bg-amber-400/10 border border-amber-400/20 hover:bg-amber-400/20 transition cursor-pointer flex-shrink-0"
                        title="Retry draft generation"
                      >
                        <FontAwesomeIcon icon={faRotateRight} spin={isRetryingThis} className="text-[10px]" />
                        <span>Retry</span>
                      </button>
                    )}
                  </div>

                  {isDiscarded && draft.discardReason && (
                    <p className="text-xs text-muted/90 line-clamp-2 italic bg-bg/60 p-2 rounded border border-border/60">
                      "{draft.discardReason}"
                    </p>
                  )}

                  {/* Platform Thread Badges & Status */}
                  <div className="flex flex-wrap items-center gap-1.5">
                    {isFailed && (
                      <Badge variant="danger" data-testid="workflow-draft-failed-badge">
                        Failed
                      </Badge>
                    )}
                    {isDiscarded && (
                      <Badge variant="warning" data-testid="workflow-draft-discarded-badge" className="border-amber-500/40 text-amber-300 bg-amber-500/10">
                        Discarded {draft.relevanceScore != null ? `(${draft.relevanceScore}/10)` : ""}
                      </Badge>
                    )}
                    {draft.threads.length === 0 ? (
                      !isFailed && !isDiscarded && <Badge variant="neutral">{draft.status}</Badge>
                    ) : (
                      draft.threads.map((thread, idx) => (
                        <button
                          type="button"
                          key={idx}
                          onClick={(e) => handleOpenVariant(e, draft.id, thread.variantId)}
                          data-testid={`workflow-draft-thread-${thread.platform.toLowerCase()}`}
                          className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono font-medium transition cursor-pointer hover:scale-105 ${
                            thread.stage === "Sent"
                              ? "bg-green-500/10 text-green-400 border border-green-500/20 hover:border-green-400"
                              : "bg-bg text-foreground border border-border hover:border-accent"
                          }`}
                        >
                          <span>{getPlatformLabel(thread.platform)}</span>
                          <span className="opacity-60">({thread.stage})</span>
                        </button>
                      ))
                    )}
                  </div>
                </div>

                <div className="flex justify-between items-center border-t border-border pt-3 mt-4 text-[10px] font-mono text-muted">
                  <span>{new Date(draft.createdAt).toLocaleDateString()}</span>
                  <span className="text-accent group-hover:underline">Open in Composer →</span>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
