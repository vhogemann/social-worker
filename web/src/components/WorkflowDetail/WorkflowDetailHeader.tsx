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
  const hasAutoPublish = workflow.destinations.some((d) => d.autoPublish);

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
                  className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-panel border border-border text-[11px] text-foreground font-mono"
                >
                  <FontAwesomeIcon icon={faShareNodes} className="text-[10px] text-accent" />
                  <span>{dest.platform}</span>
                  {dest.accountHandle && <span className="opacity-70">@{dest.accountHandle}</span>}
                </span>
              ))
            )}
          </div>
          {hasAutoPublish && (
            <>
              <span className="text-border">·</span>
              <span className="px-1.5 py-0.5 rounded bg-amber-500/10 border border-amber-500/20 text-amber-400 font-mono text-[10px] font-semibold">
                Auto
              </span>
            </>
          )}
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
