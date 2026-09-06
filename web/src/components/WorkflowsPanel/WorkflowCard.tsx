import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faBolt, faEdit, faRss, faTrashCan } from "@fortawesome/free-solid-svg-icons";
import { type AutomationWorkflowDto } from "../../api/workflows";
import { Button } from "../common/Button";

export interface WorkflowCardProps {
  workflow: AutomationWorkflowDto;
  isTriggering?: boolean;
  onClick: (id: string) => void;
  onTrigger: (id: string) => void;
  onEdit: (workflow: AutomationWorkflowDto) => void;
  onDelete: (id: string) => void;
}

export const WorkflowCard: React.FC<WorkflowCardProps> = ({
  workflow,
  isTriggering = false,
  onClick,
  onTrigger,
  onEdit,
  onDelete
}) => {
  return (
    <div
      onClick={() => onClick(workflow.id)}
      className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-600 hover:shadow-md cursor-pointer group shadow-sm"
    >
      <div className="flex flex-col gap-3">
        <div className="flex justify-between items-start">
          <h3 className="text-sm font-bold text-foreground line-clamp-1 group-hover:text-accent transition">
            {workflow.title}
          </h3>
        </div>

        <p className="text-xs text-muted font-sans line-clamp-2 bg-bg/50 p-2.5 rounded-lg border border-border/50">
          "{workflow.instructionPrompt || "Summarize this article as a thread."}"
        </p>

        {/* Sources Section */}
        <div>
          <span className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1.5">
            Ingestion Sources ({workflow.sources.length})
          </span>
          {workflow.sources.length === 0 ? (
            <span className="text-[11px] text-muted italic">No linked feeds</span>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {workflow.sources.map((src) => (
                <span
                  key={src.feedSubscriptionId}
                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-bg border border-border text-[11px] text-foreground font-mono"
                >
                  <FontAwesomeIcon icon={faRss} className="text-[9px] text-accent" />
                  <span className="line-clamp-1">{src.title}</span>
                </span>
              ))}
            </div>
          )}
        </div>

        {/* Destinations Section */}
        <div>
          <span className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1.5">
            Target Destinations ({workflow.destinations.length})
          </span>
          {workflow.destinations.length === 0 ? (
            <span className="text-[11px] text-muted italic">Drafts only (no publishing)</span>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {workflow.destinations.map((dest) => (
                <span
                  key={dest.id}
                  className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-bg border border-border text-[11px] text-foreground font-mono"
                >
                  <span>{dest.platform}</span>
                  {dest.accountHandle && (
                    <span className="opacity-75 font-mono text-[10px]">@{dest.accountHandle}</span>
                  )}
                  {dest.publishingMode === "Scheduled" ? (
                    <span className="text-[8px] bg-blue-500/20 text-blue-400 font-semibold px-1 py-0.5 rounded uppercase">
                      Scheduled {dest.scheduleName ? `(${dest.scheduleName})` : ""}
                    </span>
                  ) : dest.publishingMode === "Manual" ? (
                    <span className="text-[8px] bg-zinc-500/20 text-zinc-400 font-semibold px-1 py-0.5 rounded uppercase">
                      Outbox Review
                    </span>
                  ) : dest.autoPublish || dest.publishingMode === "Automatic" ? (
                    <span className="text-[8px] bg-accent/20 text-accent font-semibold px-1 py-0.5 rounded uppercase">
                      Auto-Publish
                    </span>
                  ) : null}
                </span>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Actions */}
      <div className="flex justify-between items-center border-t border-border pt-4 mt-4">
        <Button
          variant="secondary"
          size="xs"
          loading={isTriggering}
          icon={faBolt}
          onClick={(e) => {
            e.stopPropagation();
            onTrigger(workflow.id);
          }}
          className="font-mono text-foreground"
        >
          {isTriggering ? "Running..." : "Trigger Now"}
        </Button>

        <div className="flex gap-2">
          <Button
            variant="outline"
            size="xs"
            onClick={(e) => {
              e.stopPropagation();
              onEdit(workflow);
            }}
            title="Edit workflow"
            aria-label="Edit workflow"
          >
            <FontAwesomeIcon icon={faEdit} />
          </Button>
          <Button
            variant="danger"
            size="xs"
            onClick={(e) => {
              e.stopPropagation();
              onDelete(workflow.id);
            }}
            title="Delete workflow"
            aria-label="Delete workflow"
          >
            <FontAwesomeIcon icon={faTrashCan} />
          </Button>
        </div>
      </div>
    </div>
  );
};
