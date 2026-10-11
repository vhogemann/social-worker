import React, { useEffect, useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faCheck,
  faCommentDots,
  faExternalLinkAlt,
  faImage,
  faLink,
  faRefresh,
  faRobot,
  faSave,
  faShieldHalved,
  faSpinner
} from "@fortawesome/free-solid-svg-icons";
import {
  getWorkflowReplyAgent,
  updateWorkflowReplyAgent,
  pollWorkflowReplyAgent,
  getWorkflowCommentAudits,
  type WorkflowReplyAgentDto,
  type WorkflowCommentAuditDto,
  type UpdateWorkflowReplyAgentRequest
} from "../../api/replyAgents";
import { Button } from "../common/Button";
import { Badge, type BadgeVariant } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import { EmptyState } from "../common/EmptyState";

export interface WorkflowReplyAgentTabProps {
  workflowId: string;
}

export const WorkflowReplyAgentTab: React.FC<WorkflowReplyAgentTabProps> = ({ workflowId }) => {
  const [agent, setAgent] = useState<WorkflowReplyAgentDto | null>(null);
  const [audits, setAudits] = useState<WorkflowCommentAuditDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [polling, setPolling] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const [formData, setFormData] = useState<UpdateWorkflowReplyAgentRequest>({
    isEnabled: true,
    personaPrompt: "",
    minRankingScore: 7,
    maxCommentAgeHours: 24,
    maxRepliesPerCommentThread: 2,
    maxRepliesPerUser: 3,
    maxThreadDepth: 4,
    autoPublishReplies: true,
    checkIntervalMinutes: 15
  });

  const loadAgentAndAudits = async () => {
    setLoading(true);
    setError(null);
    try {
      const [agentData, auditsData] = await Promise.all([
        getWorkflowReplyAgent(workflowId),
        getWorkflowCommentAudits(workflowId).catch(() => [] as WorkflowCommentAuditDto[])
      ]);

      if (agentData) {
        setAgent(agentData);
        setFormData({
          isEnabled: agentData.isEnabled,
          personaPrompt: agentData.personaPrompt,
          minRankingScore: agentData.minRankingScore,
          maxCommentAgeHours: agentData.maxCommentAgeHours,
          maxRepliesPerCommentThread: agentData.maxRepliesPerCommentThread,
          maxRepliesPerUser: agentData.maxRepliesPerUser,
          maxThreadDepth: agentData.maxThreadDepth,
          autoPublishReplies: agentData.autoPublishReplies,
          checkIntervalMinutes: agentData.checkIntervalMinutes
        });
      }
      setAudits(auditsData);
    } catch (err: any) {
      setError(err?.message || "Failed to load Reply Agent configuration.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadAgentAndAudits();
  }, [workflowId]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setError(null);
    setSuccessMessage(null);
    try {
      const updated = await updateWorkflowReplyAgent(workflowId, formData);
      setAgent(updated);
      setSuccessMessage("Reply Agent settings saved successfully.");
    } catch (err: any) {
      setError(err?.message || "Failed to save Reply Agent settings.");
    } finally {
      setSaving(false);
    }
  };

  const handlePoll = async () => {
    setPolling(true);
    setError(null);
    setSuccessMessage(null);
    try {
      const res = await pollWorkflowReplyAgent(workflowId);
      setSuccessMessage(res.message || "Reply monitoring check triggered.");
      const updatedAudits = await getWorkflowCommentAudits(workflowId).catch(() => [] as WorkflowCommentAuditDto[]);
      setAudits(updatedAudits);
      const updatedAgent = await getWorkflowReplyAgent(workflowId);
      if (updatedAgent) setAgent(updatedAgent);
    } catch (err: any) {
      setError(err?.message || "Failed to trigger comment polling.");
    } finally {
      setPolling(false);
    }
  };

  const getScoreBadgeVariant = (score: number): BadgeVariant => {
    if (score >= 7) return "success";
    if (score >= 5) return "warning";
    return "neutral";
  };

  const getDecisionBadgeVariant = (decision: string): BadgeVariant => {
    switch (decision) {
      case "Replied":
        return "success";
      case "PendingApproval":
        return "accent";
      case "SkippedLowScore":
      case "SkippedTooOld":
        return "neutral";
      case "SkippedTerminal":
      case "SkippedStopCriteria":
        return "warning";
      case "Failed":
        return "danger";
      default:
        return "neutral";
    }
  };

  const parseLinks = (json: string | null) => {
    if (!json) return [];
    try {
      return JSON.parse(json) as Array<{ url: string; title?: string }>;
    } catch {
      return [];
    }
  };

  const parseImages = (json: string | null) => {
    if (!json) return [];
    try {
      return JSON.parse(json) as Array<{ imageUrl: string; altText?: string; visualDescription?: string }>;
    } catch {
      return [];
    }
  };

  if (loading && !agent) {
    return (
      <div className="flex flex-col items-center justify-center p-12 text-muted" data-testid="reply-agent-loading">
        <FontAwesomeIcon icon={faSpinner} spin className="text-xl mb-3" />
        <span className="text-xs font-mono">Loading Reply Agent configuration...</span>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-8" data-testid="workflow-reply-agent-tab">
      {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}
      {successMessage && <AlertBanner variant="info" message={successMessage} onDismiss={() => setSuccessMessage(null)} />}

      {/* Top Banner / Actions */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 p-4 rounded-xl border border-border bg-card">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-accent/10 border border-accent/20 flex items-center justify-center text-accent">
            <FontAwesomeIcon icon={faRobot} className="text-base" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h2 className="text-sm font-semibold text-foreground">Reply Agent</h2>
              <Badge variant={formData.isEnabled ? "success" : "neutral"}>
                {formData.isEnabled ? "Active" : "Disabled"}
              </Badge>
              {formData.autoPublishReplies ? (
                <Badge variant="accent">Auto-Publish</Badge>
              ) : (
                <Badge variant="neutral">Requires Approval</Badge>
              )}
            </div>
            <p className="text-xs text-muted mt-0.5">
              Monitors public replies to posts published by this workflow, ranks quality, and replies automatically.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 w-full sm:w-auto">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={handlePoll}
            disabled={polling || !formData.isEnabled}
            data-testid="poll-replies-button"
            className="flex-1 sm:flex-none"
          >
            <FontAwesomeIcon icon={polling ? faSpinner : faBolt} spin={polling} className="mr-1.5 text-xs" />
            <span>{polling ? "Polling..." : "Poll Replies Now"}</span>
          </Button>
        </div>
      </div>

      {/* Settings Form */}
      <form onSubmit={handleSave} className="flex flex-col gap-6" data-testid="reply-agent-form">
        <div className="p-5 rounded-xl border border-border bg-card flex flex-col gap-5">
          <h3 className="text-sm font-semibold text-foreground border-b border-border pb-3">Agent Settings & Constraints</h3>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="flex items-center justify-between p-3 rounded-lg border border-border bg-muted/20">
              <div>
                <label htmlFor="agent-enabled" className="text-xs font-medium text-foreground block">
                  Enable Reply Agent
                </label>
                <span className="text-[11px] text-muted">Allow the agent to scan replies and respond.</span>
              </div>
              <input
                id="agent-enabled"
                type="checkbox"
                checked={formData.isEnabled ?? true}
                onChange={(e) => setFormData((prev) => ({ ...prev, isEnabled: e.target.checked }))}
                className="w-4 h-4 rounded text-accent focus:ring-accent border-border"
                data-testid="agent-enabled-checkbox"
              />
            </div>

            <div className="flex items-center justify-between p-3 rounded-lg border border-border bg-muted/20">
              <div>
                <label htmlFor="agent-auto-publish" className="text-xs font-medium text-foreground block">
                  Auto-Publish Replies
                </label>
                <span className="text-[11px] text-muted">Directly publish responses without manual approval.</span>
              </div>
              <input
                id="agent-auto-publish"
                type="checkbox"
                checked={formData.autoPublishReplies ?? true}
                onChange={(e) => setFormData((prev) => ({ ...prev, autoPublishReplies: e.target.checked }))}
                className="w-4 h-4 rounded text-accent focus:ring-accent border-border"
                data-testid="agent-auto-publish-checkbox"
              />
            </div>
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="agent-persona" className="text-xs font-medium text-foreground">
              Persona & Instructions Prompt
            </label>
            <textarea
              id="agent-persona"
              rows={3}
              value={formData.personaPrompt || ""}
              onChange={(e) => setFormData((prev) => ({ ...prev, personaPrompt: e.target.value }))}
              placeholder="e.g. You are a knowledgeable, concise technical assistant. Respond helpfully to genuine inquiries..."
              className="w-full px-3 py-2 text-xs rounded-lg border border-border bg-bg text-foreground placeholder:text-muted focus:outline-none focus:ring-1 focus:ring-accent font-sans"
              data-testid="agent-persona-input"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
            <div className="flex flex-col gap-1">
              <label htmlFor="min-ranking-score" className="text-xs font-medium text-foreground">
                Min Ranking Score (1 - 10)
              </label>
              <input
                id="min-ranking-score"
                type="number"
                min={1}
                max={10}
                value={formData.minRankingScore ?? 7}
                onChange={(e) => setFormData((prev) => ({ ...prev, minRankingScore: parseInt(e.target.value, 10) || 7 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="min-score-input"
              />
              <span className="text-[10px] text-muted">Comments below this score are skipped.</span>
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="max-comment-age" className="text-xs font-medium text-foreground">
                Max Comment Age (Hours)
              </label>
              <input
                id="max-comment-age"
                type="number"
                min={1}
                max={168}
                value={formData.maxCommentAgeHours ?? 24}
                onChange={(e) => setFormData((prev) => ({ ...prev, maxCommentAgeHours: parseInt(e.target.value, 10) || 24 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="max-age-input"
              />
              <span className="text-[10px] text-muted">Ignore comments older than this threshold.</span>
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="max-replies-branch" className="text-xs font-medium text-foreground">
                Max Replies Per Branch
              </label>
              <input
                id="max-replies-branch"
                type="number"
                min={1}
                max={10}
                value={formData.maxRepliesPerCommentThread ?? 2}
                onChange={(e) => setFormData((prev) => ({ ...prev, maxRepliesPerCommentThread: parseInt(e.target.value, 10) || 2 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="max-replies-branch-input"
              />
              <span className="text-[10px] text-muted">Stop criteria limit per sub-thread.</span>
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="max-replies-user" className="text-xs font-medium text-foreground">
                Max Replies Per User
              </label>
              <input
                id="max-replies-user"
                type="number"
                min={1}
                max={10}
                value={formData.maxRepliesPerUser ?? 3}
                onChange={(e) => setFormData((prev) => ({ ...prev, maxRepliesPerUser: parseInt(e.target.value, 10) || 3 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="max-replies-user-input"
              />
              <span className="text-[10px] text-muted">Prevents endless back-and-forth loops.</span>
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="max-thread-depth" className="text-xs font-medium text-foreground">
                Max Thread Depth
              </label>
              <input
                id="max-thread-depth"
                type="number"
                min={1}
                max={10}
                value={formData.maxThreadDepth ?? 4}
                onChange={(e) => setFormData((prev) => ({ ...prev, maxThreadDepth: parseInt(e.target.value, 10) || 4 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="max-thread-depth-input"
              />
              <span className="text-[10px] text-muted">Deep branches are ignored.</span>
            </div>

            <div className="flex flex-col gap-1">
              <label htmlFor="check-interval" className="text-xs font-medium text-foreground">
                Check Interval (Minutes)
              </label>
              <input
                id="check-interval"
                type="number"
                min={5}
                max={1440}
                value={formData.checkIntervalMinutes ?? 15}
                onChange={(e) => setFormData((prev) => ({ ...prev, checkIntervalMinutes: parseInt(e.target.value, 10) || 15 }))}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-border bg-bg text-foreground"
                data-testid="check-interval-input"
              />
              <span className="text-[10px] text-muted">Background polling schedule.</span>
            </div>
          </div>

          <div className="flex justify-end pt-2 border-t border-border">
            <Button type="submit" variant="primary" size="sm" disabled={saving} data-testid="save-agent-settings-button">
              <FontAwesomeIcon icon={saving ? faSpinner : faSave} spin={saving} className="mr-1.5 text-xs" />
              <span>{saving ? "Saving..." : "Save Settings"}</span>
            </Button>
          </div>
        </div>
      </form>

      {/* Audits History */}
      <div className="flex flex-col gap-4" data-testid="reply-audits-section">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-foreground">Evaluated Comments & Activity</h3>
            <span className="text-xs font-mono text-muted">({audits.length})</span>
          </div>
          <Button type="button" variant="ghost" size="sm" onClick={() => void loadAgentAndAudits()} title="Refresh audits">
            <FontAwesomeIcon icon={faRefresh} className="text-xs mr-1" />
            <span>Refresh</span>
          </Button>
        </div>

        {audits.length === 0 ? (
          <EmptyState
            title="No comments evaluated yet"
            description="When posts published by this workflow receive replies, the Reply Agent will rank and display decisions here."
          />
        ) : (
          <div className="flex flex-col gap-3">
            {audits.map((audit) => {
              const links = parseLinks(audit.parsedLinksJson);
              const images = parseImages(audit.parsedImagesJson);

              return (
                <div
                  key={audit.id}
                  className="p-4 rounded-xl border border-border bg-card flex flex-col gap-3 text-xs"
                  data-testid={`audit-row-${audit.id}`}
                >
                  <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 pb-2">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-foreground">@{audit.remoteAuthorHandle}</span>
                      <span className="text-muted font-mono text-[11px]">{audit.platform}</span>
                      <span className="text-muted">•</span>
                      <span className="text-muted text-[11px]">{new Date(audit.commentCreatedAt).toLocaleString()}</span>
                    </div>

                    <div className="flex items-center gap-2">
                      <Badge variant={getScoreBadgeVariant(audit.rankingScore)}>
                        Score: {audit.rankingScore}/10
                      </Badge>
                      <Badge variant="neutral">{audit.rankingCategory}</Badge>
                      <Badge variant={getDecisionBadgeVariant(audit.rankingDecision)}>
                        {audit.rankingDecision}
                      </Badge>
                    </div>
                  </div>

                  <div className="text-foreground text-xs leading-relaxed font-sans whitespace-pre-wrap">
                    "{audit.commentText}"
                  </div>

                  {(links.length > 0 || images.length > 0) && (
                    <div className="flex flex-wrap gap-2 text-[11px] text-muted bg-muted/20 p-2 rounded-lg">
                      {links.map((link, idx) => (
                        <span key={idx} className="inline-flex items-center gap-1 text-accent">
                          <FontAwesomeIcon icon={faLink} className="text-[10px]" />
                          <span className="truncate max-w-[200px]">{link.title || link.url}</span>
                        </span>
                      ))}
                      {images.map((img, idx) => (
                        <span key={idx} className="inline-flex items-center gap-1 text-accent">
                          <FontAwesomeIcon icon={faImage} className="text-[10px]" />
                          <span className="truncate max-w-[200px]">{img.visualDescription || img.altText || "Image"}</span>
                        </span>
                      ))}
                    </div>
                  )}

                  {audit.rankingReasoning && (
                    <div className="text-[11px] text-muted italic">
                      Reasoning: {audit.rankingReasoning}
                    </div>
                  )}

                  {audit.generatedReplyText && (
                    <div className="mt-1 p-2.5 rounded-lg bg-accent/5 border border-accent/20 flex flex-col gap-1">
                      <span className="text-[10px] uppercase font-mono tracking-wider text-accent font-semibold flex items-center gap-1">
                        <FontAwesomeIcon icon={faCommentDots} className="text-[10px]" />
                        <span>Generated Reply</span>
                      </span>
                      <span className="text-foreground text-xs leading-relaxed">
                        {audit.generatedReplyText}
                      </span>
                      {audit.remoteReplyId && (
                        <span className="text-[10px] text-muted font-mono mt-1">
                          Remote ID: {audit.remoteReplyId}
                        </span>
                      )}
                    </div>
                  )}

                  {audit.errorMessage && (
                    <div className="text-[11px] text-destructive bg-destructive/10 p-2 rounded-lg">
                      Error: {audit.errorMessage}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};
