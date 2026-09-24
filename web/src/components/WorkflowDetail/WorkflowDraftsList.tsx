import React from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBolt, faFileLines, faPlus, faRotateRight } from "@fortawesome/free-solid-svg-icons";
import { type WorkflowDraftSummaryDto } from "../../api/workflows";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { Badge } from "../common/Badge";

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
  const failedDraftsCount = drafts.filter((d) => d.status === "Failed").length;

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
      <div className="flex justify-between items-center">
        <h2 className="text-sm font-bold text-foreground">
          Generated Drafts <span className="text-muted font-normal">({drafts.length})</span>
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
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {drafts.map((draft) => {
            const isFailed = draft.status === "Failed";
            const isRetryingThis = retryingDraftId === draft.id;

            return (
              <div
                key={draft.id}
                onClick={() => handleOpenDraft(draft.id)}
                className={`bg-panel border rounded-xl p-5 flex flex-col justify-between transition hover:shadow-md cursor-pointer group ${
                  isFailed
                    ? "border-red-500/30 hover:border-red-500/60 bg-red-950/10"
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

                  {/* Platform Thread Badges & Status */}
                  <div className="flex flex-wrap items-center gap-1.5">
                    {isFailed && (
                      <Badge variant="danger" data-testid="workflow-draft-failed-badge">
                        Failed
                      </Badge>
                    )}
                    {draft.threads.length === 0 ? (
                      !isFailed && <Badge variant="neutral">{draft.status}</Badge>
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
                          <span>{thread.platform}</span>
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

