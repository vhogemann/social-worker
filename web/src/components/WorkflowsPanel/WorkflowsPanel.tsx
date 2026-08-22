import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faCircleCheck,
  faEdit,
  faLayerGroup,
  faPenNib,
  faPlus,
  faRotateRight,
  faRss,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchWorkflows,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
  type AutomationWorkflowDto,
  type CreateAutomationWorkflowRequest,
  type CreateWorkflowDestinationRequest
} from "../../api/workflows";
import { fetchFeeds, type FeedSubscriptionDto } from "../../api/feeds";
import { listAccounts, type AccountDto } from "../../api/accounts";

export const WorkflowsPanel: React.FC = () => {
  const [workflows, setWorkflows] = useState<AutomationWorkflowDto[]>([]);
  const [availableFeeds, setAvailableFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [connectedAccounts, setConnectedAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);

  // Modal form state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [title, setTitle] = useState("");
  const [instructionPrompt, setInstructionPrompt] = useState("Summarize this article as an engaging thread.");
  const [selectedFeedIds, setSelectedFeedIds] = useState<string[]>([]);
  const [destinations, setDestinations] = useState<{ platform: string; autoPublish: boolean; accountId?: string; accountHandle?: string }[]>([]);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [wfList, feedList, accountsList] = await Promise.all([
        fetchWorkflows(),
        fetchFeeds(),
        listAccounts().catch(() => [] as AccountDto[])
      ]);
      setWorkflows(wfList);
      setAvailableFeeds(feedList);
      setConnectedAccounts(accountsList);
    } catch (err: any) {
      setError(err?.message || "Failed to load automation workflows.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  const handleOpenCreateModal = () => {
    setEditingId(null);
    setTitle("");
    setInstructionPrompt("Summarize this article as an engaging thread.");
    setSelectedFeedIds(availableFeeds.map((f) => f.id)); // default select all feeds
    
    // Default destinations for connected accounts
    if (connectedAccounts.length > 0) {
      setDestinations(connectedAccounts.map((a) => ({ platform: a.platform, autoPublish: false, accountId: a.id, accountHandle: a.handle })));
    } else {
      setDestinations([{ platform: "Bluesky", autoPublish: false }]);
    }
    
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (wf: AutomationWorkflowDto) => {
    setEditingId(wf.id);
    setTitle(wf.title);
    setInstructionPrompt(wf.instructionPrompt || "");
    setSelectedFeedIds(wf.sources.map((s) => s.feedSubscriptionId));
    setDestinations(wf.destinations.map((d) => ({
      platform: d.platform,
      autoPublish: d.autoPublish,
      accountId: d.accountId ?? undefined,
      accountHandle: d.accountHandle ?? undefined
    })));
    setIsModalOpen(true);
  };

  const handleToggleFeed = (feedId: string) => {
    setSelectedFeedIds((prev) =>
      prev.includes(feedId) ? prev.filter((id) => id !== feedId) : [...prev, feedId]
    );
  };

  const handleToggleDestination = (platform: string, accountId?: string, accountHandle?: string) => {
    setDestinations((prev) => {
      const exists = prev.some((d) => accountId ? d.accountId === accountId : d.platform === platform);
      if (exists) {
        return prev.filter((d) => accountId ? d.accountId !== accountId : d.platform !== platform);
      } else {
        return [...prev, { platform, autoPublish: false, accountId, accountHandle }];
      }
    });
  };

  const handleToggleAutoPublish = (platform: string, accountId?: string) => {
    setDestinations((prev) =>
      prev.map((d) => {
        const matches = accountId ? d.accountId === accountId : d.platform === platform;
        return matches ? { ...d, autoPublish: !d.autoPublish } : d;
      })
    );
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;

    setError(null);
    const payload: CreateAutomationWorkflowRequest = {
      title: title.trim(),
      instructionPrompt: instructionPrompt.trim() || undefined,
      feedSubscriptionIds: selectedFeedIds,
      destinations: destinations.map((d) => ({ platform: d.platform, autoPublish: d.autoPublish, accountId: d.accountId }))
    };

    try {
      if (editingId) {
        await updateWorkflow(editingId, payload);
      } else {
        await createWorkflow(payload);
      }
      setIsModalOpen(false);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to save automation workflow.");
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this workflow?")) return;
    try {
      await deleteWorkflow(id);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to delete workflow.");
    }
  };

  const handleTrigger = async (id: string) => {
    setTriggeringId(id);
    setError(null);
    try {
      await triggerWorkflow(id);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger workflow execution.");
    } finally {
      setTriggeringId(null);
    }
  };

  const activeAccountPlatforms = Array.from(
    new Set(["Bluesky", ...connectedAccounts.map((a) => a.platform)])
  );

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto" data-testid="workflows-dashboard">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Top Header */}
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div>
            <h1 className="text-lg font-bold text-foreground">Automation Workflows</h1>
            <p className="text-[11px] text-muted font-mono">
              Autonomous multi-source ingestion, 2-tier generation, proofreader validation, and multi-platform publishing
            </p>
          </div>
          <div className="flex gap-2">
            <Link
              to="/composer"
              className="px-3 py-1.5 rounded-lg border border-border bg-panel hover:bg-bg text-muted hover:text-foreground text-xs font-mono flex items-center gap-1.5 transition"
            >
              <FontAwesomeIcon icon={faPenNib} />
              <span>Ad-hoc Composer</span>
            </Link>
            <button
              onClick={handleOpenCreateModal}
              className="px-3 py-1.5 rounded-lg bg-accent text-accent-foreground font-semibold text-xs transition hover:opacity-90 flex items-center gap-1.5 shadow"
            >
              <FontAwesomeIcon icon={faPlus} />
              <span>New Workflow</span>
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

        {/* Workflows Grid */}
        {loading && workflows.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : workflows.length === 0 ? (
          <div className="text-center py-16 bg-panel border border-border rounded-xl flex flex-col items-center gap-3">
            <div className="p-3 rounded-full bg-bg border border-border text-muted">
              <FontAwesomeIcon icon={faLayerGroup} className="text-xl" />
            </div>
            <h3 className="text-sm font-semibold text-foreground">No automation workflows yet</h3>
            <p className="text-xs text-muted max-w-sm">
              Create a workflow to connect RSS/YouTube feeds to automated 2-tier thread generation and multi-platform publishing.
            </p>
            <button
              onClick={handleOpenCreateModal}
              className="mt-2 px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition"
            >
              Create First Workflow
            </button>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {workflows.map((wf) => (
              <div
                key={wf.id}
                className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-700 shadow-sm"
              >
                <div className="flex flex-col gap-3">
                  <div className="flex justify-between items-start">
                    <h3 className="text-sm font-bold text-foreground line-clamp-1">{wf.title}</h3>
                  </div>

                  <p className="text-xs text-muted font-sans line-clamp-2 bg-bg/50 p-2.5 rounded-lg border border-border/50">
                    "{wf.instructionPrompt || "Summarize this article as a thread."}"
                  </p>

                  {/* Sources Section */}
                  <div>
                    <span className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1.5">
                      Ingestion Sources ({wf.sources.length})
                    </span>
                    {wf.sources.length === 0 ? (
                      <span className="text-[11px] text-muted italic">No linked feeds</span>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        {wf.sources.map((src) => (
                          <span
                            key={src.feedSubscriptionId}
                            className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-bg border border-border text-[11px] text-foreground font-mono"
                          >
                            <FontAwesomeIcon icon={faRss} className="text-[9px] text-accent" />
                            <span className="line-clamp-1">{src.title}</span>
                          </span>
                        ))}
                      </div>
                    )}
                  </div>

                  {/* Destinations Section */}
                  <div>
                    <span className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1.5">
                      Target Destinations ({wf.destinations.length})
                    </span>
                    {wf.destinations.length === 0 ? (
                      <span className="text-[11px] text-muted italic">Drafts only (no publishing)</span>
                    ) : (
                      <div className="flex flex-wrap gap-1.5">
                        {wf.destinations.map((dest) => (
                          <span
                            key={dest.id}
                            className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-bg border border-border text-[11px] text-foreground font-mono"
                          >
                            <span>{dest.platform}</span>
                            {dest.accountHandle && <span className="opacity-75 font-mono text-[10px]">@{dest.accountHandle}</span>}
                            {dest.autoPublish && (
                              <span className="text-[8px] bg-accent/20 text-accent font-semibold px-1 py-0.2 rounded uppercase">
                                Auto-Publish
                              </span>
                            )}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex justify-between items-center border-t border-border pt-4 mt-4">
                  <button
                    onClick={() => handleTrigger(wf.id)}
                    disabled={triggeringId === wf.id}
                    className="px-3 py-1 rounded-lg bg-panel border border-border hover:border-accent text-foreground text-xs font-mono flex items-center gap-1.5 transition disabled:opacity-50"
                  >
                    {triggeringId === wf.id ? (
                      <FontAwesomeIcon icon={faSpinner} spin className="text-[10px]" />
                    ) : (
                      <FontAwesomeIcon icon={faBolt} className="text-[10px] text-accent" />
                    )}
                    <span>Trigger Now</span>
                  </button>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => handleOpenEditModal(wf)}
                      className="px-2 py-1 rounded border border-border hover:border-accent text-muted hover:text-foreground text-xs transition"
                      title="Edit workflow"
                    >
                      <FontAwesomeIcon icon={faEdit} />
                    </button>
                    <button
                      onClick={() => handleDelete(wf.id)}
                      className="px-2 py-1 rounded border border-border hover:bg-red-500/10 text-muted hover:text-red-400 text-xs transition"
                      title="Delete workflow"
                    >
                      <FontAwesomeIcon icon={faTrashCan} />
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Create / Edit Workflow Modal */}
        {isModalOpen && (
          <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-panel border border-border rounded-xl max-w-xl w-full p-6 shadow-2xl relative flex flex-col gap-5">
              <div className="flex justify-between items-center border-b border-border pb-3">
                <h2 className="text-sm font-bold text-foreground">
                  {editingId ? "Edit Automation Workflow" : "Create Automation Workflow"}
                </h2>
                <button onClick={() => setIsModalOpen(false)} className="text-muted hover:text-foreground">
                  <FontAwesomeIcon icon={faXmark} />
                </button>
              </div>

              <form onSubmit={handleSave} className="flex flex-col gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Workflow Title
                  </label>
                  <input
                    required
                    type="text"
                    value={title}
                    onChange={(e) => setTitle(e.target.value)}
                    placeholder="e.g. Daily Tech Highlights"
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    LLM Instruction Prompt
                  </label>
                  <textarea
                    required
                    rows={3}
                    value={instructionPrompt}
                    onChange={(e) => setInstructionPrompt(e.target.value)}
                    placeholder="e.g. Draft a 3-part thread summarizing the key takeaways with code snippets..."
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent font-sans"
                  />
                </div>

                {/* Feed Sources Checklist */}
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Feed Sources to Monitor ({selectedFeedIds.length}/{availableFeeds.length} selected)
                  </label>
                  {availableFeeds.length === 0 ? (
                    <div className="p-3 bg-bg border border-border rounded-lg text-xs text-muted">
                      No feed subscriptions found.{" "}
                      <Link to="/feeds" className="text-accent underline">
                        Add a feed first
                      </Link>
                      .
                    </div>
                  ) : (
                    <div className="max-h-36 overflow-y-auto bg-bg border border-border rounded-lg p-2 divide-y divide-border/50">
                      {availableFeeds.map((feed) => {
                        const isChecked = selectedFeedIds.includes(feed.id);
                        return (
                          <label
                            key={feed.id}
                            className="flex items-center gap-2.5 p-1.5 hover:bg-panel rounded cursor-pointer transition select-none"
                          >
                            <input
                              type="checkbox"
                              checked={isChecked}
                              onChange={() => handleToggleFeed(feed.id)}
                              className="rounded border-border text-accent bg-panel focus:ring-0"
                            />
                            <div className="flex-1 min-w-0">
                              <p className="text-xs text-foreground font-medium line-clamp-1">{feed.title}</p>
                              <p className="text-[10px] text-muted font-mono truncate">{feed.feedUrl}</p>
                            </div>
                          </label>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* Destination Platforms Selector */}
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Publishing Destinations & Auto-Publish
                  </label>
                  <div className="space-y-2 bg-bg border border-border rounded-lg p-3">
                    {connectedAccounts.length > 0 ? (
                      connectedAccounts.map((acc) => {
                        const currentDest = destinations.find((d) => d.accountId === acc.id);
                        const isEnabled = !!currentDest;
                        const isAutoPublish = currentDest?.autoPublish ?? false;

                        return (
                          <div
                            key={acc.id}
                            className="flex justify-between items-center p-2 rounded-lg bg-panel border border-border/70"
                          >
                            <label className="flex items-center gap-2 cursor-pointer select-none">
                              <input
                                type="checkbox"
                                checked={isEnabled}
                                onChange={() => handleToggleDestination(acc.platform, acc.id, acc.handle)}
                                className="rounded border-border text-accent bg-bg focus:ring-0"
                              />
                              <span className="text-xs text-foreground font-semibold">{acc.platform}</span>
                              <span className="text-[10px] text-muted font-mono">@{acc.handle}</span>
                            </label>

                            {isEnabled && (
                              <label className="flex items-center gap-2 cursor-pointer select-none text-xs">
                                <input
                                  type="checkbox"
                                  checked={isAutoPublish}
                                  onChange={() => handleToggleAutoPublish(acc.platform, acc.id)}
                                  className="rounded border-border text-accent bg-bg focus:ring-0"
                                />
                                <span className="text-[11px] text-muted font-mono">Auto-Publish</span>
                              </label>
                            )}
                          </div>
                        );
                      })
                    ) : (
                      <div
                        key="Bluesky"
                        className="flex justify-between items-center p-2 rounded-lg bg-panel border border-border/70"
                      >
                        <label className="flex items-center gap-2 cursor-pointer select-none">
                          <input
                            type="checkbox"
                            checked={destinations.some((d) => d.platform === "Bluesky")}
                            onChange={() => handleToggleDestination("Bluesky")}
                            className="rounded border-border text-accent bg-bg focus:ring-0"
                          />
                          <span className="text-xs text-foreground font-semibold">Bluesky</span>
                        </label>
                        {destinations.some((d) => d.platform === "Bluesky") && (
                          <label className="flex items-center gap-2 cursor-pointer select-none text-xs">
                            <input
                              type="checkbox"
                              checked={destinations.find((d) => d.platform === "Bluesky")?.autoPublish ?? false}
                              onChange={() => handleToggleAutoPublish("Bluesky")}
                              className="rounded border-border text-accent bg-bg focus:ring-0"
                            />
                            <span className="text-[11px] text-muted font-mono">Auto-Publish</span>
                          </label>
                        )}
                      </div>
                    )}
                  </div>
                </div>

                <div className="flex justify-end gap-2 border-t border-border pt-4 mt-2">
                  <button
                    type="button"
                    onClick={() => setIsModalOpen(false)}
                    className="px-4 py-2 rounded-lg border border-border hover:bg-bg text-xs text-muted hover:text-foreground transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition shadow"
                  >
                    {editingId ? "Save Changes" : "Create Workflow"}
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
