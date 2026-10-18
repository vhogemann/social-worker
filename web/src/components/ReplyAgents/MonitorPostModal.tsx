import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faLink } from "@fortawesome/free-solid-svg-icons";
import { Modal } from "../common/Modal";
import { type ReplyAgentDto, type ReplyAgentAssignedPostDto, assignReplyAgentPost } from "../../api/replyAgents";

export interface MonitorPostModalProps {
  isOpen: boolean;
  onClose: () => void;
  agents: ReplyAgentDto[];
  defaultAgentId?: string;
  onAssigned?: (assigned: ReplyAgentAssignedPostDto) => void;
}

export function MonitorPostModal({
  isOpen,
  onClose,
  agents,
  defaultAgentId,
  onAssigned,
}: MonitorPostModalProps) {
  const [selectedAgentId, setSelectedAgentId] = useState<string>(defaultAgentId || agents[0]?.id || "");
  const [postUrl, setPostUrl] = useState("");
  const [platform, setPlatform] = useState("auto");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (defaultAgentId) {
      setSelectedAgentId(defaultAgentId);
    } else if (agents.length > 0 && !selectedAgentId) {
      setSelectedAgentId(agents[0].id);
    }
  }, [defaultAgentId, agents]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedAgentId) {
      setError("Please select a reply agent.");
      return;
    }
    if (!postUrl.trim()) {
      setError("Please enter a post URL or platform URI.");
      return;
    }

    try {
      setIsSubmitting(true);
      setError(null);

      const assigned = await assignReplyAgentPost(selectedAgentId, {
        postUrl: postUrl.trim(),
        platform: platform === "auto" ? null : platform,
      });

      setPostUrl("");
      onClose();
      onAssigned?.(assigned);
    } catch (err: any) {
      setError(err.message || "Failed to monitor post");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Monitor Social Post"
      description="Assign any post (organic, external, or internal) to an autonomous reply agent to actively evaluate incoming comments."
      maxWidth="md"
      footer={
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="px-3 py-1.5 rounded-lg border border-border text-xs font-mono text-muted hover:text-foreground transition-colors"
          >
            Cancel
          </button>
          <button
            type="submit"
            form="monitor-post-form"
            disabled={isSubmitting || !postUrl.trim() || !selectedAgentId}
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-accent text-accent-foreground text-xs font-mono font-medium hover:bg-accent/90 transition-colors disabled:opacity-50"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>{isSubmitting ? "Assigning..." : "Start Monitoring"}</span>
          </button>
        </div>
      }
    >
      <form id="monitor-post-form" onSubmit={handleSubmit} className="space-y-4">
        {error && (
          <div className="p-3 text-xs font-mono bg-destructive/10 text-destructive border border-destructive/20 rounded-lg">
            {error}
          </div>
        )}

        <div>
          <label className="block text-xs font-mono text-foreground font-medium mb-1">
            Reply Agent <span className="text-destructive">*</span>
          </label>
          <select
            value={selectedAgentId}
            onChange={(e) => setSelectedAgentId(e.target.value)}
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          >
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name} ({agent.isEnabled ? "Active" : "Disabled"})
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="block text-xs font-mono text-foreground font-medium mb-1">
            Post URL or Platform URI <span className="text-destructive">*</span>
          </label>
          <div className="relative">
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-muted">
              <FontAwesomeIcon icon={faLink} className="w-3.5 h-3.5" />
            </div>
            <input
              type="text"
              required
              value={postUrl}
              onChange={(e) => setPostUrl(e.target.value)}
              placeholder="https://bsky.app/profile/alice.bsky.social/post/3ld..."
              className="w-full bg-bg border border-border rounded-lg pl-9 pr-3 py-2 text-xs font-mono text-foreground placeholder:text-muted focus:outline-none focus:border-accent"
            />
          </div>
          <p className="mt-1 text-[11px] font-mono text-muted">
            Supports Bluesky post URLs, Meta Threads post URLs, or raw AT-URIs.
          </p>
        </div>

        <div>
          <label className="block text-xs font-mono text-foreground font-medium mb-1">
            Platform Hint
          </label>
          <select
            value={platform}
            onChange={(e) => setPlatform(e.target.value)}
            className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          >
            <option value="auto">Auto-detect from URL</option>
            <option value="Bluesky">Bluesky</option>
            <option value="MetaThreads">Meta Threads</option>
          </select>
        </div>
      </form>
    </Modal>
  );
}
