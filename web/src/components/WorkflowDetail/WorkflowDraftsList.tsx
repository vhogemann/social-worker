import React from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBolt, faFileLines, faPlus } from "@fortawesome/free-solid-svg-icons";
import { type WorkflowDraftSummaryDto } from "../../api/workflows";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { Badge } from "../common/Badge";

export interface WorkflowDraftsListProps {
  workflowId?: string;
  drafts: WorkflowDraftSummaryDto[];
  isTriggering?: boolean;
  onTrigger: () => void;
}

export const WorkflowDraftsList: React.FC<WorkflowDraftsListProps> = ({
  workflowId,
  drafts,
  isTriggering = false,
  onTrigger,
}) => {
  const navigate = useNavigate();

  const handleOpenDraft = (draftId: string) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${draftId}`);
    } else {
      navigate(`/draft/${draftId}`);
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
        <Button
          variant="secondary"
          size="sm"
          icon={faPlus}
          onClick={handleCreateNewDraft}
        >
          New Draft
        </Button>
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
          {drafts.map((draft) => (
            <div
              key={draft.id}
              onClick={() => handleOpenDraft(draft.id)}
              className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-600 hover:shadow-md cursor-pointer group"
              data-testid="workflow-draft-card"
            >
              <div className="flex flex-col gap-3">
                <div className="flex justify-between items-start gap-2">
                  <h3 className="text-sm font-bold text-foreground line-clamp-2 group-hover:text-accent transition">
                    {draft.title}
                  </h3>
                </div>

                {/* Platform Thread Badges */}
                <div className="flex flex-wrap gap-1.5">
                  {draft.threads.length === 0 ? (
                    <Badge variant="neutral">{draft.status}</Badge>
                  ) : (
                    draft.threads.map((thread, idx) => (
                      <span
                        key={idx}
                        className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono font-medium ${
                          thread.stage === "Sent"
                            ? "bg-green-500/10 text-green-400 border border-green-500/20"
                            : "bg-bg text-foreground border border-border"
                        }`}
                      >
                        <span>{thread.platform}</span>
                        <span className="opacity-60">({thread.stage})</span>
                      </span>
                    ))
                  )}
                </div>
              </div>

              <div className="flex justify-between items-center border-t border-border pt-3 mt-4 text-[10px] font-mono text-muted">
                <span>{new Date(draft.createdAt).toLocaleDateString()}</span>
                <span className="text-accent group-hover:underline">Open in Composer →</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
