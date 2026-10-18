import React, { useState, useEffect } from "react";
import { useParams, Link, useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faRobot,
  faArrowLeft,
  faPlus,
  faArrowRotateRight,
  faCheck,
  faTrashCan,
  faSliders,
  faExternalLink,
  faComments,
  faDiagramProject,
} from "@fortawesome/free-solid-svg-icons";
import {
  type ReplyAgentDto,
  type ReplyAgentAssignedPostDto,
  type WorkflowCommentAuditDto,
  getReplyAgent,
  updateReplyAgent,
  deleteReplyAgent,
  getReplyAgentPosts,
  unassignReplyAgentPost,
  pollReplyAgentPost,
  getReplyAgentAudits,
} from "../../api/replyAgents";
import { MonitorPostModal } from "./MonitorPostModal";

type DetailTab = "posts" | "settings" | "workflows" | "audits";

export function ReplyAgentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [agent, setAgent] = useState<ReplyAgentDto | null>(null);
  const [posts, setPosts] = useState<ReplyAgentAssignedPostDto[]>([]);
  const [audits, setAudits] = useState<WorkflowCommentAuditDto[]>([]);
  const [activeTab, setActiveTab] = useState<DetailTab>("posts");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isMonitorModalOpen, setIsMonitorModalOpen] = useState(false);
  const [pollingPostId, setPollingPostId] = useState<string | null>(null);

  // Form states
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);
  const [personaPrompt, setPersonaPrompt] = useState("");
  const [minRankingScore, setMinRankingScore] = useState(7);
  const [maxRepliesBranch, setMaxRepliesBranch] = useState(2);
  const [maxRepliesUser, setMaxRepliesUser] = useState(3);
  const [maxThreadDepth, setMaxThreadDepth] = useState(4);
  const [maxCommentAgeHours, setMaxCommentAgeHours] = useState(24);
  const [maxPostAgeDays, setMaxPostAgeDays] = useState(7);
  const [autoPublishReplies, setAutoPublishReplies] = useState(true);
  const [checkIntervalMinutes, setCheckIntervalMinutes] = useState(15);
  const [savingSettings, setSavingSettings] = useState(false);
  const [saveSuccess, setSaveSuccess] = useState(false);

  const loadAgentData = async () => {
    if (!id) return;
    try {
      setLoading(true);
      setError(null);
      const [agentData, postsData, auditsData] = await Promise.all([
        getReplyAgent(id),
        getReplyAgentPosts(id),
        getReplyAgentAudits({ agentId: id, limit: 50 }),
      ]);
      setAgent(agentData);
      setPosts(postsData);
      setAudits(auditsData);

      setName(agentData.name);
      setDescription(agentData.description || "");
      setIsEnabled(agentData.isEnabled);
      setPersonaPrompt(agentData.personaPrompt);
      setMinRankingScore(agentData.minRankingScore);
      setMaxRepliesBranch(agentData.maxRepliesPerCommentThread);
      setMaxRepliesUser(agentData.maxRepliesPerUser);
      setMaxThreadDepth(agentData.maxThreadDepth);
      setMaxCommentAgeHours(agentData.maxCommentAgeHours);
      setMaxPostAgeDays(agentData.maxPostAgeDays);
      setAutoPublishReplies(agentData.autoPublishReplies);
      setCheckIntervalMinutes(agentData.checkIntervalMinutes);
    } catch (err: any) {
      setError(err.message || "Failed to load reply agent details");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAgentData();
  }, [id]);

  const handleSaveSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !name.trim()) return;

    try {
      setSavingSettings(true);
      setError(null);
      setSaveSuccess(false);

      const updated = await updateReplyAgent(id, {
        name: name.trim(),
        description: description.trim() || null,
        isEnabled,
        personaPrompt: personaPrompt.trim(),
        minRankingScore,
        maxRepliesPerCommentThread: maxRepliesBranch,
        maxRepliesPerUser: maxRepliesUser,
        maxThreadDepth,
        maxCommentAgeHours,
        maxPostAgeDays,
        autoPublishReplies,
        checkIntervalMinutes,
      });

      setAgent(updated);
      setSaveSuccess(true);
      setTimeout(() => setSaveSuccess(false), 3000);
    } catch (err: any) {
      setError(err.message || "Failed to save agent settings");
    } finally {
      setSavingSettings(false);
    }
  };

  const handlePollPost = async (postId: string) => {
    if (!id) return;
    try {
      setPollingPostId(postId);
      await pollReplyAgentPost(id, postId);
      const updatedPosts = await getReplyAgentPosts(id);
      setPosts(updatedPosts);
    } catch (err: any) {
      setError(err.message || "Failed to trigger poll");
    } finally {
      setPollingPostId(null);
    }
  };

  const handleUnassignPost = async (postId: string) => {
    if (!id || !window.confirm("Stop monitoring this post?")) return;
    try {
      await unassignReplyAgentPost(id, postId);
      setPosts((prev) => prev.filter((p) => p.postId !== postId));
    } catch (err: any) {
      setError(err.message || "Failed to unassign post");
    }
  };

  const handleDeleteAgent = async () => {
    if (!id || !agent) return;
    if (!window.confirm(`Are you sure you want to delete reply agent '${agent.name}'?`)) return;

    try {
      await deleteReplyAgent(id);
      navigate("/reply-agents");
    } catch (err: any) {
      setError(err.message || "Failed to delete agent");
    }
  };

  if (loading) {
    return (
      <div className="flex-1 flex items-center justify-center p-8 text-xs font-mono text-muted animate-pulse">
        Loading agent details...
      </div>
    );
  }

  if (!agent) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-8 text-center">
        <h3 className="text-sm font-semibold mb-2">Reply Agent Not Found</h3>
        <Link to="/reply-agents" className="text-xs font-mono text-accent hover:underline">
          Return to Reply Agents
        </Link>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-bg text-foreground overflow-hidden font-sans">
      {/* Top Header */}
      <header className="px-6 py-4 border-b border-border bg-panel shrink-0 flex items-center justify-between gap-4">
        <div className="flex items-center gap-3 min-w-0">
          <Link
            to="/reply-agents"
            className="p-1.5 rounded-lg border border-border text-muted hover:text-foreground hover:bg-border/20 transition"
            title="Back to Reply Agents"
          >
            <FontAwesomeIcon icon={faArrowLeft} className="w-3 h-3" />
          </Link>
          <div className="w-8 h-8 rounded-lg bg-accent/15 flex items-center justify-center text-accent shrink-0">
            <FontAwesomeIcon icon={faRobot} className="w-4 h-4" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h1 className="text-base font-bold tracking-tight truncate">{agent.name}</h1>
              <span
                className={`text-[10px] font-mono px-1.5 py-0.2 rounded ${
                  agent.isEnabled
                    ? "bg-emerald-950/40 text-emerald-400 border border-emerald-800/40"
                    : "bg-zinc-800/40 text-zinc-400 border border-zinc-700/40"
                }`}
              >
                {agent.isEnabled ? "Active" : "Disabled"}
              </span>
            </div>
            {agent.description && (
              <p className="text-xs text-muted font-mono truncate mt-0.5">{agent.description}</p>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2 shrink-0">
          <button
            onClick={() => setIsMonitorModalOpen(true)}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-accent/40 bg-accent/10 text-accent text-xs font-mono font-medium hover:bg-accent/20 transition shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>Monitor Post</span>
          </button>
          <button
            onClick={handleDeleteAgent}
            className="p-1.5 rounded-lg border border-border text-muted hover:text-destructive hover:bg-destructive/10 transition"
            title="Delete Agent"
          >
            <FontAwesomeIcon icon={faTrashCan} className="w-3 h-3" />
          </button>
        </div>
      </header>

      {/* Sub-Tabs */}
      <div className="px-6 border-b border-border bg-panel/40 flex items-center gap-6 shrink-0 text-xs font-mono">
        <button
          onClick={() => setActiveTab("posts")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "posts"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Monitored Posts</span>
          <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-border/60">
            {posts.length}
          </span>
        </button>

        <button
          onClick={() => setActiveTab("settings")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "settings"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Persona & Guardrails</span>
        </button>

        <button
          onClick={() => setActiveTab("workflows")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "workflows"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Workflows</span>
          <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-border/60">
            {agent.workflows.length}
          </span>
        </button>

        <button
          onClick={() => setActiveTab("audits")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "audits"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Agent Audits</span>
        </button>
      </div>

      {error && (
        <div className="mx-6 mt-4 p-3 rounded-lg text-xs font-mono bg-destructive/10 text-destructive border border-destructive/20 flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-muted hover:text-foreground">✕</button>
        </div>
      )}

      {/* Content */}
      <main className="flex-1 overflow-y-auto p-6 min-h-0">
        {/* TAB 1: MONITORED POSTS */}
        {activeTab === "posts" && (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-semibold">Posts Monitored by {agent.name}</h3>
                <p className="text-xs text-muted font-mono">
                  Incoming replies on these posts are evaluated against this agent's persona.
                </p>
              </div>
            </div>

            {posts.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <FontAwesomeIcon icon={faComments} className="w-8 h-8 text-muted/40 mb-3" />
                <h4 className="text-sm font-semibold mb-1">No Posts Assigned</h4>
                <p className="text-xs text-muted font-mono max-w-sm mx-auto mb-4">
                  Assign viral tweets, threads, or Bluesky posts to monitor comments.
                </p>
                <button
                  onClick={() => setIsMonitorModalOpen(true)}
                  className="px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition shadow-sm"
                >
                  <FontAwesomeIcon icon={faPlus} className="w-3 h-3 mr-1.5" />
                  Monitor Post
                </button>
              </div>
            ) : (
              <div className="border border-border rounded-xl bg-panel/40 overflow-hidden shadow-sm">
                <table className="w-full text-left text-xs font-mono">
                  <thead className="bg-panel/80 border-b border-border text-muted uppercase text-[10px]">
                    <tr>
                      <th className="py-2.5 px-4">Post & Platform</th>
                      <th className="py-2.5 px-4">Origin</th>
                      <th className="py-2.5 px-4">Audited / Replied</th>
                      <th className="py-2.5 px-4">Last Polled</th>
                      <th className="py-2.5 px-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/60">
                    {posts.map((post) => (
                      <tr key={post.postId} className="hover:bg-panel/80 transition-colors">
                        <td className="py-3 px-4 max-w-md">
                          <div className="flex items-center gap-2 mb-1">
                            <span className="px-1.5 py-0.5 rounded text-[10px] bg-accent/15 text-accent font-semibold">
                              {post.platform}
                            </span>
                            {post.authorHandle && (
                              <span className="text-muted text-[11px]">@{post.authorHandle.replace(/^@/, "")}</span>
                            )}
                          </div>
                          <div className="text-foreground text-xs line-clamp-2">
                            {post.publishedText || post.remoteId || "Post content unavailable"}
                          </div>
                          {post.url && (
                            <a
                              href={post.url}
                              target="_blank"
                              rel="noreferrer"
                              className="text-[11px] text-accent/80 hover:text-accent hover:underline flex items-center gap-1 mt-1"
                            >
                              <span>View platform post</span>
                              <FontAwesomeIcon icon={faExternalLink} className="w-2.5 h-2.5" />
                            </a>
                          )}
                        </td>
                        <td className="py-3 px-4">
                          <span
                            className={`px-1.5 py-0.5 rounded text-[10px] font-mono ${
                              post.isExternal
                                ? "bg-amber-950/40 text-amber-400 border border-amber-800/40"
                                : "bg-blue-950/40 text-blue-400 border border-blue-800/40"
                            }`}
                          >
                            {post.isExternal ? "Organic / External" : "Workflow Draft"}
                          </span>
                        </td>
                        <td className="py-3 px-4">
                          <span className="text-foreground font-semibold">{post.commentsAuditedCount}</span> audited /{" "}
                          <span className="text-emerald-400 font-semibold">{post.repliesSentCount}</span> replied
                        </td>
                        <td className="py-3 px-4 text-muted text-[11px]">
                          {post.lastPolledAt ? new Date(post.lastPolledAt).toLocaleString() : "Never polled"}
                        </td>
                        <td className="py-3 px-4 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              onClick={() => handlePollPost(post.postId)}
                              disabled={pollingPostId === post.postId}
                              className="p-1.5 rounded-lg border border-border text-muted hover:text-foreground hover:bg-border/20 transition disabled:opacity-50"
                              title="Check comments now"
                            >
                              <FontAwesomeIcon
                                icon={faArrowRotateRight}
                                className={`w-3 h-3 ${pollingPostId === post.postId ? "animate-spin text-accent" : ""}`}
                              />
                            </button>
                            <button
                              onClick={() => handleUnassignPost(post.postId)}
                              className="p-1.5 rounded-lg border border-border text-muted hover:text-destructive hover:bg-destructive/10 transition"
                              title="Stop monitoring"
                            >
                              <FontAwesomeIcon icon={faTrashCan} className="w-3 h-3" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* TAB 2: SETTINGS */}
        {activeTab === "settings" && (
          <form onSubmit={handleSaveSettings} className="max-w-2xl space-y-6">
            <div className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-mono text-foreground font-medium mb-1">Agent Name</label>
                  <input
                    type="text"
                    required
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
                  />
                </div>

                <div>
                  <label className="block text-xs font-mono text-foreground font-medium mb-1">Status</label>
                  <div className="flex items-center gap-3 h-9">
                    <label className="flex items-center gap-2 cursor-pointer text-xs font-mono">
                      <input
                        type="checkbox"
                        checked={isEnabled}
                        onChange={(e) => setIsEnabled(e.target.checked)}
                        className="rounded border-border text-accent focus:ring-accent"
                      />
                      <span>Active</span>
                    </label>
                  </div>
                </div>
              </div>

              <div>
                <label className="block text-xs font-mono text-foreground font-medium mb-1">Description</label>
                <input
                  type="text"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
                />
              </div>

              <div>
                <label className="block text-xs font-mono text-foreground font-medium mb-1">Persona & Tone Prompt</label>
                <textarea
                  required
                  rows={4}
                  value={personaPrompt}
                  onChange={(e) => setPersonaPrompt(e.target.value)}
                  className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
                />
              </div>

              <div className="border-t border-border pt-4">
                <h4 className="text-xs font-mono font-semibold text-foreground mb-3 flex items-center gap-2">
                  <FontAwesomeIcon icon={faSliders} className="w-3 h-3 text-accent" />
                  <span>Evaluation & Limits</span>
                </h4>

                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs font-mono">
                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Min Ranking Score (1-10)</label>
                    <input
                      type="number"
                      min={1}
                      max={10}
                      value={minRankingScore}
                      onChange={(e) => setMinRankingScore(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>

                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Max Replies / Thread</label>
                    <input
                      type="number"
                      min={1}
                      value={maxRepliesBranch}
                      onChange={(e) => setMaxRepliesBranch(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>

                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Max Replies / User</label>
                    <input
                      type="number"
                      min={1}
                      value={maxRepliesUser}
                      onChange={(e) => setMaxRepliesUser(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>

                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Max Thread Depth</label>
                    <input
                      type="number"
                      min={1}
                      value={maxThreadDepth}
                      onChange={(e) => setMaxThreadDepth(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs font-mono mt-3">
                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Max Comment Age (Hours)</label>
                    <input
                      type="number"
                      min={1}
                      value={maxCommentAgeHours}
                      onChange={(e) => setMaxCommentAgeHours(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>

                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Max Post Age (Days)</label>
                    <input
                      type="number"
                      min={1}
                      value={maxPostAgeDays}
                      onChange={(e) => setMaxPostAgeDays(parseInt(e.target.value) || 1)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>

                  <div>
                    <label className="block text-muted mb-1 text-[11px]">Check Interval (Mins)</label>
                    <input
                      type="number"
                      min={1}
                      value={checkIntervalMinutes}
                      onChange={(e) => setCheckIntervalMinutes(parseInt(e.target.value) || 5)}
                      className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                    />
                  </div>
                </div>

                <div className="mt-4">
                  <label className="flex items-center gap-2 cursor-pointer text-xs font-mono">
                    <input
                      type="checkbox"
                      checked={autoPublishReplies}
                      onChange={(e) => setAutoPublishReplies(e.target.checked)}
                      className="rounded border-border text-accent focus:ring-accent"
                    />
                    <span>Auto-publish replies immediately</span>
                  </label>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-3 pt-4 border-t border-border">
              <button
                type="submit"
                disabled={savingSettings || !name.trim()}
                className="flex items-center gap-2 px-4 py-2 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition shadow-sm disabled:opacity-50"
              >
                <FontAwesomeIcon icon={faCheck} className="w-3 h-3" />
                <span>{savingSettings ? "Saving..." : "Save Settings"}</span>
              </button>
              {saveSuccess && (
                <span className="text-xs font-mono text-emerald-400">Settings saved successfully!</span>
              )}
            </div>
          </form>
        )}

        {/* TAB 3: WORKFLOWS */}
        {activeTab === "workflows" && (
          <div className="space-y-4 max-w-2xl">
            <div>
              <h3 className="text-sm font-semibold">Assigned Automation Workflows</h3>
              <p className="text-xs text-muted font-mono">
                Workflows currently utilizing this reply agent for automatic post checks.
              </p>
            </div>

            {agent.workflows.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <FontAwesomeIcon icon={faDiagramProject} className="w-8 h-8 text-muted/40 mb-3" />
                <h4 className="text-sm font-semibold mb-1">No Workflows Assigned</h4>
                <p className="text-xs text-muted font-mono max-w-sm mx-auto">
                  Assign this agent inside any Automation Workflow settings tab.
                </p>
              </div>
            ) : (
              <div className="divide-y divide-border border border-border rounded-xl bg-panel/40 overflow-hidden">
                {agent.workflows.map((wf) => (
                  <div key={wf.id} className="p-3 flex items-center justify-between hover:bg-panel/70 transition">
                    <div className="flex items-center gap-3">
                      <FontAwesomeIcon icon={faDiagramProject} className="w-4 h-4 text-accent" />
                      <span className="text-xs font-medium text-foreground">{wf.title}</span>
                    </div>
                    <Link
                      to={`/workflows/${wf.id}`}
                      className="text-xs font-mono text-accent hover:underline flex items-center gap-1"
                    >
                      <span>Open Workflow</span>
                      <FontAwesomeIcon icon={faExternalLink} className="w-2.5 h-2.5" />
                    </Link>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB 4: AUDITS */}
        {activeTab === "audits" && (
          <div className="space-y-4">
            <div>
              <h3 className="text-sm font-semibold">Agent Evaluation Log</h3>
              <p className="text-xs text-muted font-mono">
                Recent comments processed by {agent.name}.
              </p>
            </div>

            {audits.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30 text-xs font-mono text-muted">
                No comment evaluations logged yet for this agent.
              </div>
            ) : (
              <div className="space-y-2">
                {audits.map((audit) => (
                  <div
                    key={audit.id}
                    className="p-3 rounded-lg border border-border bg-panel/30 flex items-start justify-between gap-4 text-xs font-mono"
                  >
                    <div className="space-y-1 max-w-2xl">
                      <div className="flex items-center gap-2">
                        <span className="px-1.5 py-0.2 rounded text-[10px] bg-accent/15 text-accent font-semibold">
                          {audit.platform}
                        </span>
                        <span className="font-semibold text-foreground">@{audit.remoteAuthorHandle.replace(/^@/, "")}</span>
                        <span className="text-muted text-[10px]">
                          {new Date(audit.commentCreatedAt).toLocaleString()}
                        </span>
                      </div>
                      <div className="text-foreground text-xs line-clamp-2">
                        "{audit.commentText}"
                      </div>
                      {audit.generatedReplyText && (
                        <div className="text-emerald-400 text-[11px] line-clamp-2">
                          Reply: "{audit.generatedReplyText}"
                        </div>
                      )}
                      {audit.rankingReasoning && (
                        <div className="text-muted text-[10px] italic">
                          {audit.rankingReasoning}
                        </div>
                      )}
                    </div>

                    <div className="text-right shrink-0">
                      <span
                        className={`inline-block px-2 py-0.5 rounded text-[10px] font-semibold ${
                          audit.rankingDecision === "Replied"
                            ? "bg-emerald-950/50 text-emerald-400 border border-emerald-800/50"
                            : audit.rankingDecision === "PendingApproval"
                            ? "bg-amber-950/50 text-amber-400 border border-amber-800/50"
                            : "bg-zinc-800/50 text-zinc-400 border border-zinc-700/50"
                        }`}
                      >
                        {audit.rankingDecision}
                      </span>
                      <div className="text-[10px] text-muted mt-1">
                        Score {audit.rankingScore}/10 • {audit.rankingCategory}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </main>

      {/* Monitor Post Modal */}
      <MonitorPostModal
        isOpen={isMonitorModalOpen}
        onClose={() => setIsMonitorModalOpen(false)}
        agents={[agent]}
        defaultAgentId={agent.id}
        onAssigned={(newPost) => {
          setPosts((prev) => [newPost, ...prev]);
        }}
      />
    </div>
  );
}
