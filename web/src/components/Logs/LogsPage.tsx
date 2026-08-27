import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowLeft,
  faRotateRight,
  faSpinner,
  faTrashCan
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchFeedQueue,
  retryFeedQueueItem,
  deleteFeedQueueItem,
  type FeedQueueItemDto
} from "../../api/feeds";
import { Button } from "../common/Button";
import { Badge } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import { Pagination } from "../common/Pagination";
import { EmptyState } from "../common/EmptyState";

export const LogsPage: React.FC = () => {
  const [queueItems, setQueueItems] = useState<FeedQueueItemDto[]>([]);
  const [filter, setFilter] = useState<"All" | "Waiting" | "Failed" | "Succeeded">("All");
  const [page, setPage] = useState(1);
  const pageSize = 15;
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadQueue = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchFeedQueue();
      setQueueItems(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load processing backlog logs.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadQueue();
  }, []);

  const handleRetry = async (id: string) => {
    setActionId(id);
    setError(null);
    try {
      await retryFeedQueueItem(id);
      await loadQueue();
    } catch (err: any) {
      setError(err?.message || "Failed to retry queue item.");
    } finally {
      setActionId(null);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this log entry?")) return;
    setActionId(id);
    setError(null);
    try {
      await deleteFeedQueueItem(id);
      await loadQueue();
    } catch (err: any) {
      setError(err?.message || "Failed to delete log entry.");
    } finally {
      setActionId(null);
    }
  };

  const filteredItems = queueItems.filter((item) => {
    if (filter === "All") return true;
    if (filter === "Waiting") return item.status === "Pending" || item.status === "Processing";
    if (filter === "Failed") return item.status === "Failed";
    if (filter === "Succeeded") return item.status === "Succeeded";
    return true;
  });

  const totalPages = Math.ceil(filteredItems.length / pageSize) || 1;
  const paginatedItems = filteredItems.slice((page - 1) * pageSize, page * pageSize);

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
    <div className="flex flex-col h-full bg-bg overflow-y-auto" data-testid="logs-panel">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Header */}
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Link to="/" className="text-muted hover:text-foreground text-xs flex items-center gap-1.5 transition">
              <FontAwesomeIcon icon={faArrowLeft} />
              <span>Back</span>
            </Link>
            <div>
              <h1 className="text-lg font-bold text-foreground">Processing Logs & Backlog</h1>
              <p className="text-[11px] text-muted font-mono">Real-time status of feed ingestion and workflow generation runs</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <div className="flex items-center gap-1 bg-panel border border-border rounded-lg p-0.5 text-xs">
              {(["All", "Waiting", "Failed", "Succeeded"] as const).map((tab) => (
                <button
                  key={tab}
                  onClick={() => {
                    setFilter(tab);
                    setPage(1);
                  }}
                  className={`px-3 py-1 rounded-md text-[11px] font-mono transition ${
                    filter === tab ? "bg-bg text-foreground font-semibold shadow" : "text-muted hover:text-foreground"
                  }`}
                >
                  {tab}
                </button>
              ))}
            </div>
            <Button
              variant="outline"
              size="xs"
              onClick={() => loadQueue()}
              disabled={loading}
              title="Refresh Logs"
              aria-label="Refresh Logs"
              className="p-2"
            >
              <FontAwesomeIcon icon={faRotateRight} spin={loading} />
            </Button>
          </div>
        </div>

        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {/* Table View */}
        {loading && queueItems.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : paginatedItems.length === 0 ? (
          <EmptyState
            description="No backlog items found for the selected filter."
            className="py-16"
          />
        ) : (
          <div className="bg-panel border border-border rounded-xl overflow-hidden shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-border bg-bg/40 text-[10px] font-mono uppercase text-muted tracking-wider">
                    <th className="py-2.5 px-4">Article / Item</th>
                    <th className="py-2.5 px-4">Feed Source</th>
                    <th className="py-2.5 px-4">Target Workflow</th>
                    <th className="py-2.5 px-4">Status</th>
                    <th className="py-2.5 px-4">Attempts</th>
                    <th className="py-2.5 px-4">Timestamp</th>
                    <th className="py-2.5 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {paginatedItems.map((item) => (
                    <tr key={item.id} className="hover:bg-bg/40 transition">
                      <td className="py-3 px-4 max-w-[260px]">
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
                      <td className="py-3 px-4 text-muted text-[11px] whitespace-nowrap">{item.feedSubscriptionTitle}</td>
                      <td className="py-3 px-4 text-muted text-[11px] whitespace-nowrap">
                        {item.automationWorkflowTitle || "Default Workflow"}
                      </td>
                      <td className="py-3 px-4">
                        <Badge variant={getBadgeVariant(item.status)}>{item.status}</Badge>
                      </td>
                      <td className="py-3 px-4 text-muted font-mono text-[11px]">
                        {item.attemptCount} / {item.maxAttempts}
                      </td>
                      <td className="py-3 px-4 text-muted font-mono text-[10px] whitespace-nowrap">
                        {new Date(item.createdAt).toLocaleString()}
                      </td>
                      <td className="py-3 px-4 text-right whitespace-nowrap space-x-1">
                        {item.status === "Failed" && (
                          <Button
                            variant="primary"
                            size="xs"
                            onClick={() => handleRetry(item.id)}
                            loading={actionId === item.id}
                            className="font-mono px-2.5 py-1"
                          >
                            Retry
                          </Button>
                        )}
                        <Button
                          variant="ghost"
                          size="xs"
                          onClick={() => handleDelete(item.id)}
                          disabled={actionId === item.id}
                          title="Delete log entry"
                          aria-label="Delete log entry"
                          className="p-1.5 hover:text-red-400"
                        >
                          <FontAwesomeIcon icon={faTrashCan} />
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination Bar */}
            <Pagination
              currentPage={page}
              totalPages={totalPages}
              totalItems={filteredItems.length}
              onPageChange={setPage}
              className="px-4 py-3"
            />
          </div>
        )}
      </div>
    </div>
  );
};

export { LogsPage as LogsPanel };
