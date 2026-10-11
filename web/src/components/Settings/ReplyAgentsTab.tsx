import { useState, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faComments,
  faPen,
  faTrashCan,
  faCheck,
  faRobot,
  faSliders,
  faLink,
} from "@fortawesome/free-solid-svg-icons";
import {
  type ReplyAgentDto,
  listReplyAgents,
  createReplyAgent,
  updateReplyAgent,
  deleteReplyAgent,
} from "../../api/replyAgents";
import { Modal } from "../common/Modal";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsEmptyState } from "./common/SettingsEmptyState";
import { SettingsAlert } from "./common/SettingsAlert";

export function ReplyAgentsTab() {
  const [agents, setAgents] = useState<ReplyAgentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isEnabled, setIsEnabled] = useState(true);
  const [personaPrompt, setPersonaPrompt] = useState("");
  const [minRankingScore, setMinRankingScore] = useState(7);
  const [maxRepliesPerBranch, setMaxRepliesPerBranch] = useState(2);
  const [maxRepliesPerUser, setMaxRepliesPerUser] = useState(3);
  const [maxThreadDepth, setMaxThreadDepth] = useState(4);
  const [maxCommentAgeHours, setMaxCommentAgeHours] = useState(24);
  const [maxPostAgeDays, setMaxPostAgeDays] = useState(7);
  const [autoPublishReplies, setAutoPublishReplies] = useState(true);
  const [checkIntervalMinutes, setCheckIntervalMinutes] = useState(15);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const fetchAgents = async () => {
    try {
      setLoading(true);
      const data = await listReplyAgents();
      setAgents(data);
    } catch (err: any) {
      setError(err.message || "Failed to load reply agents");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAgents();
  }, []);

  const handleStartAdd = () => {
    setEditingId(null);
    setName("");
    setDescription("");
    setIsEnabled(true);
    setPersonaPrompt("You are a helpful and polite social media assistant. Respond concisely to questions and feedback.");
    setMinRankingScore(7);
    setMaxRepliesPerBranch(2);
    setMaxRepliesPerUser(3);
    setMaxThreadDepth(4);
    setMaxCommentAgeHours(24);
    setMaxPostAgeDays(7);
    setAutoPublishReplies(true);
    setCheckIntervalMinutes(15);
    setModalError(null);
    setIsModalOpen(true);
  };

  const handleStartEdit = (agent: ReplyAgentDto) => {
    setEditingId(agent.id);
    setName(agent.name);
    setDescription(agent.description || "");
    setIsEnabled(agent.isEnabled);
    setPersonaPrompt(agent.personaPrompt);
    setMinRankingScore(agent.minRankingScore);
    setMaxRepliesPerBranch(agent.maxRepliesPerCommentThread);
    setMaxRepliesPerUser(agent.maxRepliesPerUser);
    setMaxThreadDepth(agent.maxThreadDepth);
    setMaxCommentAgeHours(agent.maxCommentAgeHours);
    setMaxPostAgeDays(agent.maxPostAgeDays);
    setAutoPublishReplies(agent.autoPublishReplies);
    setCheckIntervalMinutes(agent.checkIntervalMinutes);
    setModalError(null);
    setIsModalOpen(true);
  };

  const handleDelete = async (agent: ReplyAgentDto) => {
    const warning = agent.assignedWorkflowsCount > 0
      ? `This agent is assigned to ${agent.assignedWorkflowsCount} workflow(s). Deleting it will unassign it from all workflows. Are you sure?`
      : `Are you sure you want to delete reply agent "${agent.name}"?`;

    if (!window.confirm(warning)) return;

    try {
      await deleteReplyAgent(agent.id);
      setAgents((prev) => prev.filter((a) => a.id !== agent.id));
    } catch (err: any) {
      setError(err.message || "Failed to delete reply agent");
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setModalError("Agent name is required");
      return;
    }

    try {
      setIsSubmitting(true);
      setModalError(null);

      if (editingId) {
        const updated = await updateReplyAgent(editingId, {
          name: name.trim(),
          description: description.trim() || null,
          isEnabled,
          personaPrompt: personaPrompt.trim(),
          minRankingScore,
          maxRepliesPerCommentThread: maxRepliesPerBranch,
          maxRepliesPerUser,
          maxThreadDepth,
          maxCommentAgeHours,
          maxPostAgeDays,
          autoPublishReplies,
          checkIntervalMinutes,
        });
        setAgents((prev) => prev.map((a) => (a.id === editingId ? updated : a)));
      } else {
        const created = await createReplyAgent({
          name: name.trim(),
          description: description.trim() || null,
          isEnabled,
          personaPrompt: personaPrompt.trim(),
          minRankingScore,
          maxRepliesPerCommentThread: maxRepliesPerBranch,
          maxRepliesPerUser,
          maxThreadDepth,
          maxCommentAgeHours,
          maxPostAgeDays,
          autoPublishReplies,
          checkIntervalMinutes,
        });
        setAgents((prev) => [created, ...prev]);
      }

      setIsModalOpen(false);
    } catch (err: any) {
      setModalError(err.message || "Failed to save reply agent");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="space-y-6">
      {error && <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />}

      <SettingsSection
        title="Autonomous Reply Agents"
        description="Configure autonomous LLM agents that monitor and engage with comments across your automation workflows."
        action={
          <button
            onClick={handleStartAdd}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition-colors shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>New Reply Agent</span>
          </button>
        }
      >
        {loading ? (
          <div className="py-8 text-center text-xs font-mono text-muted animate-pulse">
            Loading reply agents...
          </div>
        ) : agents.length === 0 ? (
          <SettingsEmptyState
            icon={faRobot}
            title="No Reply Agents"
            description="Create reusable reply agents and assign them to one or more workflows to monitor incoming comments."
            actionLabel="Create Reply Agent"
            onAction={handleStartAdd}
          />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {agents.map((agent) => (
              <div
                key={agent.id}
                className="p-4 rounded-xl border border-border bg-panel/40 hover:bg-panel/70 transition-all flex flex-col justify-between group shadow-sm"
              >
                <div>
                  <div className="flex items-start justify-between gap-3 mb-2">
                    <div className="flex items-center gap-2">
                      <div className="w-7 h-7 rounded-lg bg-accent/15 flex items-center justify-center text-accent">
                        <FontAwesomeIcon icon={faRobot} className="w-3.5 h-3.5" />
                      </div>
                      <div>
                        <h4 className="text-sm font-semibold text-foreground tracking-tight">{agent.name}</h4>
                        <span className={`text-[10px] font-mono px-1.5 py-0.5 rounded ${
                          agent.isEnabled
                            ? "bg-emerald-950/40 text-emerald-400 border border-emerald-800/40"
                            : "bg-zinc-800/40 text-zinc-400 border border-zinc-700/40"
                        }`}>
                          {agent.isEnabled ? "Active" : "Disabled"}
                        </span>
                      </div>
                    </div>
                    <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                      <button
                        onClick={() => handleStartEdit(agent)}
                        className="p-1.5 text-muted hover:text-foreground hover:bg-bg rounded-md transition-colors"
                        title="Edit agent"
                      >
                        <FontAwesomeIcon icon={faPen} className="w-3 h-3" />
                      </button>
                      <button
                        onClick={() => handleDelete(agent)}
                        className="p-1.5 text-muted hover:text-red-400 hover:bg-red-950/20 rounded-md transition-colors"
                        title="Delete agent"
                      >
                        <FontAwesomeIcon icon={faTrashCan} className="w-3 h-3" />
                      </button>
                    </div>
                  </div>

                  {agent.description && (
                    <p className="text-xs text-muted/90 mb-3">{agent.description}</p>
                  )}

                  <div className="bg-bg/60 p-2.5 rounded-lg border border-border/50 text-[11px] font-mono text-muted mb-3 line-clamp-3">
                    {agent.personaPrompt}
                  </div>

                  <div className="flex flex-wrap gap-2 text-[10px] font-mono text-muted mb-3">
                    <span className="px-2 py-0.5 bg-bg border border-border rounded">
                      Score: ≥{agent.minRankingScore}/10
                    </span>
                    <span className="px-2 py-0.5 bg-bg border border-border rounded">
                      Max Replies: {agent.maxRepliesPerCommentThread}
                    </span>
                    <span className="px-2 py-0.5 bg-bg border border-border rounded">
                      Depth: ≤{agent.maxThreadDepth}
                    </span>
                    <span className="px-2 py-0.5 bg-bg border border-border rounded">
                      {agent.autoPublishReplies ? "Auto-Publish" : "Requires Approval"}
                    </span>
                  </div>
                </div>

                <div className="pt-3 border-t border-border/50 flex items-center justify-between text-[11px] font-mono text-muted">
                  <div className="flex items-center gap-1.5">
                    <FontAwesomeIcon icon={faLink} className="w-3 h-3 text-accent/70" />
                    <span>
                      {agent.assignedWorkflowsCount === 1
                        ? "1 workflow assigned"
                        : `${agent.assignedWorkflowsCount} workflows assigned`}
                    </span>
                  </div>
                  <span>Every {agent.checkIntervalMinutes}m</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </SettingsSection>

      <Modal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={editingId ? "Edit Reply Agent" : "Create Reply Agent"}
      >
        <form onSubmit={handleSubmit} className="space-y-4">
          {modalError && <SettingsAlert type="error" message={modalError} onDismiss={() => setModalError(null)} />}

          <div>
            <label className="block text-xs font-mono uppercase text-muted mb-1 font-semibold">Agent Name</label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Friendly Tech Assistant"
              className="w-full px-3 py-2 bg-bg border border-border rounded-lg text-sm text-foreground focus:outline-none focus:border-accent"
              required
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase text-muted mb-1 font-semibold">Description</label>
            <input
              type="text"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Brief summary of where and why this agent is used"
              className="w-full px-3 py-2 bg-bg border border-border rounded-lg text-sm text-foreground focus:outline-none focus:border-accent"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase text-muted mb-1 font-semibold">Persona & Tone Prompt</label>
            <textarea
              value={personaPrompt}
              onChange={(e) => setPersonaPrompt(e.target.value)}
              rows={4}
              placeholder="Instructions defining persona, style, guidelines, and guardrails..."
              className="w-full px-3 py-2 bg-bg border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:border-accent resize-none"
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Min Quality Score (1-10)</label>
              <input
                type="number"
                min={1}
                max={10}
                value={minRankingScore}
                onChange={(e) => setMinRankingScore(parseInt(e.target.value) || 7)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Max Replies / Branch</label>
              <input
                type="number"
                min={1}
                max={10}
                value={maxRepliesPerBranch}
                onChange={(e) => setMaxRepliesPerBranch(parseInt(e.target.value) || 2)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Max Replies / User</label>
              <input
                type="number"
                min={1}
                max={10}
                value={maxRepliesPerUser}
                onChange={(e) => setMaxRepliesPerUser(parseInt(e.target.value) || 3)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Max Thread Depth</label>
              <input
                type="number"
                min={1}
                max={10}
                value={maxThreadDepth}
                onChange={(e) => setMaxThreadDepth(parseInt(e.target.value) || 4)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Max Comment Age (Hours)</label>
              <input
                type="number"
                min={1}
                value={maxCommentAgeHours}
                onChange={(e) => setMaxCommentAgeHours(parseInt(e.target.value) || 24)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
            <div>
              <label className="block text-[11px] font-mono uppercase text-muted mb-1">Check Interval (Mins)</label>
              <input
                type="number"
                min={1}
                value={checkIntervalMinutes}
                onChange={(e) => setCheckIntervalMinutes(parseInt(e.target.value) || 15)}
                className="w-full px-3 py-1.5 bg-bg border border-border rounded-lg text-xs font-mono text-foreground"
              />
            </div>
          </div>

          <div className="space-y-2 pt-2 border-t border-border">
            <label className="flex items-center gap-2 text-xs font-mono cursor-pointer select-none">
              <input
                type="checkbox"
                checked={isEnabled}
                onChange={(e) => setIsEnabled(e.target.checked)}
                className="rounded border-border text-accent focus:ring-accent"
              />
              <span className="text-foreground">Enable this Reply Agent</span>
            </label>

            <label className="flex items-center gap-2 text-xs font-mono cursor-pointer select-none">
              <input
                type="checkbox"
                checked={autoPublishReplies}
                onChange={(e) => setAutoPublishReplies(e.target.checked)}
                className="rounded border-border text-accent focus:ring-accent"
              />
              <span className="text-foreground">Auto-publish evaluated replies (if false, replies require manual approval)</span>
            </label>
          </div>

          <div className="flex justify-end gap-2 pt-4 border-t border-border">
            <button
              type="button"
              onClick={() => setIsModalOpen(false)}
              className="px-4 py-2 border border-border rounded-lg text-xs font-mono text-muted hover:text-foreground hover:bg-bg transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting}
              className="px-4 py-2 bg-accent text-accent-foreground rounded-lg text-xs font-mono font-medium hover:bg-accent/90 transition-colors disabled:opacity-50"
            >
              {isSubmitting ? "Saving..." : editingId ? "Save Changes" : "Create Agent"}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
