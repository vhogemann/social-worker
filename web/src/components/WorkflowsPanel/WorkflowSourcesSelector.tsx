import React from "react";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AutomationWorkflowDto, type WorkflowSourceKind } from "../../api/workflows";

export interface WorkflowSourcesSelectorProps {
  sourceKind: WorkflowSourceKind;
  onSourceKindChange: (kind: WorkflowSourceKind) => void;
  availableFeeds: FeedSubscriptionDto[];
  selectedFeedIds: string[];
  onToggleFeed: (feedId: string) => void;
  availableWorkflows: AutomationWorkflowDto[];
  selectedSourceWorkflowIds: string[];
  onToggleSourceWorkflow: (workflowId: string) => void;
  lookbackDays: number;
  onLookbackDaysChange: (days: number) => void;
}

export const WorkflowSourcesSelector: React.FC<WorkflowSourcesSelectorProps> = ({
  sourceKind,
  onSourceKindChange,
  availableFeeds,
  selectedFeedIds,
  onToggleFeed,
  availableWorkflows,
  selectedSourceWorkflowIds,
  onToggleSourceWorkflow,
  lookbackDays,
  onLookbackDaysChange
}) => {
  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <label className="block text-[10px] font-mono uppercase tracking-wider text-muted">
          Source Type
        </label>
        <div className="flex gap-1 bg-bg p-0.5 rounded-lg border border-border">
          <button
            type="button"
            onClick={() => onSourceKindChange("Feed")}
            className={`px-2.5 py-1 text-xs rounded-md font-medium transition ${
              sourceKind === "Feed"
                ? "bg-accent text-accent-fg shadow-sm"
                : "text-muted hover:text-foreground"
            }`}
          >
            RSS / YouTube Feeds
          </button>
          <button
            type="button"
            onClick={() => onSourceKindChange("WorkflowHighlights")}
            className={`px-2.5 py-1 text-xs rounded-md font-medium transition ${
              sourceKind === "WorkflowHighlights"
                ? "bg-accent text-accent-fg shadow-sm"
                : "text-muted hover:text-foreground"
            }`}
          >
            Workflow Highlights
          </button>
        </div>
      </div>

      {sourceKind === "Feed" ? (
        <div>
          <div className="flex justify-between items-center mb-1.5">
            <span className="text-[10px] font-mono uppercase tracking-wider text-muted">
              Feeds ({selectedFeedIds.length} selected)
            </span>
          </div>
          {availableFeeds.length === 0 ? (
            <p className="text-xs text-muted">No feed subscriptions found. Add feeds in the Feeds tab first.</p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-2 max-h-40 overflow-y-auto p-2 bg-bg border border-border rounded-lg">
              {availableFeeds.map((feed) => {
                const isSelected = selectedFeedIds.includes(feed.id);
                return (
                  <label
                    key={feed.id}
                    className={`flex items-center gap-2 p-2 rounded-lg border cursor-pointer text-xs transition select-none ${
                      isSelected
                        ? "bg-panel border-accent text-foreground"
                        : "border-border text-muted hover:text-foreground hover:bg-panel/50"
                    }`}
                  >
                    <input
                      type="checkbox"
                      checked={isSelected}
                      onChange={() => onToggleFeed(feed.id)}
                      className="rounded border-border text-accent focus:ring-0"
                    />
                    <div className="truncate">
                      <p className="font-medium truncate">{feed.title}</p>
                      <p className="text-[10px] text-muted font-mono truncate">{feed.feedUrl}</p>
                    </div>
                  </label>
                );
              })}
            </div>
          )}
        </div>
      ) : (
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-3 p-2.5 bg-bg/50 border border-border rounded-lg text-xs">
            <label htmlFor="lookback-days" className="font-medium text-foreground whitespace-nowrap">
              Lookback Window:
            </label>
            <input
              id="lookback-days"
              type="number"
              min={1}
              max={90}
              value={lookbackDays}
              onChange={(e) => onLookbackDaysChange(Math.max(1, parseInt(e.target.value) || 7))}
              className="w-20 rounded border border-border bg-bg px-2 py-1 text-xs text-foreground outline-none focus:border-accent"
            />
            <span className="text-muted text-[11px]">days of published posts to aggregate</span>
          </div>

          <div>
            <div className="flex justify-between items-center mb-1.5">
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted">
                Source Workflows to Aggregate ({selectedSourceWorkflowIds.length} selected)
              </span>
            </div>
            {availableWorkflows.length === 0 ? (
              <p className="text-xs text-muted">No other workflows available to aggregate.</p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 max-h-40 overflow-y-auto p-2 bg-bg border border-border rounded-lg">
                {availableWorkflows.map((wf) => {
                  const isSelected = selectedSourceWorkflowIds.includes(wf.id);
                  return (
                    <label
                      key={wf.id}
                      className={`flex items-center gap-2 p-2 rounded-lg border cursor-pointer text-xs transition select-none ${
                        isSelected
                          ? "bg-panel border-accent text-foreground"
                          : "border-border text-muted hover:text-foreground hover:bg-panel/50"
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => onToggleSourceWorkflow(wf.id)}
                        className="rounded border-border text-accent focus:ring-0"
                      />
                      <div className="truncate">
                        <p className="font-medium truncate">{wf.title}</p>
                        <p className="text-[10px] text-muted font-mono truncate">
                          {wf.destinations.map((d) => d.platform).join(", ") || "No destinations"}
                        </p>
                      </div>
                    </label>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};
