import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faRotateRight, faTrashCan } from "@fortawesome/free-solid-svg-icons";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { Button } from "../common/Button";

export interface FeedSubscriptionCardProps {
  feed: FeedSubscriptionDto;
  isTriggering?: boolean;
  onTrigger: (id: string) => void;
  onEdit: (feed: FeedSubscriptionDto) => void;
  onDelete: (id: string) => void;
}

export const FeedSubscriptionCard: React.FC<FeedSubscriptionCardProps> = ({
  feed,
  isTriggering = false,
  onTrigger,
  onEdit,
  onDelete
}) => {
  return (
    <div className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-700">
      <div>
        <div className="flex justify-between items-start mb-2">
          <h3 className="text-sm font-bold text-foreground line-clamp-1">{feed.title}</h3>
        </div>

        <p className="text-[10px] text-muted font-mono break-all mb-4">{feed.feedUrl}</p>

        <div className="space-y-1 mb-4 text-xs">
          <div className="flex justify-between">
            <span className="text-muted">Last Polled:</span>
            <span className="text-foreground">
              {feed.lastPolledAt ? new Date(feed.lastPolledAt).toLocaleString() : "Never"}
            </span>
          </div>
          {feed.includeFilters && (
            <div className="flex justify-between">
              <span className="text-muted">Include:</span>
              <span className="text-foreground font-mono text-[10px]">{feed.includeFilters}</span>
            </div>
          )}
          {feed.excludeFilters && (
            <div className="flex justify-between">
              <span className="text-muted">Exclude:</span>
              <span className="text-foreground font-mono text-[10px]">{feed.excludeFilters}</span>
            </div>
          )}
        </div>
      </div>

      <div className="flex justify-between items-center border-t border-border pt-4 mt-2">
        <Button
          variant="secondary"
          size="xs"
          loading={isTriggering}
          icon={faRotateRight}
          onClick={() => onTrigger(feed.id)}
          className="font-mono text-foreground"
        >
          {isTriggering ? "Polling..." : "Poll Now"}
        </Button>
        <div className="flex gap-2">
          <Button variant="outline" size="xs" onClick={() => onEdit(feed)}>
            Edit
          </Button>
          <Button
            variant="danger"
            size="xs"
            onClick={() => onDelete(feed.id)}
            title="Delete subscription"
            aria-label="Delete subscription"
          >
            <FontAwesomeIcon icon={faTrashCan} />
          </Button>
        </div>
      </div>
    </div>
  );
};
