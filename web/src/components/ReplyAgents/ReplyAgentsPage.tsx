import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faRobot,
  faPlus,
  faArrowRotateRight,
  faComments,
  faPen,
  faTrashCan,
  faCheck,
  faSliders,
  faExternalLink,
  faClock,
  faFilter,
  faPaperPlane,
} from "@fortawesome/free-solid-svg-icons";
import {
  type ReplyAgentDto,
  type ReplyAgentAssignedPostDto,
  type WorkflowCommentAuditDto,
  listReplyAgents,
  getAllMonitoredPosts,
  getReplyAgentAudits,
  approveReplyAgentAudit,
  pollReplyAgentPost,
  unassignReplyAgentPost,
  createReplyAgent,
  updateReplyAgent,
  deleteReplyAgent,
} from "../../api/replyAgents";
import { Modal } from "../common/Modal";
import { MonitorPostModal } from "./MonitorPostModal";

type ActiveTab = "posts" | "agents" | "inbox" | "audits";

export function ReplyAgentsPage() {
  const [activeTab, setActiveTab] = useState<ActiveTab>("posts");

  const [agents, setAgents] = useState<ReplyAgentDto[]>([]);
  const [posts, setPosts] = useState<ReplyAgentAssignedPostDto[]>([]);
  const [audits, setAudits] = useState<WorkflowCommentAuditDto[]>([]);
  const [pendingAudits, setPendingAudits] = useState<WorkflowCommentAuditDto[]>([]);

  const [loading, setLoading] = useState(false);
  const [pollingPostId, setPollingPostId] = useState<string | null>(null);
  const [approvingAuditId, setApprovingAuditId] = useState<string | null>(null);
  const [replyTextDrafts, setReplyTextDrafts] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  // Monitor Post Modal
  const [isMonitorModalOpen, setIsMonitorModalOpen] = useState(false);

  // Create / Edit Agent Modal
  const [isAgentModalOpen, setIsAgentModalOpen] = useState(false);
  const [editingAgentId, setEditingAgentId] = useState<string | null>(null);
  const [agentName, setAgentName] = useState("");
  const [agentDescription, setAgentDescription] = useState("");
  const [agentIsEnabled, setAgentIsEnabled] = useState(true);
  const [agentPersona, setAgentPersona] = useState("");
  const [agentMinScore, setAgentMinScore] = useState(7);
  const [agentMaxRepliesBranch, setAgentMaxRepliesBranch] = useState(2);
  const [agentMaxRepliesUser, setAgentMaxRepliesUser] = useState(3);
  const [agentMaxDepth, setAgentMaxDepth] = useState(4);
  const [agentMaxCommentAge, setAgentMaxCommentAge] = useState(24);
  const [agentMaxPostAge, setAgentMaxPostAge] = useState(7);
  const [agentAutoPublish, setAgentAutoPublish] = useState(true);
  const [agentInterval, setAgentInterval] = useState(15);
  const [agentSubmitting, setAgentSubmitting] = useState(false);
  const [agentModalError, setAgentModalError] = useState<string | null>(null);

  // Audits filter
  const [auditDecisionFilter, setAuditDecisionFilter] = useState<string>("all");

  const loadData = async () => {
    try {
      setLoading(true);
      setError(null);
      const [agentsData, postsData, pendingData, allAuditsData] = await Promise.all([
        listReplyAgents(),
        getAllMonitoredPosts(),
        getReplyAgentAudits({ decision: "PendingApproval", limit: 50 }),
        getReplyAgentAudits({ limit: 100 }),
      ]);
      setAgents(agentsData);
      setPosts(postsData);
      setPendingAudits(pendingData);
      setAudits(allAuditsData);
    } catch (err: any) {
      setError(err.message || "Failed to load reply agents dashboard");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handlePollPost = async (agentId: string, postId: string) => {
    try {
      setPollingPostId(postId);
      await pollReplyAgentPost(agentId, postId);
      const updatedPosts = await getAllMonitoredPosts();
      setPosts(updatedPosts);
    } catch (err: any) {
      setError(err.message || "Failed to trigger poll for post");
    } finally {
      setPollingPostId(null);
    }
  };

  const handleUnassignPost = async (agentId: string, postId: string) => {
    if (!window.confirm("Stop monitoring this post?")) return;
    try {
      await unassignReplyAgentPost(agentId, postId);
      setPosts((prev) => prev.filter((p) => !(p.replyAgentId === agentId && p.postId === postId)));
    } catch (err: any) {
      setError(err.message || "Failed to unassign post");
    }
  };

  const handleApproveReply = async (audit: WorkflowCommentAuditDto) => {
    try {
      setApprovingAuditId(audit.id);
      const customReply = replyTextDrafts[audit.id] ?? audit.generatedReplyText ?? undefined;
      await approveReplyAgentAudit(audit.id, customReply);
      setPendingAudits((prev) => prev.filter((a) => a.id !== audit.id));
      const freshAudits = await getReplyAgentAudits({ limit: 100 });
      setAudits(freshAudits);
    } catch (err: any) {
      setError(err.message || "Failed to approve reply");
    } finally {
      setApprovingAuditId(null);
    }
  };

  const handleOpenAddAgent = () => {
    setEditingAgentId(null);
    setAgentName("");
    setAgentDescription("");
    setAgentIsEnabled(true);
    setAgentPersona("You are a helpful and polite social media assistant. Respond concisely to questions and feedback.");
    setAgentMinScore(7);
    setAgentMaxRepliesBranch(2);
    setAgentMaxRepliesUser(3);
    setAgentMaxDepth(4);
    setAgentMaxCommentAge(24);
    setAgentMaxPostAge(7);
    setAgentAutoPublish(true);
    setAgentInterval(15);
    setAgentModalError(null);
    setIsAgentModalOpen(true);
  };

  const handleOpenEditAgent = (agent: ReplyAgentDto) => {
    setEditingAgentId(agent.id);
    setAgentName(agent.name);
    setAgentDescription(agent.description || "");
    setAgentIsEnabled(agent.isEnabled);
    setAgentPersona(agent.personaPrompt);
    setAgentMinScore(agent.minRankingScore);
    setAgentMaxRepliesBranch(agent.maxRepliesPerCommentThread);
    setAgentMaxRepliesUser(agent.maxRepliesPerUser);
    setAgentMaxDepth(agent.maxThreadDepth);
    setAgentMaxCommentAge(agent.maxCommentAgeHours);
    setAgentMaxPostAge(agent.maxPostAgeDays);
    setAgentAutoPublish(agent.autoPublishReplies);
    setAgentInterval(agent.checkIntervalMinutes);
    setAgentModalError(null);
    setIsAgentModalOpen(true);
  };

  const handleSaveAgent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!agentName.trim()) {
      setAgentModalError("Agent name is required.");
      return;
    }

    try {
      setAgentSubmitting(true);
      setAgentModalError(null);

      if (editingAgentId) {
        const updated = await updateReplyAgent(editingAgentId, {
          name: agentName.trim(),
          description: agentDescription.trim() || null,
          isEnabled: agentIsEnabled,
          personaPrompt: agentPersona.trim(),
          minRankingScore: agentMinScore,
          maxRepliesPerCommentThread: agentMaxRepliesBranch,
          maxRepliesPerUser: agentMaxRepliesUser,
          maxThreadDepth: agentMaxDepth,
          maxCommentAgeHours: agentMaxCommentAge,
          maxPostAgeDays: agentMaxPostAge,
          autoPublishReplies: agentAutoPublish,
          checkIntervalMinutes: agentInterval,
        });
        setAgents((prev) => prev.map((a) => (a.id === updated.id ? updated : a)));
      } else {
        const created = await createReplyAgent({
          name: agentName.trim(),
          description: agentDescription.trim() || null,
          isEnabled: agentIsEnabled,
          personaPrompt: agentPersona.trim(),
          minRankingScore: agentMinScore,
          maxRepliesPerCommentThread: agentMaxRepliesBranch,
          maxRepliesPerUser: agentMaxRepliesUser,
          maxThreadDepth: agentMaxDepth,
          maxCommentAgeHours: agentMaxCommentAge,
          maxPostAgeDays: agentMaxPostAge,
          autoPublishReplies: agentAutoPublish,
          checkIntervalMinutes: agentInterval,
        });
        setAgents((prev) => [created, ...prev]);
      }

      setIsAgentModalOpen(false);
    } catch (err: any) {
      setAgentModalError(err.message || "Failed to save reply agent");
    } finally {
      setAgentSubmitting(false);
    }
  };

  const handleDeleteAgent = async (agent: ReplyAgentDto) => {
    if (!window.confirm(`Are you sure you want to delete reply agent '${agent.name}'?`)) return;
    try {
      await deleteReplyAgent(agent.id);
      setAgents((prev) => prev.filter((a) => a.id !== agent.id));
      setPosts((prev) => prev.filter((p) => p.replyAgentId !== agent.id));
    } catch (err: any) {
      setError(err.message || "Failed to delete agent");
    }
  };

  const filteredAudits = audits.filter((a) => {
    if (auditDecisionFilter === "all") return true;
    return a.rankingDecision === auditDecisionFilter;
  });

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-bg text-foreground overflow-hidden font-sans">
      {/* Top Header */}
      <header className="px-6 py-4 border-b border-border bg-panel shrink-0 flex items-center justify-between gap-4">
        <div>
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-accent/15 flex items-center justify-center text-accent">
              <FontAwesomeIcon icon={faRobot} className="w-4 h-4" />
            </div>
            <h1 className="text-lg font-bold tracking-tight">Reply Agents</h1>
          </div>
          <p className="text-xs text-muted font-mono mt-0.5">
            Monitor, rank, and respond to comments on organic and workflow posts automatically.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={() => setIsMonitorModalOpen(true)}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-accent/40 bg-accent/10 text-accent text-xs font-mono font-medium hover:bg-accent/20 transition-colors shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>Monitor Post</span>
          </button>
          <button
            onClick={handleOpenAddAgent}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition-colors shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>New Reply Agent</span>
          </button>
        </div>
      </header>

      {/* Navigation Sub-Tabs */}
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
          onClick={() => setActiveTab("inbox")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "inbox"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Approval Inbox</span>
          {pendingAudits.length > 0 && (
            <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-amber-500/20 text-amber-400 font-bold">
              {pendingAudits.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab("agents")}
          className={`py-3 border-b-2 font-medium transition-colors flex items-center gap-2 ${
            activeTab === "agents"
              ? "border-accent text-accent"
              : "border-transparent text-muted hover:text-foreground"
          }`}
        >
          <span>Agents Library</span>
          <span className="px-1.5 py-0.2 rounded-full text-[10px] bg-border/60">
            {agents.length}
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
          <span>Activity Stream</span>
        </button>

        <div className="ml-auto py-2">
          <button
            onClick={loadData}
            disabled={loading}
            className="text-muted hover:text-foreground transition p-1.5 rounded-lg hover:bg-border/20 text-xs flex items-center gap-1.5"
            title="Refresh dashboard"
          >
            <FontAwesomeIcon icon={faArrowRotateRight} className={`w-3 h-3 ${loading ? "animate-spin" : ""}`} />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      {error && (
        <div className="mx-6 mt-4 p-3 rounded-lg text-xs font-mono bg-destructive/10 text-destructive border border-destructive/20 flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-muted hover:text-foreground">✕</button>
        </div>
      )}

      {/* Main Content Area */}
      <main className="flex-1 overflow-y-auto p-6 min-h-0">
        {/* TAB 1: MONITORED POSTS */}
        {activeTab === "posts" && (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-semibold">Active Monitored Posts</h3>
                <p className="text-xs text-muted font-mono">
                  Posts receiving periodic comment checks. Organic viral posts and workflow threads.
                </p>
              </div>
            </div>

            {loading && posts.length === 0 ? (
              <div className="py-12 text-center text-xs font-mono text-muted animate-pulse">
                Loading monitored posts...
              </div>
            ) : posts.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <FontAwesomeIcon icon={faComments} className="w-8 h-8 text-muted/40 mb-3" />
                <h4 className="text-sm font-semibold mb-1">No Posts Monitored Yet</h4>
                <p className="text-xs text-muted font-mono max-w-sm mx-auto mb-4">
                  Add external posts or organic viral tweets/threads to delegate incoming comment replies.
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
                      <th className="py-2.5 px-4">Assigned Agent</th>
                      <th className="py-2.5 px-4">Origin</th>
                      <th className="py-2.5 px-4">Comments / Replies</th>
                      <th className="py-2.5 px-4">Last Polled</th>
                      <th className="py-2.5 px-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/60">
                    {posts.map((post) => (
                      <tr key={`${post.replyAgentId}-${post.postId}`} className="hover:bg-panel/80 transition-colors">
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
                          <Link
                            to={`/reply-agents/${post.replyAgentId}`}
                            className="font-medium text-foreground hover:text-accent hover:underline flex items-center gap-1.5"
                          >
                            <FontAwesomeIcon icon={faRobot} className="w-3 h-3 text-muted" />
                            <span>{post.agentName}</span>
                          </Link>
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
                          <div className="flex items-center gap-2">
                            <span className="text-foreground font-semibold">{post.commentsAuditedCount}</span>
                            <span className="text-muted">audited</span>
                            <span className="text-muted font-bold">/</span>
                            <span className="text-emerald-400 font-semibold">{post.repliesSentCount}</span>
                            <span className="text-muted">replied</span>
                          </div>
                        </td>
                        <td className="py-3 px-4 text-muted text-[11px]">
                          {post.lastPolledAt ? new Date(post.lastPolledAt).toLocaleString() : "Never polled"}
                        </td>
                        <td className="py-3 px-4 text-right">
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              onClick={() => handlePollPost(post.replyAgentId, post.postId)}
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
                              onClick={() => handleUnassignPost(post.replyAgentId, post.postId)}
                              className="p-1.5 rounded-lg border border-border text-muted hover:text-destructive hover:bg-destructive/10 transition"
                              title="Stop monitoring post"
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

        {/* TAB 2: APPROVAL INBOX */}
        {activeTab === "inbox" && (
          <div className="space-y-4">
            <div>
              <h3 className="text-sm font-semibold">Approval Inbox</h3>
              <p className="text-xs text-muted font-mono">
                Comments evaluated by agents requiring manual approval before publishing.
              </p>
            </div>

            {pendingAudits.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <FontAwesomeIcon icon={faCheck} className="w-8 h-8 text-emerald-400/60 mb-3" />
                <h4 className="text-sm font-semibold mb-1">Inbox Zero</h4>
                <p className="text-xs text-muted font-mono max-w-sm mx-auto">
                  All comments have either been automatically replied to or skipped.
                </p>
              </div>
            ) : (
              <div className="space-y-4">
                {pendingAudits.map((audit) => (
                  <div
                    key={audit.id}
                    className="p-4 rounded-xl border border-border bg-panel/60 shadow-sm space-y-3"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex items-center gap-2">
                        <span className="px-1.5 py-0.5 rounded text-[10px] font-mono bg-accent/15 text-accent font-semibold">
                          {audit.platform}
                        </span>
                        <span className="font-semibold text-xs">@{audit.remoteAuthorHandle.replace(/^@/, "")}</span>
                        <span className="text-muted text-[11px] font-mono">
                          {new Date(audit.commentCreatedAt).toLocaleString()}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-amber-950/40 text-amber-400 border border-amber-800/40">
                          Score {audit.rankingScore}/10 • {audit.rankingCategory}
                        </span>
                      </div>
                    </div>

                    <div className="p-3 rounded-lg bg-bg/80 border border-border/60 text-xs">
                      {audit.commentText}
                    </div>

                    {audit.rankingReasoning && (
                      <div className="text-[11px] font-mono text-muted italic">
                        Reasoning: {audit.rankingReasoning}
                      </div>
                    )}

                    <div className="pt-2 border-t border-border/40">
                      <label className="block text-xs font-mono font-medium text-foreground mb-1">
                        Proposed Reply
                      </label>
                      <textarea
                        rows={2}
                        value={replyTextDrafts[audit.id] ?? audit.generatedReplyText ?? ""}
                        onChange={(e) =>
                          setReplyTextDrafts({ ...replyTextDrafts, [audit.id]: e.target.value })
                        }
                        className="w-full bg-input border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
                      />
                      <div className="flex justify-end gap-2 mt-2">
                        <button
                          onClick={() => handleApproveReply(audit)}
                          disabled={approvingAuditId === audit.id}
                          className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-mono font-medium hover:bg-emerald-500 transition shadow-sm disabled:opacity-50"
                        >
                          <FontAwesomeIcon icon={faPaperPlane} className="w-3 h-3" />
                          <span>{approvingAuditId === audit.id ? "Sending..." : "Approve & Send"}</span>
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB 3: AGENTS LIBRARY */}
        {activeTab === "agents" && (
          <div className="space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-sm font-semibold">Autonomous Reply Agents</h3>
                <p className="text-xs text-muted font-mono">
                  Configured agents with their persona prompts, threshold scoring, and assignments.
                </p>
              </div>
            </div>

            {agents.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <FontAwesomeIcon icon={faRobot} className="w-8 h-8 text-muted/40 mb-3" />
                <h4 className="text-sm font-semibold mb-1">No Reply Agents Configured</h4>
                <p className="text-xs text-muted font-mono max-w-sm mx-auto mb-4">
                  Create autonomous agents to analyze and participate in comment sections.
                </p>
                <button
                  onClick={handleOpenAddAgent}
                  className="px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition shadow-sm"
                >
                  <FontAwesomeIcon icon={faPlus} className="w-3 h-3 mr-1.5" />
                  Create Agent
                </button>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {agents.map((agent) => (
                  <div
                    key={agent.id}
                    className="p-4 rounded-xl border border-border bg-panel/40 hover:bg-panel/70 transition flex flex-col justify-between group shadow-sm"
                  >
                    <div>
                      <div className="flex items-start justify-between gap-3 mb-2">
                        <div className="flex items-center gap-2">
                          <div className="w-8 h-8 rounded-lg bg-accent/15 flex items-center justify-center text-accent">
                            <FontAwesomeIcon icon={faRobot} className="w-4 h-4" />
                          </div>
                          <div>
                            <Link
                              to={`/reply-agents/${agent.id}`}
                              className="text-sm font-semibold text-foreground hover:text-accent hover:underline"
                            >
                              {agent.name}
                            </Link>
                            <div>
                              <span
                                className={`text-[10px] font-mono px-1.5 py-0.5 rounded ${
                                  agent.isEnabled
                                    ? "bg-emerald-950/40 text-emerald-400 border border-emerald-800/40"
                                    : "bg-zinc-800/40 text-zinc-400 border border-zinc-700/40"
                                }`}
                              >
                                {agent.isEnabled ? "Active" : "Disabled"}
                              </span>
                            </div>
                          </div>
                        </div>

                        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                          <button
                            onClick={() => handleOpenEditAgent(agent)}
                            className="p-1.5 rounded text-muted hover:text-foreground hover:bg-border/30 transition"
                            title="Edit Agent"
                          >
                            <FontAwesomeIcon icon={faPen} className="w-3 h-3" />
                          </button>
                          <button
                            onClick={() => handleDeleteAgent(agent)}
                            className="p-1.5 rounded text-muted hover:text-destructive hover:bg-destructive/10 transition"
                            title="Delete Agent"
                          >
                            <FontAwesomeIcon icon={faTrashCan} className="w-3 h-3" />
                          </button>
                        </div>
                      </div>

                      {agent.description && (
                        <p className="text-xs text-muted mb-3 line-clamp-2">{agent.description}</p>
                      )}

                      <div className="p-2.5 rounded-lg bg-bg/60 border border-border/40 text-[11px] font-mono text-muted mb-3 line-clamp-2 italic">
                        "{agent.personaPrompt}"
                      </div>
                    </div>

                    <div className="pt-3 border-t border-border/60 text-[11px] font-mono space-y-1.5">
                      <div className="flex items-center justify-between text-muted">
                        <span>Min Ranking Score:</span>
                        <span className="text-foreground font-semibold">{agent.minRankingScore}/10</span>
                      </div>
                      <div className="flex items-center justify-between text-muted">
                        <span>Monitored Posts:</span>
                        <span className="text-foreground font-semibold">{agent.assignedPostsCount}</span>
                      </div>
                      <div className="flex items-center justify-between text-muted">
                        <span>Workflows Assigned:</span>
                        <span className="text-foreground font-semibold">{agent.assignedWorkflowsCount}</span>
                      </div>
                      <div className="flex items-center justify-between text-muted">
                        <span>Auto-publish:</span>
                        <span className={agent.autoPublishReplies ? "text-emerald-400" : "text-amber-400"}>
                          {agent.autoPublishReplies ? "Enabled" : "Manual Approval"}
                        </span>
                      </div>

                      <div className="pt-2">
                        <Link
                          to={`/reply-agents/${agent.id}`}
                          className="block text-center py-1.5 rounded bg-border/40 hover:bg-border/70 text-foreground text-xs font-semibold transition"
                        >
                          Manage Agent
                        </Link>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB 4: ACTIVITY STREAM / AUDITS */}
        {activeTab === "audits" && (
          <div className="space-y-4">
            <div className="flex items-center justify-between gap-4">
              <div>
                <h3 className="text-sm font-semibold">Comment Evaluation Activity</h3>
                <p className="text-xs text-muted font-mono">
                  Full log of inspected comments, classification categories, and LLM reasoning.
                </p>
              </div>

              <div className="flex items-center gap-2 text-xs font-mono">
                <FontAwesomeIcon icon={faFilter} className="w-3 h-3 text-muted" />
                <select
                  value={auditDecisionFilter}
                  onChange={(e) => setAuditDecisionFilter(e.target.value)}
                  className="bg-input border border-border rounded-lg px-2.5 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
                >
                  <option value="all">All Decisions</option>
                  <option value="Replied">Replied</option>
                  <option value="PendingApproval">Pending Approval</option>
                  <option value="SkippedBelowThreshold">Skipped (Low Score)</option>
                  <option value="SkippedStopCriteria">Skipped (Depth/User limit)</option>
                  <option value="Failed">Failed</option>
                </select>
              </div>
            </div>

            {filteredAudits.length === 0 ? (
              <div className="p-8 text-center rounded-xl border border-dashed border-border bg-panel/30">
                <p className="text-xs text-muted font-mono">No audit events match the selected filter.</p>
              </div>
            ) : (
              <div className="space-y-2">
                {filteredAudits.map((audit) => (
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
        agents={agents}
        onAssigned={(newPost) => {
          setPosts((prev) => [newPost, ...prev]);
        }}
      />

      {/* Create / Edit Agent Modal */}
      <Modal
        isOpen={isAgentModalOpen}
        onClose={() => setIsAgentModalOpen(false)}
        title={editingAgentId ? "Edit Reply Agent" : "Create Reply Agent"}
        description="Configure an autonomous persona and safety guardrails for comment evaluation and replies."
        maxWidth="lg"
        footer={
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setIsAgentModalOpen(false)}
              className="px-3 py-1.5 rounded-lg border border-border text-xs font-mono text-muted hover:text-foreground transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              form="reply-agent-form"
              disabled={agentSubmitting || !agentName.trim()}
              className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition-colors disabled:opacity-50"
            >
              <FontAwesomeIcon icon={faCheck} className="w-3 h-3" />
              <span>{agentSubmitting ? "Saving..." : editingAgentId ? "Save Changes" : "Create Agent"}</span>
            </button>
          </div>
        }
      >
        <form id="reply-agent-form" onSubmit={handleSaveAgent} className="space-y-4">
          {agentModalError && (
            <div className="p-3 text-xs font-mono bg-destructive/10 text-destructive border border-destructive/20 rounded-lg">
              {agentModalError}
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-mono text-foreground font-medium mb-1">
                Agent Name <span className="text-destructive">*</span>
              </label>
              <input
                type="text"
                required
                value={agentName}
                onChange={(e) => setAgentName(e.target.value)}
                placeholder="e.g. Viral Engagement Bot"
                className="w-full bg-input border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
              />
            </div>

            <div>
              <label className="block text-xs font-mono text-foreground font-medium mb-1">Status</label>
              <div className="flex items-center gap-3 h-9">
                <label className="flex items-center gap-2 cursor-pointer text-xs font-mono">
                  <input
                    type="checkbox"
                    checked={agentIsEnabled}
                    onChange={(e) => setAgentIsEnabled(e.target.checked)}
                    className="rounded border-border text-accent focus:ring-accent"
                  />
                  <span>Enable Agent</span>
                </label>
              </div>
            </div>
          </div>

          <div>
            <label className="block text-xs font-mono text-foreground font-medium mb-1">Description</label>
            <input
              type="text"
              value={agentDescription}
              onChange={(e) => setAgentDescription(e.target.value)}
              placeholder="e.g. Engages thoughtfully with positive comments on technical posts"
              className="w-full bg-input border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>

          <div>
            <label className="block text-xs font-mono text-foreground font-medium mb-1">
              Persona & Guidance Prompt <span className="text-destructive">*</span>
            </label>
            <textarea
              required
              rows={3}
              value={agentPersona}
              onChange={(e) => setAgentPersona(e.target.value)}
              placeholder="Define voice, tone, and specific instructions for replies..."
              className="w-full bg-input border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>

          <div className="border-t border-border pt-4">
            <h4 className="text-xs font-mono font-semibold text-foreground mb-3 flex items-center gap-2">
              <FontAwesomeIcon icon={faSliders} className="w-3 h-3 text-accent" />
              <span>Guardrails & Stop Criteria</span>
            </h4>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs font-mono">
              <div>
                <label className="block text-muted mb-1 text-[11px]">Min Ranking Score (1-10)</label>
                <input
                  type="number"
                  min={1}
                  max={10}
                  value={agentMinScore}
                  onChange={(e) => setAgentMinScore(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>

              <div>
                <label className="block text-muted mb-1 text-[11px]">Max Replies / Thread</label>
                <input
                  type="number"
                  min={1}
                  value={agentMaxRepliesBranch}
                  onChange={(e) => setAgentMaxRepliesBranch(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>

              <div>
                <label className="block text-muted mb-1 text-[11px]">Max Replies / User</label>
                <input
                  type="number"
                  min={1}
                  value={agentMaxRepliesUser}
                  onChange={(e) => setAgentMaxRepliesUser(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>

              <div>
                <label className="block text-muted mb-1 text-[11px]">Max Thread Depth</label>
                <input
                  type="number"
                  min={1}
                  value={agentMaxDepth}
                  onChange={(e) => setAgentMaxDepth(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs font-mono mt-3">
              <div>
                <label className="block text-muted mb-1 text-[11px]">Max Comment Age (Hours)</label>
                <input
                  type="number"
                  min={1}
                  value={agentMaxCommentAge}
                  onChange={(e) => setAgentMaxCommentAge(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>

              <div>
                <label className="block text-muted mb-1 text-[11px]">Max Post Age (Days)</label>
                <input
                  type="number"
                  min={1}
                  value={agentMaxPostAge}
                  onChange={(e) => setAgentMaxPostAge(parseInt(e.target.value) || 1)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>

              <div>
                <label className="block text-muted mb-1 text-[11px]">Check Interval (Mins)</label>
                <input
                  type="number"
                  min={1}
                  value={agentInterval}
                  onChange={(e) => setAgentInterval(parseInt(e.target.value) || 5)}
                  className="w-full bg-input border border-border rounded-lg px-2.5 py-1.5 text-foreground"
                />
              </div>
            </div>

            <div className="mt-3">
              <label className="flex items-center gap-2 cursor-pointer text-xs font-mono">
                <input
                  type="checkbox"
                  checked={agentAutoPublish}
                  onChange={(e) => setAgentAutoPublish(e.target.checked)}
                  className="rounded border-border text-accent focus:ring-accent"
                />
                <span>Auto-publish replies (if disabled, replies are queued in Approval Inbox)</span>
              </label>
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
