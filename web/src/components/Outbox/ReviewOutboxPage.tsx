import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faInbox,
  faCalendarAlt,
  faPaperPlane,
  faPenNib,
  faTrash,
  faSpinner,
  faBolt
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchOutboxDrafts,
  approveOutboxDraft,
  dismissOutboxDraft,
  type OutboxDraftDto
} from "../../api/outbox";
import { fetchSchedules, type ScheduleSummaryDto } from "../../api/schedules";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { AlertBanner } from "../common/AlertBanner";
import { Badge } from "../common/Badge";
import { ConfirmModal } from "../common/ConfirmModal";
import { getPlatformLabel } from "../../config/platformBadges";

export const ReviewOutboxPage: React.FC = () => {
  const navigate = useNavigate();
  const [drafts, setDrafts] = useState<OutboxDraftDto[]>([]);
  const [schedules, setSchedules] = useState<ScheduleSummaryDto[]>([]);
  const [selectedScheduleId, setSelectedScheduleId] = useState<string>("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [dismissingDraftId, setDismissingDraftId] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [outboxList, scheduleList] = await Promise.all([
        fetchOutboxDrafts(),
        fetchSchedules().catch(() => [] as ScheduleSummaryDto[])
      ]);
      setDrafts(outboxList);
      setSchedules(scheduleList);
      if (scheduleList.length > 0 && !selectedScheduleId) {
        setSelectedScheduleId(scheduleList[0].id);
      }
    } catch (err: any) {
      setError(err?.message || "Failed to load outbox drafts.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleApproveQueue = async (draftId: string) => {
    if (!selectedScheduleId) {
      setError("Please select or create a schedule first.");
      return;
    }
    setActionInProgress(draftId);
    try {
      await approveOutboxDraft(draftId, {
        action: "queue",
        scheduleId: selectedScheduleId
      });
      setDrafts((prev) => prev.filter((d) => d.draftId !== draftId));
    } catch (err: any) {
      setError(err?.message || "Failed to enqueue draft.");
    } finally {
      setActionInProgress(null);
    }
  };

  const handlePublishNow = async (draftId: string) => {
    setActionInProgress(draftId);
    try {
      await approveOutboxDraft(draftId, {
        action: "publish_now"
      });
      setDrafts((prev) => prev.filter((d) => d.draftId !== draftId));
    } catch (err: any) {
      setError(err?.message || "Failed to publish draft immediately.");
    } finally {
      setActionInProgress(null);
    }
  };

  const handleDismiss = async () => {
    if (!dismissingDraftId) return;
    try {
      await dismissOutboxDraft(dismissingDraftId);
      setDrafts((prev) => prev.filter((d) => d.draftId !== dismissingDraftId));
    } catch (err: any) {
      setError(err?.message || "Failed to dismiss draft.");
    } finally {
      setDismissingDraftId(null);
    }
  };

  return (
    <div className="flex-1 overflow-y-auto bg-background text-foreground p-6">
      <div className="max-w-4xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-border pb-4">
          <div>
            <div className="flex items-center gap-2">
              <FontAwesomeIcon icon={faInbox} className="text-accent text-lg" />
              <h1 className="text-xl font-bold">Review Outbox</h1>
              <Badge variant="neutral" size="sm">
                {drafts.length} pending
              </Badge>
            </div>
            <p className="text-xs text-muted mt-1">
              Automated workflow drafts awaiting your editorial review before publication or scheduling.
            </p>
          </div>

          {schedules.length > 0 && (
            <div className="flex items-center gap-2 text-xs">
              <span className="text-muted shrink-0">Default Schedule:</span>
              <select
                value={selectedScheduleId}
                onChange={(e) => setSelectedScheduleId(e.target.value)}
                className="bg-panel border border-border rounded-lg px-2.5 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
              >
                {schedules.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.name} ({s.timezone})
                  </option>
                ))}
              </select>
            </div>
          )}
        </div>

        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {loading ? (
          <div className="flex justify-center items-center py-20 text-muted gap-2">
            <FontAwesomeIcon icon={faSpinner} spin />
            <span className="text-xs">Loading outbox drafts...</span>
          </div>
        ) : drafts.length === 0 ? (
          <EmptyState
            icon={faInbox}
            title="Outbox is clear"
            description="There are no drafts waiting for review. Workflows running in Manual mode will send their generated threads here."
            action={
              <Button size="sm" variant="secondary" onClick={() => navigate("/workflows")}>
                View Workflows
              </Button>
            }
          />
        ) : (
          <div className="space-y-4">
            {drafts.map((draft) => {
              const isBusy = actionInProgress === draft.draftId;
              return (
                <div
                  key={draft.draftId}
                  className="bg-panel border border-border rounded-xl p-5 shadow-sm space-y-4 transition hover:border-border/80"
                  data-testid={`outbox-card-${draft.draftId}`}
                >
                  {/* Origin Badge & Date */}
                  <div className="flex items-center justify-between gap-2 text-xs">
                    <div className="flex items-center gap-1.5 text-accent font-medium">
                      <FontAwesomeIcon icon={faBolt} className="text-[10px]" />
                      <span>{draft.workflowTitle || "Automated Workflow"}</span>
                    </div>
                    <span className="text-muted">
                      {new Date(draft.createdAt).toLocaleString(undefined, {
                        month: "short",
                        day: "numeric",
                        hour: "2-digit",
                        minute: "2-digit"
                      })}
                    </span>
                  </div>

                  {/* Title & Preview */}
                  <div>
                    <h2 className="text-base font-semibold text-foreground mb-1">{draft.title}</h2>
                    <p className="text-xs text-muted line-clamp-3 whitespace-pre-wrap font-sans bg-background/50 p-3 rounded-lg border border-border/50">
                      {draft.content}
                    </p>
                  </div>

                  {/* Platforms */}
                  {draft.threads && draft.threads.length > 0 && (
                    <div className="flex items-center gap-2 flex-wrap text-xs">
                      <span className="text-muted text-[11px]">Targets:</span>
                      {draft.threads.map((t) => (
                        <span
                          key={t.threadId}
                          className="px-2 py-0.5 rounded bg-background border border-border text-foreground text-[11px] font-mono"
                        >
                          {getPlatformLabel(t.platform)} {t.accountHandle ? `(${t.accountHandle})` : ""}
                        </span>
                      ))}
                    </div>
                  )}

                  {/* Actions Toolbar */}
                  <div className="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-border/50">
                    <div className="flex items-center gap-2">
                      <Button
                        size="sm"
                        variant="primary"
                        onClick={() => handleApproveQueue(draft.draftId)}
                        disabled={isBusy || schedules.length === 0}
                      >
                        {isBusy ? (
                          <FontAwesomeIcon icon={faSpinner} spin />
                        ) : (
                          <FontAwesomeIcon icon={faCalendarAlt} className="mr-1.5" />
                        )}
                        Approve & Queue
                      </Button>

                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => handlePublishNow(draft.draftId)}
                        disabled={isBusy}
                      >
                        <FontAwesomeIcon icon={faPaperPlane} className="mr-1.5" />
                        Publish Now
                      </Button>
                    </div>

                    <div className="flex items-center gap-2">
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => navigate(`/draft/${draft.draftId}`)}
                        disabled={isBusy}
                      >
                        <FontAwesomeIcon icon={faPenNib} className="mr-1.5" />
                        Edit in Composer
                      </Button>

                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-red-400 hover:text-red-300"
                        onClick={() => setDismissingDraftId(draft.draftId)}
                        disabled={isBusy}
                      >
                        <FontAwesomeIcon icon={faTrash} />
                      </Button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* Dismiss Modal */}
        <ConfirmModal
          isOpen={!!dismissingDraftId}
          title="Dismiss Outbox Draft"
          message="Are you sure you want to dismiss this draft? It will be archived and removed from your review queue."
          confirmLabel="Dismiss Draft"
          variant="danger"
          onConfirm={handleDismiss}
          onCancel={() => setDismissingDraftId(null)}
        />
      </div>
    </div>
  );
};
