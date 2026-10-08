import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faClock,
  faEdit,
  faRss,
  faShareNodes,
  faTrashCan
} from "@fortawesome/free-solid-svg-icons";
import { type AutomationWorkflowDto } from "../../api/workflows";
import { Button } from "../common/Button";
import { getPlatformLabel } from "../../config/platformBadges";

export interface WorkflowDetailHeaderProps {
  workflow: AutomationWorkflowDto;
  isTriggering?: boolean;
  onTrigger: () => void;
  onOpenHistory: () => void;
  onOpenEdit: () => void;
  onDelete: () => void;
}

export const WorkflowDetailHeader: React.FC<WorkflowDetailHeaderProps> = ({
  workflow,
  isTriggering = false,
  onTrigger,
  onOpenHistory,
  onOpenEdit,
  onDelete
}) => {
  return (
    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border pb-5">
      <div className="flex flex-col gap-2">
        <h1 className="text-xl font-bold text-foreground">{workflow.title}</h1>
        <div className="flex flex-wrap items-center gap-3 text-xs text-muted">
          <span className="inline-flex items-center gap-1 font-mono text-[11px]">
            <FontAwesomeIcon icon={faRss} className="text-accent text-[10px]" />
            <span>
              {workflow.sources.length} feed source{workflow.sources.length === 1 ? "" : "s"}
            </span>
          </span>
          <span className="text-border">·</span>
          <div className="flex flex-wrap items-center gap-1.5">
            {workflow.destinations.length === 0 ? (
              <span className="italic text-[11px]">Drafts only</span>
            ) : (
              workflow.destinations.map((dest) => (
                <span
                  key={dest.id}
                  className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-panel border border-border text-[11px] text-foreground font-mono"
                >
                  <FontAwesomeIcon icon={faShareNodes} className="text-[10px] text-accent" />
                  <span>{getPlatformLabel(dest.platform)}</span>
                  {dest.accountHandle && <span className="opacity-70">@{dest.accountHandle}</span>}
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
              ))
            )}
          </div>
        </div>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <Button
          variant="secondary"
          size="sm"
          loading={isTriggering}
          icon={faBolt}
          onClick={onTrigger}
          className="font-mono text-foreground"
        >
          {isTriggering ? "Running..." : "Trigger Now"}
        </Button>

        <Button
          variant="secondary"
          size="sm"
          icon={faClock}
          onClick={onOpenHistory}
          className="font-mono text-muted hover:text-foreground"
        >
          Execution History
        </Button>

        <Button
          variant="outline"
          size="sm"
          onClick={onOpenEdit}
          title="Edit workflow"
          aria-label="Edit workflow"
          className="p-2"
        >
          <FontAwesomeIcon icon={faEdit} />
        </Button>

        <Button
          variant="danger"
          size="sm"
          onClick={onDelete}
          title="Delete workflow"
          aria-label="Delete workflow"
          className="p-2"
        >
          <FontAwesomeIcon icon={faTrashCan} />
        </Button>
      </div>
    </div>
  );
};
