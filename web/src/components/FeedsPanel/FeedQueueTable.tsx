import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faTrashCan } from "@fortawesome/free-solid-svg-icons";
import { type FeedQueueItemDto } from "../../api/feeds";
import { Badge } from "../common/Badge";
import { Button } from "../common/Button";

export interface FeedQueueTableProps {
  items: FeedQueueItemDto[];
  actionId: string | null;
  onRetry: (id: string) => void;
  onDelete: (id: string) => void;
}

export const FeedQueueTable: React.FC<FeedQueueTableProps> = ({
  items,
  actionId,
  onRetry,
  onDelete
}) => {
  const getBadgeVariant = (status: string) => {
    switch (status) {
      case "Succeeded":
        return "success";
      case "Failed":
        return "danger";
      case "Processing":
        return "warning";
      default:
        return "neutral";
    }
  };

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left border-collapse text-xs">
        <thead>
          <tr className="border-b border-border text-[10px] font-mono uppercase text-muted tracking-wider">
            <th className="py-2 px-3">Item</th>
            <th className="py-2 px-3">Feed</th>
            <th className="py-2 px-3">Status</th>
            <th className="py-2 px-3">Attempts</th>
            <th className="py-2 px-3">Time</th>
            <th className="py-2 px-3 text-right">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {items.map((item) => (
            <tr key={item.id} className="hover:bg-bg/50 transition">
              <td className="py-2 px-3 max-w-[280px]">
                <a
                  href={item.itemLink}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="font-medium text-foreground hover:underline line-clamp-1"
                >
                  {item.itemTitle}
                </a>
                {item.lastError && (
                  <p className="text-[10px] text-red-400 font-mono line-clamp-1 mt-0.5" title={item.lastError}>
                    {item.lastError}
                  </p>
                )}
              </td>
              <td className="py-2 px-3 text-muted text-[11px] whitespace-nowrap">
                {item.feedSubscriptionTitle}
              </td>
              <td className="py-2 px-3">
                <Badge variant={getBadgeVariant(item.status)}>{item.status}</Badge>
              </td>
              <td className="py-2 px-3 text-muted font-mono text-[11px]">
                {item.attemptCount} / {item.maxAttempts}
              </td>
              <td className="py-2 px-3 text-muted font-mono text-[10px] whitespace-nowrap">
                {new Date(item.createdAt).toLocaleTimeString()}
              </td>
              <td className="py-2 px-3 text-right whitespace-nowrap space-x-1">
                {item.status === "Failed" && (
                  <Button
                    variant="primary"
                    size="xs"
                    onClick={() => onRetry(item.id)}
                    loading={actionId === item.id}
                    className="font-mono px-2 py-0.5"
                  >
                    Retry
                  </Button>
                )}
                <Button
                  variant="ghost"
                  size="xs"
                  onClick={() => onDelete(item.id)}
                  disabled={actionId === item.id}
                  title="Delete queue item"
                  aria-label="Delete queue item"
                  className="hover:text-red-400 p-1"
                >
                  <FontAwesomeIcon icon={faTrashCan} />
                </Button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
