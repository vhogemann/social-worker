import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faCommentDots,
  faImage,
  faLink,
  faPaperPlane,
  faPenToSquare,
  faRefresh,
  faRobot,
  faSliders,
  faSpinner,
} from "@fortawesome/free-solid-svg-icons";
import {
  getWorkflowAssignedReplyAgent,
  assignWorkflowReplyAgent,
  listReplyAgents,
  pollWorkflowReplyAgent,
  getWorkflowCommentAudits,
  approveWorkflowCommentReply,
  type ReplyAgentDto,
  type WorkflowCommentAuditDto,
} from "../../api/replyAgents";
import { Button } from "../common/Button";
import { Badge, type BadgeVariant } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import { EmptyState } from "../common/EmptyState";

export interface WorkflowReplyAgentTabProps {
  workflowId: string;
}

export const WorkflowReplyAgentTab: React.FC<WorkflowReplyAgentTabProps> = ({ workflowId }) => {
  const [assignedAgent, setAssignedAgent] = useState<ReplyAgentDto | null>(null);
  const [availableAgents, setAvailableAgents] = useState<ReplyAgentDto[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string>("");
  const [audits, setAudits] = useState<WorkflowCommentAuditDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingAssignment, setSavingAssignment] = useState(false);
  const [polling, setPolling] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [approvingAuditId, setApprovingAuditId] = useState<string | null>(null);
  const [editingAuditId, setEditingAuditId] = useState<string | null>(null);
  const [editedReplies, setEditedReplies] = useState<Record<string, string>>({});

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [assigned, allAgents, auditsData] = await Promise.all([
        getWorkflowAssignedReplyAgent(workflowId),
        listReplyAgents(),
        getWorkflowCommentAudits(workflowId).catch(() => [] as WorkflowCommentAuditDto[]),
      ]);

      setAssignedAgent(assigned);
      setSelectedAgentId(assigned?.id || "");
      setAvailableAgents(allAgents);
      setAudits(auditsData);
    } catch (err: any) {
      setError(err?.message || "Failed to load Reply Agent configuration.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [workflowId]);

  const handleAssign = async () => {
    setSavingAssignment(true);
    setError(null);
    setSuccessMessage(null);
    try {
      const targetId = selectedAgentId ? selectedAgentId : null;
      const updated = await assignWorkflowReplyAgent(workflowId, targetId);
      setAssignedAgent(updated);
      setSuccessMessage(updated ? `Assigned "${updated.name}" to this workflow.` : "Reply agent unassigned from this workflow.");
      const updatedAudits = await getWorkflowCommentAudits(workflowId).catch(() => [] as WorkflowCommentAuditDto[]);
      setAudits(updatedAudits);
    } catch (err: any) {
      setError(err?.message || "Failed to update reply agent assignment.");
    } finally {
      setSavingAssignment(false);
    }
  };

  const handlePoll = async () => {
    setPolling(true);
    setError(null);
    setSuccessMessage(null);
    try {
      const res = await pollWorkflowReplyAgent(workflowId);
      setSuccessMessage(`Reply monitoring check complete. Checked ${res.postsChecked} post(s).`);
      const updatedAudits = await getWorkflowCommentAudits(workflowId).catch(() => [] as WorkflowCommentAuditDto[]);
      setAudits(updatedAudits);
    } catch (err: any) {
      setError(err?.message || "Failed to trigger comment polling.");
    } finally {
      setPolling(false);
    }
  };

  const handleApproveReply = async (auditId: string) => {
    try {
      setApprovingAuditId(auditId);
      setError(null);
      const textToSend =
        editedReplies[auditId] !== undefined
          ? editedReplies[auditId]
          : audits.find((a) => a.id === auditId)?.generatedReplyText ?? undefined;

      const result = await approveWorkflowCommentReply(workflowId, auditId, textToSend);
      setAudits((prev) =>
        prev.map((a) =>
          a.id === auditId
            ? {
                ...a,
                rankingDecision: "Replied",
                generatedReplyText: result.replyText,
                remoteReplyId: result.remoteReplyId ?? null,
                errorMessage: null,
              }
            : a
        )
      );
      setEditingAuditId(null);
      setSuccessMessage(`Reply published successfully! Remote ID: ${result.remoteReplyId || "Sent"}`);
    } catch (err: any) {
      setError(err?.message || "Failed to publish reply.");
    } finally {
      setApprovingAuditId(null);
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

  if (loading && !assignedAgent) {
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

      {/* Assignment Card */}
      <div className="p-5 rounded-xl border border-border bg-card flex flex-col gap-5">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 border-b border-border pb-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-lg bg-accent/10 border border-accent/20 flex items-center justify-center text-accent">
              <FontAwesomeIcon icon={faRobot} className="text-base" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h2 className="text-sm font-semibold text-foreground">Assigned Reply Agent</h2>
                {assignedAgent ? (
                  <Badge variant={assignedAgent.isEnabled ? "success" : "neutral"}>
                    {assignedAgent.isEnabled ? "Active" : "Disabled"}
                  </Badge>
                ) : (
                  <Badge variant="neutral">None Assigned</Badge>
                )}
              </div>
              <p className="text-xs text-muted mt-0.5">
                Assign a shared reply agent from your library to evaluate and respond to comments on posts published by this workflow.
              </p>
            </div>
          </div>

          <Link
            to="/settings/reply-agents"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-border text-xs font-mono text-muted hover:text-foreground hover:bg-bg transition-colors"
          >
            <FontAwesomeIcon icon={faSliders} className="text-xs" />
            <span>Manage Agents Library</span>
          </Link>
        </div>

        <div className="flex flex-col sm:flex-row items-start sm:items-end gap-3">
          <div className="flex-1 w-full">
            <label htmlFor="agent-select" className="text-xs font-medium text-foreground block mb-1">
              Select Reply Agent
            </label>
            <select
              id="agent-select"
              value={selectedAgentId}
              onChange={(e) => setSelectedAgentId(e.target.value)}
              className="w-full px-3 py-2 bg-bg border border-border rounded-lg text-xs font-mono text-foreground focus:outline-none focus:border-accent"
              data-testid="agent-select-dropdown"
            >
              <option value="">-- No Reply Agent Assigned --</option>
              {availableAgents.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name} {!a.isEnabled ? "(Disabled)" : ""} ({a.autoPublishReplies ? "Auto-Publish" : "Requires Approval"})
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-2 w-full sm:w-auto">
            <Button
              type="button"
              variant="primary"
              size="sm"
              onClick={handleAssign}
              disabled={savingAssignment || selectedAgentId === (assignedAgent?.id || "")}
              data-testid="save-assignment-button"
            >
              <FontAwesomeIcon icon={savingAssignment ? faSpinner : faRobot} spin={savingAssignment} className="mr-1.5 text-xs" />
              <span>{savingAssignment ? "Saving..." : "Apply Assignment"}</span>
            </Button>

            {assignedAgent && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={handlePoll}
                disabled={polling || !assignedAgent.isEnabled}
                data-testid="poll-replies-button"
              >
                <FontAwesomeIcon icon={polling ? faSpinner : faBolt} spin={polling} className="mr-1.5 text-xs" />
                <span>{polling ? "Polling..." : "Poll Replies Now"}</span>
              </Button>
            )}
          </div>
        </div>

        {/* Assigned Agent Details Summary */}
        {assignedAgent && (
          <div className="mt-2 p-3.5 rounded-lg border border-border/60 bg-panel/30 text-xs flex flex-col gap-2">
            <div className="flex items-center justify-between">
              <span className="font-semibold text-foreground">{assignedAgent.name}</span>
              <span className="text-[11px] font-mono text-muted">
                Polls every {assignedAgent.checkIntervalMinutes}m • Assigned to {assignedAgent.assignedWorkflowsCount} workflow(s)
              </span>
            </div>
            {assignedAgent.description && (
              <p className="text-muted text-[11px]">{assignedAgent.description}</p>
            )}
            <div className="bg-bg/60 p-2 rounded border border-border/40 font-mono text-[11px] text-muted line-clamp-2">
              {assignedAgent.personaPrompt}
            </div>
            <div className="flex flex-wrap gap-2 text-[10px] font-mono text-muted pt-1">
              <span className="px-2 py-0.5 bg-bg border border-border rounded">Min Score: {assignedAgent.minRankingScore}/10</span>
              <span className="px-2 py-0.5 bg-bg border border-border rounded">Max Replies/Branch: {assignedAgent.maxRepliesPerCommentThread}</span>
              <span className="px-2 py-0.5 bg-bg border border-border rounded">Max Depth: {assignedAgent.maxThreadDepth}</span>
              <span className="px-2 py-0.5 bg-bg border border-border rounded">{assignedAgent.autoPublishReplies ? "Auto-Publish Replies" : "Requires Approval"}</span>
            </div>
          </div>
        )}
      </div>

      {/* Audits History */}
      <div className="flex flex-col gap-4" data-testid="reply-audits-section">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h3 className="text-sm font-semibold text-foreground">Evaluated Comments & Activity</h3>
            <span className="text-xs font-mono text-muted">({audits.length})</span>
          </div>
          <Button type="button" variant="ghost" size="sm" onClick={() => void loadData()} title="Refresh audits">
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
                    <div className="mt-1 p-2.5 rounded-lg bg-accent/5 border border-accent/20 flex flex-col gap-2">
                      <div className="flex items-center justify-between">
                        <span className="text-[10px] uppercase font-mono tracking-wider text-accent font-semibold flex items-center gap-1">
                          <FontAwesomeIcon icon={faCommentDots} className="text-[10px]" />
                          <span>Generated Reply</span>
                        </span>
                        {audit.rankingDecision === "PendingApproval" && (
                          <span className="text-[10px] font-mono uppercase tracking-wider text-amber-500 font-medium">
                            Awaiting Review
                          </span>
                        )}
                      </div>

                      {editingAuditId === audit.id ? (
                        <div className="flex flex-col gap-2">
                          <textarea
                            value={editedReplies[audit.id] ?? audit.generatedReplyText}
                            onChange={(e) =>
                              setEditedReplies((prev) => ({ ...prev, [audit.id]: e.target.value }))
                            }
                            rows={3}
                            className="w-full text-xs font-sans rounded-lg border border-border bg-card p-2 text-foreground focus:outline-none focus:border-accent"
                          />
                          <div className="flex items-center gap-2">
                            <Button
                              type="button"
                              size="sm"
                              variant="outline"
                              onClick={() => setEditingAuditId(null)}
                            >
                              Cancel Edit
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <span className="text-foreground text-xs leading-relaxed">
                          {editedReplies[audit.id] ?? audit.generatedReplyText}
                        </span>
                      )}

                      {audit.remoteReplyId && (
                        <span className="text-[10px] text-muted font-mono mt-1">
                          Remote ID: {audit.remoteReplyId}
                        </span>
                      )}

                      {audit.rankingDecision === "PendingApproval" && (
                        <div className="flex flex-wrap items-center gap-2 pt-2 border-t border-accent/20">
                          <Button
                            type="button"
                            variant="primary"
                            size="sm"
                            disabled={approvingAuditId === audit.id}
                            onClick={() => void handleApproveReply(audit.id)}
                            data-testid={`approve-reply-btn-${audit.id}`}
                          >
                            <FontAwesomeIcon
                              icon={approvingAuditId === audit.id ? faSpinner : faPaperPlane}
                              spin={approvingAuditId === audit.id}
                              className="mr-1.5 text-xs"
                            />
                            <span>{approvingAuditId === audit.id ? "Sending..." : "Approve & Reply"}</span>
                          </Button>
                          {editingAuditId !== audit.id && (
                            <Button
                              type="button"
                              variant="outline"
                              size="sm"
                              onClick={() => {
                                setEditingAuditId(audit.id);
                                if (editedReplies[audit.id] === undefined) {
                                  setEditedReplies((prev) => ({
                                    ...prev,
                                    [audit.id]: audit.generatedReplyText ?? "",
                                  }));
                                }
                              }}
                            >
                              <FontAwesomeIcon icon={faPenToSquare} className="mr-1.5 text-xs" />
                              <span>Edit Reply</span>
                            </Button>
                          )}
                        </div>
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
