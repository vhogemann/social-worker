import React, { useEffect, useState } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faClock,
  faEdit,
  faFileLines,
  faLayerGroup,
  faRss,
  faShareNodes,
  faSpinner,
  faTrashCan,
  faXmark
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchWorkflow,
  fetchWorkflowDrafts,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
  type AutomationWorkflowDto,
  type WorkflowDraftSummaryDto,
  type CreateAutomationWorkflowRequest
} from "../../api/workflows";
import { fetchFeeds, type FeedSubscriptionDto } from "../../api/feeds";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { WorkflowExecutionHistoryModal } from "./WorkflowExecutionHistoryModal";
import { useWorkflowStore } from "../../store/workflowStore";

export const WorkflowDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const setActiveWorkflow = useWorkflowStore((s) => s.setActiveWorkflow);

  const [workflow, setWorkflow] = useState<AutomationWorkflowDto | null>(null);
  const [drafts, setDrafts] = useState<WorkflowDraftSummaryDto[]>([]);
  const [availableFeeds, setAvailableFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [connectedAccounts, setConnectedAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [triggering, setTriggering] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);

  // Edit modal state
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [editTitle, setEditTitle] = useState("");
  const [editPrompt, setEditPrompt] = useState("");
  const [editFeedIds, setEditFeedIds] = useState<string[]>([]);
  const [editDestinations, setEditDestinations] = useState<
    { platform: string; autoPublish: boolean; accountId?: string; accountHandle?: string }[]
  >([]);

  const loadData = async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const [wf, draftList, feedList, accountsList] = await Promise.all([
        fetchWorkflow(id),
        fetchWorkflowDrafts(id),
        fetchFeeds().catch(() => [] as FeedSubscriptionDto[]),
        listAccounts().catch(() => [] as AccountDto[])
      ]);
      setWorkflow(wf);
      setActiveWorkflow(wf);
      setDrafts(draftList);
      setAvailableFeeds(feedList);
      setConnectedAccounts(accountsList);
    } catch (err: any) {
      setError(err?.message || "Failed to load workflow details.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
    return () => setActiveWorkflow(null);
  }, [id]);

  const handleOpenEditModal = () => {
    if (!workflow) return;
    setEditTitle(workflow.title);
    setEditPrompt(workflow.instructionPrompt || "");
    setEditFeedIds(workflow.sources.map((s) => s.feedSubscriptionId));
    setEditDestinations(
      workflow.destinations.map((d) => ({
        platform: d.platform,
        autoPublish: d.autoPublish,
        accountId: d.accountId ?? undefined,
        accountHandle: d.accountHandle ?? undefined
      }))
    );
    setIsEditModalOpen(true);
  };

  const handleToggleFeed = (feedId: string) => {
    setEditFeedIds((prev) =>
      prev.includes(feedId) ? prev.filter((fid) => fid !== feedId) : [...prev, feedId]
    );
  };

  const handleToggleDestination = (platform: string, accountId?: string, accountHandle?: string) => {
    setEditDestinations((prev) => {
      const exists = prev.some((d) => (accountId ? d.accountId === accountId : d.platform === platform));
      if (exists) {
        return prev.filter((d) => (accountId ? d.accountId !== accountId : d.platform !== platform));
      } else {
        return [...prev, { platform, autoPublish: false, accountId, accountHandle }];
      }
    });
  };

  const handleToggleAutoPublish = (platform: string, accountId?: string) => {
    setEditDestinations((prev) =>
      prev.map((d) => {
        const matches = accountId ? d.accountId === accountId : d.platform === platform;
        return matches ? { ...d, autoPublish: !d.autoPublish } : d;
      })
    );
  };

  const handleSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !editTitle.trim()) return;

    setError(null);
    const payload: CreateAutomationWorkflowRequest = {
      title: editTitle.trim(),
      instructionPrompt: editPrompt.trim() || undefined,
      feedSubscriptionIds: editFeedIds,
      destinations: editDestinations.map((d) => ({
        platform: d.platform,
        autoPublish: d.autoPublish,
        accountId: d.accountId
      }))
    };

    try {
      await updateWorkflow(id, payload);
      setIsEditModalOpen(false);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to update workflow.");
    }
  };

  const handleTrigger = async () => {
    if (!id) return;
    setTriggering(true);
    setError(null);
    try {
      await triggerWorkflow(id);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger workflow.");
    } finally {
      setTriggering(false);
    }
  };

  const handleDelete = async () => {
    if (!id) return;
    if (!window.confirm("Are you sure you want to delete this workflow?")) return;
    try {
      await deleteWorkflow(id);
      navigate("/");
    } catch (err: any) {
      setError(err?.message || "Failed to delete workflow.");
    }
  };

  if (loading && !workflow) {
    return (
      <div className="flex flex-col h-full bg-bg items-center justify-center text-muted">
        <FontAwesomeIcon icon={faSpinner} spin className="text-2xl" />
        <span className="text-xs font-mono mt-3">Loading workflow...</span>
      </div>
    );
  }

  if (!workflow) {
    return (
      <div className="flex flex-col h-full bg-bg items-center justify-center p-6 text-center">
        <h2 className="text-base font-bold text-foreground mb-2">Workflow not found</h2>
        <p className="text-xs text-muted mb-4">The workflow you requested does not exist or has been deleted.</p>
        <Link to="/" className="px-4 py-2 rounded-lg bg-accent text-accent-foreground text-xs font-semibold">
          Back to Workflows
        </Link>
      </div>
    );
  }

  const hasAutoPublish = workflow.destinations.some((d) => d.autoPublish);

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto" data-testid="workflow-detail-page">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Summary Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border pb-5">
            <div className="flex flex-col gap-2">
              <h1 className="text-xl font-bold text-foreground">{workflow.title}</h1>
              <div className="flex flex-wrap items-center gap-3 text-xs text-muted">
                <span className="inline-flex items-center gap-1 font-mono text-[11px]">
                  <FontAwesomeIcon icon={faRss} className="text-accent text-[10px]" />
                  <span>{workflow.sources.length} feed source{workflow.sources.length === 1 ? "" : "s"}</span>
                </span>
                <span className="text-border">·</span>
                <div className="flex flex-wrap items-center gap-1.5">
                  {workflow.destinations.length === 0 ? (
                    <span className="italic text-[11px]">Drafts only</span>
                  ) : (
                    workflow.destinations.map((dest) => (
                      <span
                        key={dest.id}
                        className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-panel border border-border text-[11px] text-foreground font-mono"
                      >
                        <FontAwesomeIcon icon={faShareNodes} className="text-[10px] text-accent" />
                        <span>{dest.platform}</span>
                        {dest.accountHandle && <span className="opacity-70">@{dest.accountHandle}</span>}
                      </span>
                    ))
                  )}
                </div>
                {hasAutoPublish && (
                  <>
                    <span className="text-border">·</span>
                    <span className="px-1.5 py-0.5 rounded bg-amber-500/10 border border-amber-500/20 text-amber-400 font-mono text-[10px] font-semibold">
                      Auto
                    </span>
                  </>
                )}
              </div>
            </div>

            <div className="flex items-center gap-2 shrink-0">
              <button
                onClick={handleTrigger}
                disabled={triggering}
                className="px-3 py-1.5 rounded-lg bg-panel border border-border hover:border-accent text-foreground text-xs font-mono flex items-center gap-1.5 transition disabled:opacity-50"
              >
                {triggering ? (
                  <FontAwesomeIcon icon={faSpinner} spin className="text-[10px]" />
                ) : (
                  <FontAwesomeIcon icon={faBolt} className="text-[10px] text-accent" />
                )}
                <span>Trigger Now</span>
              </button>

              <button
                onClick={() => setIsHistoryModalOpen(true)}
                className="px-3 py-1.5 rounded-lg bg-panel border border-border hover:border-accent text-muted hover:text-foreground text-xs font-mono flex items-center gap-1.5 transition"
              >
                <FontAwesomeIcon icon={faClock} />
                <span>Execution History</span>
              </button>

              <button
                onClick={handleOpenEditModal}
                className="p-2 rounded-lg border border-border hover:border-accent text-muted hover:text-foreground text-xs transition"
                title="Edit workflow"
              >
                <FontAwesomeIcon icon={faEdit} />
              </button>

              <button
                onClick={handleDelete}
                className="p-2 rounded-lg border border-border hover:bg-red-500/10 text-muted hover:text-red-400 text-xs transition"
                title="Delete workflow"
              >
                <FontAwesomeIcon icon={faTrashCan} />
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

        {/* Main Content: Generated Drafts Grid */}
        <div className="flex flex-col gap-4">
          <div className="flex justify-between items-center">
            <h2 className="text-sm font-bold text-foreground">
              Generated Drafts <span className="text-muted font-normal">({drafts.length})</span>
            </h2>
          </div>

          {drafts.length === 0 ? (
            <div className="text-center py-16 bg-panel border border-border rounded-xl flex flex-col items-center gap-3">
              <div className="p-3 rounded-full bg-bg border border-border text-muted">
                <FontAwesomeIcon icon={faFileLines} className="text-xl" />
              </div>
              <h3 className="text-sm font-semibold text-foreground">No drafts generated yet</h3>
              <p className="text-xs text-muted max-w-sm">
                When new feed items are ingested, generated canonical threads and platform adaptations will appear here.
              </p>
              <button
                onClick={handleTrigger}
                disabled={triggering}
                className="mt-2 px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition flex items-center gap-1.5"
              >
                <FontAwesomeIcon icon={faBolt} />
                <span>Trigger Ingestion Now</span>
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {drafts.map((draft) => (
                <div
                  key={draft.id}
                  onClick={() => navigate(`/draft/${draft.id}`)}
                  className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-600 hover:shadow-md cursor-pointer group"
                >
                  <div className="flex flex-col gap-3">
                    <div className="flex justify-between items-start gap-2">
                      <h3 className="text-sm font-bold text-foreground line-clamp-2 group-hover:text-accent transition">
                        {draft.title}
                      </h3>
                    </div>

                    {/* Platform Thread Badges */}
                    <div className="flex flex-wrap gap-1.5">
                      {draft.threads.length === 0 ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-mono bg-bg border border-border text-muted">
                          {draft.status}
                        </span>
                      ) : (
                        draft.threads.map((thread, idx) => (
                          <span
                            key={idx}
                            className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono font-medium ${
                              thread.stage === "Sent"
                                ? "bg-green-500/10 text-green-400 border border-green-500/20"
                                : "bg-bg text-foreground border border-border"
                            }`}
                          >
                            <span>{thread.platform}</span>
                            <span className="opacity-60">({thread.stage})</span>
                          </span>
                        ))
                      )}
                    </div>
                  </div>

                  <div className="flex justify-between items-center border-t border-border pt-3 mt-4 text-[10px] font-mono text-muted">
                    <span>{new Date(draft.createdAt).toLocaleDateString()}</span>
                    <span className="text-accent group-hover:underline">Open in Composer →</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Execution History Diagnostics Modal */}
        <WorkflowExecutionHistoryModal
          workflowId={workflow.id}
          workflowTitle={workflow.title}
          isOpen={isHistoryModalOpen}
          onClose={() => setIsHistoryModalOpen(false)}
        />

        {/* Edit Workflow Modal */}
        {isEditModalOpen && (
          <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div className="bg-panel border border-border rounded-xl max-w-xl w-full p-6 shadow-2xl relative flex flex-col gap-5">
              <div className="flex justify-between items-center border-b border-border pb-3">
                <h2 className="text-sm font-bold text-foreground">Edit Automation Workflow</h2>
                <button onClick={() => setIsEditModalOpen(false)} className="text-muted hover:text-foreground">
                  <FontAwesomeIcon icon={faXmark} />
                </button>
              </div>

              <form onSubmit={handleSaveEdit} className="flex flex-col gap-4">
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Workflow Title
                  </label>
                  <input
                    required
                    type="text"
                    value={editTitle}
                    onChange={(e) => setEditTitle(e.target.value)}
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
                    value={editPrompt}
                    onChange={(e) => setEditPrompt(e.target.value)}
                    placeholder="e.g. Draft a 3-part thread summarizing key points..."
                    className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent font-sans"
                  />
                </div>

                {/* Feed Sources Checklist */}
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Feed Sources ({editFeedIds.length}/{availableFeeds.length} selected)
                  </label>
                  {availableFeeds.length === 0 ? (
                    <div className="p-3 bg-bg border border-border rounded-lg text-xs text-muted">
                      No feed subscriptions available.{" "}
                      <Link to="/settings/feeds" className="text-accent underline">
                        Configure in Settings
                      </Link>
                      .
                    </div>
                  ) : (
                    <div className="max-h-36 overflow-y-auto bg-bg border border-border rounded-lg p-2 divide-y divide-border/50">
                      {availableFeeds.map((feed) => {
                        const isChecked = editFeedIds.includes(feed.id);
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

                {/* Destinations Selector */}
                <div>
                  <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
                    Publishing Destinations & Auto-Publish
                  </label>
                  <div className="space-y-2 bg-bg border border-border rounded-lg p-3">
                    {connectedAccounts.length > 0 ? (
                      connectedAccounts.map((acc) => {
                        const currentDest = editDestinations.find((d) => d.accountId === acc.id);
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
                            checked={editDestinations.some((d) => d.platform === "Bluesky")}
                            onChange={() => handleToggleDestination("Bluesky")}
                            className="rounded border-border text-accent bg-bg focus:ring-0"
                          />
                          <span className="text-xs text-foreground font-semibold">Bluesky</span>
                        </label>
                        {editDestinations.some((d) => d.platform === "Bluesky") && (
                          <label className="flex items-center gap-2 cursor-pointer select-none text-xs">
                            <input
                              type="checkbox"
                              checked={
                                editDestinations.find((d) => d.platform === "Bluesky")?.autoPublish ?? false
                              }
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
                    onClick={() => setIsEditModalOpen(false)}
                    className="px-4 py-2 rounded-lg border border-border hover:bg-bg text-xs text-muted hover:text-foreground transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-4 py-2 rounded-lg bg-accent text-accent-foreground font-semibold text-xs hover:opacity-90 transition shadow"
                  >
                    Save Changes
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
