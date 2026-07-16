import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  fetchFeeds,
  createFeed,
  updateFeed,
  deleteFeed,
  triggerFeed,
  discoverFeed,
  type FeedSubscriptionDto
} from "../../api/feeds";

export const FeedsPanel: React.FC = () => {
  const [feeds, setFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form state
  const [isEditing, setIsEditing] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [inputUrl, setInputUrl] = useState("");
  const [title, setTitle] = useState("");
  const [feedUrl, setFeedUrl] = useState("");
  const [websiteUrl, setWebsiteUrl] = useState("");
  const [instructionPrompt, setInstructionPrompt] = useState("Summarize this article as a thread.");
  const [autoPublish, setAutoPublish] = useState(false);
  const [includeFilters, setIncludeFilters] = useState("");
  const [excludeFilters, setExcludeFilters] = useState("");
  const [discovering, setDiscovering] = useState(false);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);

  const loadAllFeeds = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await fetchFeeds();
      setFeeds(data);
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
      instructionPrompt: instructionPrompt.trim(),
      autoPublish,
      includeFilters: includeFilters.trim() || undefined,
      excludeFilters: excludeFilters.trim() || undefined
    };

    try {
      if (editingId) {
        await updateFeed(editingId, payload);
      } else {
        await createFeed(payload);
      }
      setIsEditing(false);
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
    setInstructionPrompt(feed.instructionPrompt);
    setAutoPublish(feed.autoPublish);
    setIncludeFilters(feed.includeFilters || "");
    setExcludeFilters(feed.excludeFilters || "");
    setInputUrl(feed.websiteUrl || feed.feedUrl);
    setIsEditing(true);
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this feed subscription?")) return;
    try {
      await deleteFeed(id);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to delete subscription.");
    }
  };

  const handleTrigger = async (id: string) => {
    setTriggeringId(id);
    try {
      await triggerFeed(id);
      alert("Polling completed successfully! New items are being processed in the background.");
      await loadAllFeeds();
    } catch (err: any) {
      alert("Trigger failed: " + (err?.message || "Unknown error"));
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
    setInstructionPrompt("Summarize this article as a thread.");
    setAutoPublish(false);
    setIncludeFilters("");
    setExcludeFilters("");
    setDiscoveryError(null);
  };

  return (
    <div className="flex-1 min-h-0 flex flex-col bg-bg text-foreground overflow-y-auto p-6 font-sans">
      <div className="max-w-6xl w-full mx-auto flex flex-col h-full">
        {/* Header */}
        <div className="flex items-center justify-between mb-6 shrink-0">
          <div>
            <h1 className="text-xl font-bold text-foreground">RSS/Atom Feed Subscriptions</h1>
            <p className="text-xs text-muted mt-1">Automate thread composition and publishing from external websites and YouTube channels</p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => {
                resetForm();
                setIsEditing(true);
              }}
              className="px-4 py-2 bg-accent text-bg text-xs font-bold rounded-lg shadow transition hover:opacity-90"
            >
              Add Subscription
            </button>
            <Link
              to="/"
              className="flex items-center gap-2 px-4 py-2 bg-panel border border-border hover:bg-zinc-800 text-xs font-semibold rounded-lg shadow-sm transition text-zinc-300"
            >
              &larr; Back to Composer
            </Link>
          </div>
        </div>

        {error && (
          <div className="mb-6 p-3 bg-red-950/40 border border-red-500/50 rounded-lg text-xs text-red-200">
            {error}
          </div>
        )}

        {isEditing ? (
          /* Create / Edit Form */
          <div className="bg-panel border border-border rounded-xl p-6 mb-6 max-w-2xl">
            <h2 className="text-sm font-bold mb-4">{editingId ? "Edit Subscription" : "Add New Subscription"}</h2>
            <form onSubmit={handleSave} className="space-y-4">
              <div>
                <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Auto-Discover Feed URL</label>
                <div className="flex gap-2">
                  <input
                    type="text"
                    value={inputUrl}
                    onChange={(e) => setInputUrl(e.target.value)}
                    placeholder="Enter website URL or YouTube channel handle/URL (e.g. @ChannelName)"
                    className="flex-1 rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                  <button
                    type="button"
                    onClick={handleDiscover}
                    disabled={discovering}
                    className="px-4 py-2 bg-zinc-800 border border-border rounded-lg text-xs font-semibold transition hover:bg-zinc-700 disabled:opacity-50 text-zinc-300"
                  >
                    {discovering ? "Discovering..." : "Discover"}
                  </button>
                </div>
                {discoveryError && (
                  <p className="mt-1 text-[10px] text-red-400">{discoveryError}</p>
                )}
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Title</label>
                  <input
                    type="text"
                    required
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Feed URL (discovered or manual)</label>
                  <input
                    type="text"
                    required
                    value={feedUrl}
                    onChange={(e) => setFeedUrl(e.target.value)}
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Website URL (optional)</label>
                  <input
                    type="text"
                    value={websiteUrl}
                    onChange={(e) => setWebsiteUrl(e.target.value)}
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
                <div className="flex items-center pt-6">
                  <label className="flex items-center gap-2 cursor-pointer select-none">
                    <input
                      type="checkbox"
                      checked={autoPublish}
                      onChange={(e) => setAutoPublish(e.target.checked)}
                      className="rounded border-border text-accent bg-bg focus:ring-0"
                    />
                    <span className="text-xs text-foreground font-semibold">Auto-Publish to Bluesky when ready</span>
                  </label>
                </div>
              </div>

              <div>
                <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Instruction Prompt for LLM Agent</label>
                <textarea
                  required
                  rows={4}
                  value={instructionPrompt}
                  onChange={(e) => setInstructionPrompt(e.target.value)}
                  placeholder="e.g. Summarize this article, highlighting the top 3 key takeaways with professional tone..."
                  className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent font-sans"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Include Filters (comma-separated keywords)</label>
                  <input
                    type="text"
                    value={includeFilters}
                    onChange={(e) => setIncludeFilters(e.target.value)}
                    placeholder="e.g. dotnet, csharp, ai"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Exclude Filters (comma-separated keywords)</label>
                  <input
                    type="text"
                    value={excludeFilters}
                    onChange={(e) => setExcludeFilters(e.target.value)}
                    placeholder="e.g. politics, drama"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>
              </div>

              <div className="flex gap-3 justify-end pt-2">
                <button
                  type="button"
                  onClick={() => setIsEditing(false)}
                  className="px-4 py-2 border border-border rounded-lg text-xs font-semibold hover:bg-zinc-800 transition text-zinc-300"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-accent text-bg text-xs font-bold rounded-lg shadow hover:opacity-90 transition"
                >
                  Save Subscription
                </button>
              </div>
            </form>
          </div>
        ) : null}

        {/* List Grid */}
        {loading ? (
          <div className="text-center py-12 text-xs font-mono text-muted">Loading subscriptions...</div>
        ) : feeds.length === 0 ? (
          <div className="text-center py-12 bg-panel border border-border rounded-xl">
            <p className="text-xs text-muted">No feed subscriptions found. Add your first feed subscription above!</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {feeds.map((feed) => (
              <div key={feed.id} className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-700">
                <div>
                  <div className="flex justify-between items-start mb-2">
                    <h3 className="text-sm font-bold text-foreground line-clamp-1">{feed.title}</h3>
                    {feed.autoPublish && (
                      <span className="text-[9px] bg-accent/20 text-accent font-semibold px-1.5 py-0.5 rounded uppercase tracking-wider">
                        Auto-Publish
                      </span>
                    )}
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

                <div className="flex gap-2 pt-4 border-t border-border shrink-0">
                  <button
                    onClick={() => handleTrigger(feed.id)}
                    disabled={triggeringId === feed.id}
                    className="flex-1 px-3 py-1.5 bg-accent/10 border border-accent/20 hover:bg-accent/20 disabled:opacity-50 text-accent text-xs font-bold rounded-lg transition"
                  >
                    {triggeringId === feed.id ? "Polling..." : "Trigger Poll"}
                  </button>
                  <button
                    onClick={() => handleEdit(feed)}
                    className="px-3 py-1.5 bg-zinc-800 hover:bg-zinc-700 border border-border text-zinc-300 text-xs font-semibold rounded-lg transition"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(feed.id)}
                    className="px-3 py-1.5 bg-red-950/20 hover:bg-red-950/40 border border-red-500/20 text-red-400 text-xs font-semibold rounded-lg transition"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
