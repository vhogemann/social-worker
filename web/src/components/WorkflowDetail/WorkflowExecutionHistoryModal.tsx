import React, { useEffect, useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faRotateRight,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  retryFeedQueueItem,
  deleteFeedQueueItem,
  type FeedQueueItemDto
} from "../../api/feeds";
import { fetchWorkflowQueue } from "../../api/workflows";

interface WorkflowExecutionHistoryModalProps {
  workflowId: string;
  workflowTitle: string;
  isOpen: boolean;
  onClose: () => void;
}

export const WorkflowExecutionHistoryModal: React.FC<WorkflowExecutionHistoryModalProps> = ({
  workflowId,
  workflowTitle,
  isOpen,
  onClose
}) => {
  const [queueItems, setQueueItems] = useState<FeedQueueItemDto[]>([]);
  const [filter, setFilter] = useState<"All" | "Waiting" | "Failed" | "Succeeded">("All");
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadQueue = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchWorkflowQueue(workflowId);
      setQueueItems(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load execution history.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      void loadQueue();
    }
  }, [isOpen, workflowId]);

  if (!isOpen) return null;

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
    <div
      className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4"
      data-testid="execution-history-modal"
    >
      <div className="bg-panel border border-border rounded-xl max-w-4xl w-full p-6 shadow-2xl relative flex flex-col gap-4 max-h-[85vh] overflow-hidden">
        {/* Header */}
        <div className="flex justify-between items-center border-b border-border pb-3 shrink-0">
          <div>
            <h2 className="text-sm font-bold text-foreground">Execution History</h2>
            <p className="text-[11px] text-muted font-mono">
              Feed ingestion and generation runs for <span className="text-foreground font-semibold">{workflowTitle}</span>
            </p>
          </div>
          <div className="flex items-center gap-3">
            <div className="flex items-center gap-1 bg-bg border border-border rounded-lg p-0.5 text-xs">
              {(["All", "Waiting", "Failed", "Succeeded"] as const).map((tab) => (
                <button
                  key={tab}
                  onClick={() => {
                    setFilter(tab);
                    setPage(1);
                  }}
                  className={`px-2.5 py-1 rounded-md text-[11px] font-mono transition ${
                    filter === tab ? "bg-panel text-foreground font-semibold shadow" : "text-muted hover:text-foreground"
                  }`}
                >
                  {tab}
                </button>
              ))}
            </div>
            <button
              onClick={() => loadQueue()}
              disabled={loading}
              className="p-1.5 rounded-lg border border-border hover:bg-bg text-muted hover:text-foreground text-xs transition"
              title="Refresh logs"
            >
              <FontAwesomeIcon icon={faRotateRight} spin={loading} />
            </button>
            <button onClick={onClose} className="text-muted hover:text-foreground transition p-1">
              <FontAwesomeIcon icon={faXmark} />
            </button>
          </div>
        </div>

        {error && (
          <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 rounded-lg text-xs flex justify-between items-center shrink-0">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-400 hover:text-red-300">
              <FontAwesomeIcon icon={faXmark} />
            </button>
          </div>
        )}

        {/* Content Table */}
        <div className="flex-1 overflow-y-auto min-h-0">
          {loading && queueItems.length === 0 ? (
            <div className="flex justify-center items-center py-20 text-muted">
              <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
            </div>
          ) : paginatedItems.length === 0 ? (
            <div className="text-center py-16 bg-bg/40 border border-border rounded-xl">
              <p className="text-xs text-muted">No execution history items found for the selected filter.</p>
            </div>
          ) : (
            <div className="bg-bg/40 border border-border rounded-xl overflow-hidden">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="border-b border-border text-[10px] font-mono uppercase text-muted tracking-wider bg-bg/60">
                    <th className="py-2.5 px-3">Article / Item</th>
                    <th className="py-2.5 px-3">Feed Source</th>
                    <th className="py-2.5 px-3">Status</th>
                    <th className="py-2.5 px-3">Attempts</th>
                    <th className="py-2.5 px-3">Time</th>
                    <th className="py-2.5 px-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {paginatedItems.map((item) => (
                    <tr key={item.id} className="hover:bg-panel/50 transition">
                      <td className="py-2.5 px-3 max-w-[260px]">
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
                      <td className="py-2.5 px-3 text-muted text-[11px] whitespace-nowrap">{item.feedSubscriptionTitle}</td>
                      <td className="py-2.5 px-3">
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
                      <td className="py-2.5 px-3 text-muted font-mono text-[11px]">
                        {item.attemptCount} / {item.maxAttempts}
                      </td>
                      <td className="py-2.5 px-3 text-muted font-mono text-[10px] whitespace-nowrap">
                        {new Date(item.createdAt).toLocaleString()}
                      </td>
                      <td className="py-2.5 px-3 text-right whitespace-nowrap space-x-1">
                        {item.status === "Failed" && (
                          <button
                            onClick={() => handleRetry(item.id)}
                            disabled={actionId === item.id}
                            className="px-2 py-0.5 bg-accent/20 text-accent hover:bg-accent/30 rounded text-[10px] font-mono transition"
                          >
                            Retry
                          </button>
                        )}
                        <button
                          onClick={() => handleDelete(item.id)}
                          disabled={actionId === item.id}
                          className="p-1 text-muted hover:text-red-400 rounded transition"
                          title="Delete queue item"
                        >
                          <FontAwesomeIcon icon={faTrashCan} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Footer Pagination */}
        {totalPages > 1 && (
          <div className="flex justify-between items-center border-t border-border pt-3 text-xs text-muted shrink-0">
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
    </div>
  );
};
