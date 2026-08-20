import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowLeft,
  faClock,
  faFilter,
  faPlus,
  faRotateRight,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchFeeds,
  fetchFeedQueue,
  createFeed,
  updateFeed,
  deleteFeed,
  triggerFeed,
  discoverFeed,
  retryFeedQueueItem,
  deleteFeedQueueItem,
  type FeedQueueItemDto,
  type FeedSubscriptionDto
} from "../../api/feeds";

export const FeedsPanel: React.FC = () => {
  const [feeds, setFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [queueItems, setQueueItems] = useState<FeedQueueItemDto[]>([]);
  const [isBacklogOpen, setIsBacklogOpen] = useState(false);
  const [queueFilter, setQueueFilter] = useState<"All" | "Waiting" | "Failed" | "Succeeded">("All");
  const [queuePage, setQueuePage] = useState(1);
  const [queuePageSize, setQueuePageSize] = useState(10);
  const [loading, setLoading] = useState(false);
  const [queueLoading, setQueueLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [queueError, setQueueError] = useState<string | null>(null);

  // Form state
  const [isSubscriptionModalOpen, setIsSubscriptionModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [inputUrl, setInputUrl] = useState("");
  const [title, setTitle] = useState("");
  const [feedUrl, setFeedUrl] = useState("");
  const [websiteUrl, setWebsiteUrl] = useState("");
  const [includeFilters, setIncludeFilters] = useState("");
  const [excludeFilters, setExcludeFilters] = useState("");
  const [discovering, setDiscovering] = useState(false);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [queueActionId, setQueueActionId] = useState<string | null>(null);

  const loadAllFeeds = async () => {
    setLoading(true);
    setQueueLoading(true);
    setError(null);
    setQueueError(null);
    try {
      const [feedData, queueData] = await Promise.all([fetchFeeds(), fetchFeedQueue()]);
      setFeeds(feedData);
      setQueueItems(queueData);
    } catch (err: any) {
      setError(err?.message || "Failed to load feed subscriptions.");
      setQueueError(err?.message || "Failed to load processing backlog.");
    } finally {
      setLoading(false);
      setQueueLoading(false);
    }
  };

  useEffect(() => {
    void loadAllFeeds();
  }, []);

  const handleDiscover = async () => {
    if (!inputUrl.trim()) return;
    setDiscovering(true);
    setDiscoveryError(null);
    try {
      const result = await discoverFeed(inputUrl);
      if (result.success) {
        setFeedUrl(result.feedUrl);
        setTitle(result.title || "Discovered Feed");
        setWebsiteUrl(result.websiteUrl || inputUrl);
      } else {
        setDiscoveryError(result.error || "Could not discover any feed at this URL.");
      }
    } catch (err: any) {
      setDiscoveryError(err?.message || "Feed discovery failed.");
    } finally {
      setDiscovering(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !feedUrl.trim()) return;

    setError(null);
    const payload = {
      title: title.trim(),
      feedUrl: feedUrl.trim(),
      websiteUrl: websiteUrl.trim() || undefined,
      includeFilters: includeFilters.trim() || undefined,
      excludeFilters: excludeFilters.trim() || undefined
    };

    try {
      if (editingId) {
        await updateFeed(editingId, payload);
      } else {
        await createFeed(payload);
      }
      setIsSubscriptionModalOpen(false);
      setEditingId(null);
      resetForm();
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to save feed subscription.");
    }
  };

  const handleEdit = (feed: FeedSubscriptionDto) => {
    setEditingId(feed.id);
    setTitle(feed.title);
    setFeedUrl(feed.feedUrl);
    setWebsiteUrl(feed.websiteUrl || "");
    setIncludeFilters(feed.includeFilters || "");
    setExcludeFilters(feed.excludeFilters || "");
    setInputUrl(feed.websiteUrl || feed.feedUrl);
    setIsSubscriptionModalOpen(true);
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this subscription?")) return;
    try {
      await deleteFeed(id);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to delete subscription.");
    }
  };

  const handleTrigger = async (id: string) => {
    setTriggeringId(id);
    setError(null);
    try {
      await triggerFeed(id);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger feed polling.");
    } finally {
      setTriggeringId(null);
    }
  };

  const handleRetryQueueItem = async (id: string) => {
    setQueueActionId(id);
    setQueueError(null);
    try {
      await retryFeedQueueItem(id);
      await loadAllFeeds();
    } catch (err: any) {
      setQueueError(err?.message || "Failed to retry queue item.");
    } finally {
      setQueueActionId(null);
    }
  };

  const handleDeleteQueueItem = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this item from the queue?")) return;
    setQueueActionId(id);
    setQueueError(null);
    try {
      await deleteFeedQueueItem(id);
      await loadAllFeeds();
    } catch (err: any) {
      setQueueError(err?.message || "Failed to delete queue item.");
    } finally {
      setQueueActionId(null);
    }
  };

  const filteredQueueItems = queueItems.filter((item) => {
    if (queueFilter === "All") return true;
    if (queueFilter === "Waiting") return item.status === "Pending" || item.status === "Processing";
    if (queueFilter === "Failed") return item.status === "Failed";
    if (queueFilter === "Succeeded") return item.status === "Succeeded";
    return true;
  });

  const totalQueuePages = Math.ceil(filteredQueueItems.length / queuePageSize) || 1;
  const paginatedQueueItems = filteredQueueItems.slice(
    (queuePage - 1) * queuePageSize,
    queuePage * queuePageSize
  );

  const resetForm = () => {
    setEditingId(null);
    setInputUrl("");
    setTitle("");
    setFeedUrl("");
    setWebsiteUrl("");
    setIncludeFilters("");
    setExcludeFilters("");
    setDiscoveryError(null);
  };

  const closeSubscriptionModal = () => {
    setIsSubscriptionModalOpen(false);
    resetForm();
  };

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Top Header */}
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Link to="/" className="text-muted hover:text-foreground text-xs flex items-center gap-1.5 transition">
              <FontAwesomeIcon icon={faArrowLeft} />
              <span>Back</span>
            </Link>
            <h1 className="text-lg font-bold text-foreground">Feed Automation</h1>
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => setIsBacklogOpen(!isBacklogOpen)}
              className={`px-3 py-1.5 rounded-lg border text-xs font-mono transition flex items-center gap-1.5 ${
                isBacklogOpen ? "bg-panel border-accent text-accent" : "border-border text-muted hover:text-foreground hover:bg-panel"
              }`}
            >
              <FontAwesomeIcon icon={faClock} />
              <span>Processing Backlog ({queueItems.length})</span>
            </button>
            <button
              onClick={() => {
                resetForm();
                setIsSubscriptionModalOpen(true);
              }}
              className="px-3 py-1.5 rounded-lg bg-accent text-accent-foreground font-semibold text-xs transition hover:opacity-90 flex items-center gap-1.5"
            >
              <FontAwesomeIcon icon={faPlus} />
              <span>New Feed</span>
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

        {/* Backlog View Modal / Drawer */}
        {isBacklogOpen && (
          <div className="bg-panel border border-border rounded-xl p-5 shadow-lg flex flex-col gap-4">
            <div className="flex justify-between items-center border-b border-border pb-3">
              <div>
                <h2 className="text-sm font-bold text-foreground">Processing Backlog</h2>
                <p className="text-[11px] text-muted font-mono">Recent feed items detected and processed</p>
              </div>
              <div className="flex items-center gap-2">
                <div className="flex items-center gap-1 bg-bg border border-border rounded-lg p-0.5 text-xs">
                  {(["All", "Waiting", "Failed", "Succeeded"] as const).map((filter) => (
                    <button
                      key={filter}
                      onClick={() => {
                        setQueueFilter(filter);
                        setQueuePage(1);
                      }}
                      className={`px-2.5 py-1 rounded-md text-[11px] font-mono transition ${
                        queueFilter === filter ? "bg-panel text-foreground font-semibold shadow" : "text-muted hover:text-foreground"
                      }`}
                    >
                      {filter}
                    </button>
                  ))}
                </div>
                <button
                  onClick={() => loadAllFeeds()}
                  disabled={queueLoading}
                  className="p-1.5 rounded-lg border border-border hover:bg-bg text-muted hover:text-foreground text-xs"
                  title="Refresh Queue"
                >
                  <FontAwesomeIcon icon={faRotateRight} spin={queueLoading} />
                </button>
              </div>
            </div>

            {queueError && (
              <div className="p-2 bg-red-500/10 border border-red-500/20 text-red-400 rounded text-xs">
                {queueError}
              </div>
            )}

            {paginatedQueueItems.length === 0 ? (
              <div className="text-center py-8 text-xs text-muted">No items in the backlog for this filter.</div>
            ) : (
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
                    {paginatedQueueItems.map((item) => (
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
                        <td className="py-2 px-3 text-muted text-[11px] whitespace-nowrap">{item.feedSubscriptionTitle}</td>
                        <td className="py-2 px-3">
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
                        <td className="py-2 px-3 text-muted font-mono text-[11px]">
                          {item.attemptCount} / {item.maxAttempts}
                        </td>
                        <td className="py-2 px-3 text-muted font-mono text-[10px] whitespace-nowrap">
                          {new Date(item.createdAt).toLocaleTimeString()}
                        </td>
                        <td className="py-2 px-3 text-right whitespace-nowrap space-x-1">
                          {item.status === "Failed" && (
                            <button
                              onClick={() => handleRetryQueueItem(item.id)}
                              disabled={queueActionId === item.id}
                              className="px-2 py-0.5 bg-accent/20 text-accent hover:bg-accent/30 rounded text-[10px] font-mono"
                            >
                              Retry
                            </button>
                          )}
                          <button
                            onClick={() => handleDeleteQueueItem(item.id)}
                            disabled={queueActionId === item.id}
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

            {/* Pagination */}
            {totalQueuePages > 1 && (
              <div className="flex justify-between items-center border-t border-border pt-3 text-xs text-muted">
                <span>
                  Page {queuePage} of {totalQueuePages} ({filteredQueueItems.length} items)
                </span>
                <div className="flex gap-1">
                  <button
                    disabled={queuePage === 1}
                    onClick={() => setQueuePage((p) => p - 1)}
                    className="px-2 py-1 rounded border border-border disabled:opacity-40 hover:bg-bg"
                  >
                    Prev
                  </button>
                  <button
                    disabled={queuePage === totalQueuePages}
                    onClick={() => setQueuePage((p) => p + 1)}
                    className="px-2 py-1 rounded border border-border disabled:opacity-40 hover:bg-bg"
                  >
                    Next
                  </button>
                </div>
              </div>
            )}
          </div>
        )}

        {/* Feed Subscriptions List */}
        {loading && feeds.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : feeds.length === 0 ? (
          <div className="text-center py-12 bg-panel border border-border rounded-xl">
            <p className="text-xs text-muted">No feed subscriptions found. Add your first feed subscription.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {feeds.map((feed) => (
              <div key={feed.id} className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-700">
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
                  <button
                    onClick={() => handleTrigger(feed.id)}
                    disabled={triggeringId === feed.id}
                    className="px-2.5 py-1 rounded bg-panel border border-border hover:border-accent text-foreground text-xs font-mono flex items-center gap-1.5 transition disabled:opacity-50"
                  >
                    <FontAwesomeIcon icon={faRotateRight} spin={triggeringId === feed.id} />
                    <span>{triggeringId === feed.id ? "Polling..." : "Poll Now"}</span>
                  </button>
                  <div className="flex gap-2">
                    <button
                      onClick={() => handleEdit(feed)}
                      className="px-2.5 py-1 rounded border border-border hover:bg-bg text-muted hover:text-foreground text-xs transition"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(feed.id)}
                      className="px-2 py-1 rounded border border-border hover:bg-red-500/10 text-muted hover:text-red-400 text-xs transition"
                    >
                      <FontAwesomeIcon icon={faTrashCan} />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Modal for Creating / Editing Subscriptions */}
        {isSubscriptionModalOpen && (
          <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-panel border border-border rounded-xl max-w-xl w-full p-6 shadow-2xl relative flex flex-col gap-5">
              <div className="flex justify-between items-center border-b border-border pb-3">
                <h2 className="text-sm font-bold text-foreground">
                  {editingId ? "Edit Feed Subscription" : "Add Feed Subscription"}
                </h2>
                <button onClick={closeSubscriptionModal} className="text-muted hover:text-foreground">
                  <FontAwesomeIcon icon={faXmark} />
                </button>
              </div>

              {/* URL Auto-Discovery */}
              {!editingId && (
                <div className="p-3 bg-bg border border-border rounded-lg flex flex-col gap-2">
                  <label className="text-[10px] font-mono uppercase tracking-wider text-muted">
                    Auto-Discover Feed URL (RSS / Atom / YouTube)
                  </label>
                  <div className="flex gap-2">
                    <input
                      type="url"
                      placeholder="e.g. https://youtube.com/@channel or blog URL"
                      value={inputUrl}
                      onChange={(e) => setInputUrl(e.target.value)}
                      className="flex-1 rounded-lg border border-border bg-panel px-3 py-1.5 text-xs text-foreground outline-none focus:border-accent"
                    />
                    <button
                      type="button"
                      onClick={handleDiscover}
                      disabled={discovering || !inputUrl.trim()}
                      className="px-3 py-1.5 rounded-lg bg-panel border border-border hover:border-accent text-xs font-mono text-foreground disabled:opacity-50 transition"
                    >
                      {discovering ? <FontAwesomeIcon icon={faSpinner} spin /> : "Discover"}
                    </button>
                  </div>
                  {discoveryError && <p className="text-[10px] text-red-400 font-mono">{discoveryError}</p>}
                </div>
              )}

              <form onSubmit={handleSave} className="flex flex-col gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Feed Title</label>
                  <input
                    required
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="e.g. .NET Blog RSS"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Feed URL</label>
                  <input
                    required
                    type="url"
                    value={feedUrl}
                    onChange={(e) => setFeedUrl(e.target.value)}
                    placeholder="https://example.com/rss.xml"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent font-mono"
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Include Filters (comma-separated)</label>
                    <input
                      type="text"
                      value={includeFilters}
                      onChange={(e) => setIncludeFilters(e.target.value)}
                      placeholder="e.g. dotnet, csharp, ai"
                      className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Exclude Filters (comma-separated)</label>
                    <input
                      type="text"
                      value={excludeFilters}
                      onChange={(e) => setExcludeFilters(e.target.value)}
                      placeholder="e.g. crypto, jobs, sponsored"
                      className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                    />
                  </div>
                </div>

                <div className="flex justify-end gap-2 border-t border-border pt-4 mt-2">
                  <button
                    type="button"
                    onClick={closeSubscriptionModal}
                    className="px-4 py-2 rounded-lg border border-border hover:bg-bg text-xs text-muted hover:text-foreground transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition"
                  >
                    {editingId ? "Save Changes" : "Create Subscription"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
