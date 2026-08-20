import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowLeft,
  faRotateRight,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchFeedQueue,
  retryFeedQueueItem,
  deleteFeedQueueItem,
  type FeedQueueItemDto
} from "../../api/feeds";

export const LogsPanel: React.FC = () => {
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
            <button
              onClick={() => loadQueue()}
              disabled={loading}
              className="p-2 rounded-lg border border-border bg-panel hover:bg-bg text-muted hover:text-foreground text-xs transition"
              title="Refresh Logs"
            >
              <FontAwesomeIcon icon={faRotateRight} spin={loading} />
            </button>
          </div>
        </div>

        {error && (
          <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 rounded-lg text-xs flex justify-between items-center">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-400 hover:text-red-300">
              <FontAwesomeIcon icon={faXmark} />
            </button>
          </div>
        )}

        {/* Table View */}
        {loading && queueItems.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : paginatedItems.length === 0 ? (
          <div className="text-center py-16 bg-panel border border-border rounded-xl">
            <p className="text-xs text-muted">No backlog items found for the selected filter.</p>
          </div>
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
                        <span
                          className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-mono font-medium ${
                            item.status === "Succeeded"
                              ? "bg-green-500/10 text-green-400 border border-green-500/20"
                              : item.status === "Failed"
                              ? "bg-red-500/10 text-red-400 border border-red-500/20"
                              : item.status === "Processing"
                              ? "bg-yellow-500/10 text-yellow-400 border border-yellow-500/20"
                              : "bg-zinc-500/10 text-muted border border-border"
                          }`}
                        >
                          {item.status}
                        </span>
                      </td>
                      <td className="py-3 px-4 text-muted font-mono text-[11px]">
                        {item.attemptCount} / {item.maxAttempts}
                      </td>
                      <td className="py-3 px-4 text-muted font-mono text-[10px] whitespace-nowrap">
                        {new Date(item.createdAt).toLocaleString()}
                      </td>
                      <td className="py-3 px-4 text-right whitespace-nowrap space-x-1">
                        {item.status === "Failed" && (
                          <button
                            onClick={() => handleRetry(item.id)}
                            disabled={actionId === item.id}
                            className="px-2.5 py-1 bg-accent/20 text-accent hover:bg-accent/30 rounded text-[10px] font-mono transition"
                          >
                            Retry
                          </button>
                        )}
                        <button
                          onClick={() => handleDelete(item.id)}
                          disabled={actionId === item.id}
                          className="p-1.5 text-muted hover:text-red-400 rounded transition"
                          title="Delete log entry"
                        >
                          <FontAwesomeIcon icon={faTrashCan} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Pagination Bar */}
            {totalPages > 1 && (
              <div className="flex justify-between items-center border-t border-border px-4 py-3 text-xs text-muted">
                <span>
                  Page {page} of {totalPages} ({filteredItems.length} items)
                </span>
                <div className="flex gap-1">
                  <button
                    disabled={page === 1}
                    onClick={() => setPage((p) => p - 1)}
                    className="px-2.5 py-1 rounded border border-border disabled:opacity-40 hover:bg-bg"
                  >
                    Prev
                  </button>
                  <button
                    disabled={page === totalPages}
                    onClick={() => setPage((p) => p + 1)}
                    className="px-2.5 py-1 rounded border border-border disabled:opacity-40 hover:bg-bg"
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
