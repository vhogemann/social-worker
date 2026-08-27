import React from "react";
import { type FeedSubscriptionDto } from "../../api/feeds";

export interface WorkflowSourcesSelectorProps {
  availableFeeds: FeedSubscriptionDto[];
  selectedFeedIds: string[];
  onToggleFeed: (feedId: string) => void;
}

export const WorkflowSourcesSelector: React.FC<WorkflowSourcesSelectorProps> = ({
  availableFeeds,
  selectedFeedIds,
  onToggleFeed
}) => {
  return (
    <div>
      <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
        Feed Sources ({selectedFeedIds.length} selected)
      </label>
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
  );
};
