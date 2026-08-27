import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faRotateRight } from "@fortawesome/free-solid-svg-icons";
import { type FeedQueueItemDto } from "../../api/feeds";
import { FeedQueueTable } from "./FeedQueueTable";
import { Pagination } from "../common/Pagination";
import { EmptyState } from "../common/EmptyState";
import { AlertBanner } from "../common/AlertBanner";
import { Button } from "../common/Button";

export type QueueFilter = "All" | "Waiting" | "Failed" | "Succeeded";

export interface FeedQueueBacklogProps {
  items: FeedQueueItemDto[];
  isLoading: boolean;
  error: string | null;
  actionId: string | null;
  onRefresh: () => void;
  onRetry: (id: string) => void;
  onDelete: (id: string) => void;
}

export const FeedQueueBacklog: React.FC<FeedQueueBacklogProps> = ({
  items,
  isLoading,
  error,
  actionId,
  onRefresh,
  onRetry,
  onDelete
}) => {
  const [filter, setFilter] = useState<QueueFilter>("All");
  const [page, setPage] = useState(1);
  const pageSize = 10;

  const filteredItems = items.filter((item) => {
    if (filter === "All") return true;
    if (filter === "Waiting") return item.status === "Pending" || item.status === "Processing";
    if (filter === "Failed") return item.status === "Failed";
    if (filter === "Succeeded") return item.status === "Succeeded";
    return true;
  });

  const totalPages = Math.ceil(filteredItems.length / pageSize) || 1;
  const paginatedItems = filteredItems.slice((page - 1) * pageSize, page * pageSize);

  const handleFilterChange = (newFilter: QueueFilter) => {
    setFilter(newFilter);
    setPage(1);
  };

  return (
    <div className="bg-panel border border-border rounded-xl p-5 shadow-lg flex flex-col gap-4">
      <div className="flex justify-between items-center border-b border-border pb-3">
        <div>
          <h2 className="text-sm font-bold text-foreground">Processing Backlog</h2>
          <p className="text-[11px] text-muted font-mono">Recent feed items detected and processed</p>
        </div>
        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 bg-bg border border-border rounded-lg p-0.5 text-xs">
            {(["All", "Waiting", "Failed", "Succeeded"] as const).map((f) => (
              <button
                key={f}
                type="button"
                onClick={() => handleFilterChange(f)}
                className={`px-2.5 py-1 rounded-md text-[11px] font-mono transition ${
                  filter === f
                    ? "bg-panel text-foreground font-semibold shadow"
                    : "text-muted hover:text-foreground"
                }`}
              >
                {f}
              </button>
            ))}
          </div>
          <Button
            variant="outline"
            size="xs"
            onClick={onRefresh}
            disabled={isLoading}
            title="Refresh Queue"
            aria-label="Refresh Queue"
            className="p-1.5"
          >
            <FontAwesomeIcon icon={faRotateRight} spin={isLoading} />
          </Button>
        </div>
      </div>

      {error && <AlertBanner variant="error" message={error} />}

      {paginatedItems.length === 0 ? (
        <EmptyState description="No items in the backlog for this filter." className="py-8 bg-transparent border-0" />
      ) : (
        <FeedQueueTable
          items={paginatedItems}
          actionId={actionId}
          onRetry={onRetry}
          onDelete={onDelete}
        />
      )}

      <Pagination
        currentPage={page}
        totalPages={totalPages}
        totalItems={filteredItems.length}
        onPageChange={setPage}
      />
    </div>
  );
};
