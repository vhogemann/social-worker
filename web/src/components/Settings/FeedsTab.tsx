import React, { useEffect, useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faRotateRight,
  faRss,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchFeeds,
  createFeed,
  updateFeed,
  deleteFeed,
  triggerFeed,
  discoverFeed,
  type FeedSubscriptionDto
} from "../../api/feeds";

export const FeedsTab: React.FC = () => {
  const [feeds, setFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form modal state
  const [isModalOpen, setIsModalOpen] = useState(false);
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

  const loadAllFeeds = async () => {
    setLoading(true);
    setError(null);
    try {
      const feedData = await fetchFeeds();
      setFeeds(feedData);
    } catch (err: any) {
      setError(err?.message || "Failed to load feed subscriptions.");
    } finally {
      setLoading(false);
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
      closeModal();
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
    setIsModalOpen(true);
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

  const closeModal = () => {
    setIsModalOpen(false);
    resetForm();
  };

  return (
    <div className="space-y-6" data-testid="feeds-tab">
      <div className="flex justify-between items-center">
        <div>
          <h2 className="text-sm font-bold text-foreground">Subscriptions ({feeds.length})</h2>
          <p className="text-[11px] text-muted font-mono">
            RSS/Atom and YouTube feeds available for workflow sources
          </p>
        </div>
        <button
          onClick={() => {
            resetForm();
            setIsModalOpen(true);
          }}
          className="px-3 py-1.5 rounded-lg bg-accent text-accent-foreground font-semibold text-xs transition hover:opacity-90 flex items-center gap-1.5 shadow"
        >
          <FontAwesomeIcon icon={faPlus} />
          <span>New Feed</span>
        </button>
      </div>

      {error && (
        <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 rounded-lg text-xs flex justify-between items-center">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-400 hover:text-red-300">
            <FontAwesomeIcon icon={faXmark} />
          </button>
        </div>
      )}

      {loading && feeds.length === 0 ? (
        <div className="flex justify-center items-center py-20 text-muted">
          <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
        </div>
      ) : feeds.length === 0 ? (
        <div className="text-center py-16 bg-panel border border-border rounded-xl flex flex-col items-center gap-3">
          <div className="p-3 rounded-full bg-bg border border-border text-muted">
            <FontAwesomeIcon icon={faRss} className="text-xl" />
          </div>
          <h3 className="text-sm font-semibold text-foreground">No feed subscriptions yet</h3>
          <p className="text-xs text-muted max-w-sm">
            Add RSS, Atom, or YouTube feeds to automatically monitor new content for your workflows.
          </p>
          <button
            onClick={() => {
              resetForm();
              setIsModalOpen(true);
            }}
            className="mt-2 px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition"
          >
            Add First Feed
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {feeds.map((feed) => (
            <div
              key={feed.id}
              className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-700 shadow-sm"
            >
              <div>
                <div className="flex justify-between items-start mb-2">
                  <h3 className="text-sm font-bold text-foreground line-clamp-1">{feed.title}</h3>
                </div>

                <p className="text-[10px] text-muted font-mono break-all mb-4">{feed.feedUrl}</p>

                <div className="space-y-1.5 mb-4 text-xs">
                  <div className="flex justify-between">
                    <span className="text-muted">Last Polled:</span>
                    <span className="text-foreground font-mono text-[11px]">
                      {feed.lastPolledAt ? new Date(feed.lastPolledAt).toLocaleString() : "Never"}
                    </span>
                  </div>
                  {feed.includeFilters && (
                    <div className="flex justify-between">
                      <span className="text-muted">Include:</span>
                      <span className="text-foreground font-mono text-[10px] bg-bg px-1.5 py-0.5 rounded border border-border">
                        {feed.includeFilters}
                      </span>
                    </div>
                  )}
                  {feed.excludeFilters && (
                    <div className="flex justify-between">
                      <span className="text-muted">Exclude:</span>
                      <span className="text-foreground font-mono text-[10px] bg-bg px-1.5 py-0.5 rounded border border-border">
                        {feed.excludeFilters}
                      </span>
                    </div>
                  )}
                </div>
              </div>

              <div className="flex justify-between items-center border-t border-border pt-4 mt-2">
                <button
                  onClick={() => handleTrigger(feed.id)}
                  disabled={triggeringId === feed.id}
                  className="px-2.5 py-1 rounded bg-bg border border-border hover:border-accent text-foreground text-xs font-mono flex items-center gap-1.5 transition disabled:opacity-50"
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
                    title="Delete feed"
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
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-panel border border-border rounded-xl max-w-xl w-full p-6 shadow-2xl relative flex flex-col gap-5">
            <div className="flex justify-between items-center border-b border-border pb-3">
              <h2 className="text-sm font-bold text-foreground">
                {editingId ? "Edit Feed Subscription" : "Add Feed Subscription"}
              </h2>
              <button onClick={closeModal} className="text-muted hover:text-foreground">
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
                <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                  Feed Title
                </label>
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
                <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                  Feed URL
                </label>
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
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Include Filters (comma-separated)
                  </label>
                  <input
                    type="text"
                    value={includeFilters}
                    onChange={(e) => setIncludeFilters(e.target.value)}
                    placeholder="e.g. dotnet, csharp, ai"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Exclude Filters (comma-separated)
                  </label>
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
                  onClick={closeModal}
                  className="px-4 py-2 rounded-lg border border-border hover:bg-bg text-xs text-muted hover:text-foreground transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition shadow"
                >
                  {editingId ? "Save Changes" : "Create Subscription"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
