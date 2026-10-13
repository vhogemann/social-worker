import React, { useEffect, useState, useCallback } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faTrash,
  faPause,
  faPlay,
  faArrowUp,
  faArrowDown,
  faGripVertical,
  faClock,
  faXmark,
  faSpinner,
  faInbox
} from "@fortawesome/free-solid-svg-icons";
import { Button } from "../common/Button";
import { Badge } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import {
  fetchScheduleQueue,
  reorderQueue,
  removeQueueItem,
  pauseQueueItem,
  type ScheduleQueueItemDto
} from "../../api/schedules";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";
import { formatTime } from "./calendarUtils";

export interface QueueSidebarProps {
  scheduleId: string;
  scheduleName: string;
  onQueueUpdated: () => void;
  onOpenDraftPicker: () => void;
  onClose?: () => void;
}

export const QueueSidebar: React.FC<QueueSidebarProps> = ({
  scheduleId,
  scheduleName,
  onQueueUpdated,
  onOpenDraftPicker,
  onClose
}) => {
  const [items, setItems] = useState<ScheduleQueueItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

  const loadQueue = useCallback(async () => {
    if (!scheduleId) {
      setItems([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const data = await fetchScheduleQueue(scheduleId);
      setItems(Array.isArray(data) ? data : []);
    } catch (err: any) {
      setError(err?.message || "Failed to load queue items.");
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [scheduleId]);

  useEffect(() => {
    loadQueue();
  }, [loadQueue]);

  const handleMove = async (fromIndex: number, toIndex: number) => {
    if (toIndex < 0 || toIndex >= items.length || fromIndex === toIndex) return;

    const updated = [...items];
    const [moved] = updated.splice(fromIndex, 1);
    updated.splice(toIndex, 0, moved);

    // Optimistic update
    setItems(updated);

    try {
      const orderedIds = updated.map((item) => item.id);
      const res = await reorderQueue(scheduleId, orderedIds);
      setItems(res);
      onQueueUpdated();
    } catch (err: any) {
      setError(err?.message || "Failed to reorder queue.");
      loadQueue();
    }
  };

  const handlePauseToggle = async (item: ScheduleQueueItemDto) => {
    const isCurrentlyPaused = item.status === "Paused";
    try {
      const updated = await pauseQueueItem(scheduleId, item.id, !isCurrentlyPaused);
      setItems((prev) => prev.map((i) => (i.id === item.id ? updated : i)));
      onQueueUpdated();
    } catch (err: any) {
      setError(err?.message || "Failed to toggle pause on queue item.");
    }
  };

  const handleRemove = async (itemId: string) => {
    try {
      await removeQueueItem(scheduleId, itemId);
      setItems((prev) => prev.filter((i) => i.id !== itemId));
      onQueueUpdated();
    } catch (err: any) {
      setError(err?.message || "Failed to remove queue item.");
    }
  };

  const handleDragStart = (index: number) => {
    setDraggedIndex(index);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
  };

  const handleDrop = (dropIndex: number) => {
    if (draggedIndex === null || draggedIndex === dropIndex) {
      setDraggedIndex(null);
      return;
    }
    handleMove(draggedIndex, dropIndex);
    setDraggedIndex(null);
  };

  return (
    <div className="w-80 sm:w-96 border-l border-border bg-card flex flex-col h-full overflow-hidden select-none shrink-0">
      {/* Sidebar Header */}
      <div className="p-3 border-b border-border bg-card/80 flex items-center justify-between gap-2 shrink-0">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <h3 className="text-xs font-mono uppercase tracking-wider font-semibold text-foreground">
              Input Queue
            </h3>
            <span className="px-1.5 py-0.2 rounded-full text-[10px] font-mono bg-accent/10 text-accent border border-accent/20">
              {(items || []).length}
            </span>
          </div>
          <p className="text-[11px] font-sans text-muted truncate">{scheduleName}</p>
        </div>

        <div className="flex items-center gap-1">
          <Button
            size="sm"
            variant="primary"
            icon={faPlus}
            onClick={onOpenDraftPicker}
            data-testid="sidebar-add-draft-btn"
          >
            Add Draft
          </Button>

          {onClose && (
            <button
              onClick={onClose}
              className="p-1 rounded text-muted hover:text-foreground hover:bg-zinc-800 transition"
              title="Close sidebar"
            >
              <FontAwesomeIcon icon={faXmark} className="w-4 h-4" />
            </button>
          )}
        </div>
      </div>

      {error && (
        <div className="p-2 shrink-0">
          <AlertBanner variant="error" message={error} />
        </div>
      )}

      {/* Queue List */}
      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {loading ? (
          <div className="p-8 text-center text-xs font-mono text-muted flex items-center justify-center gap-2">
            <FontAwesomeIcon icon={faSpinner} className="animate-spin text-accent" />
            <span>Loading queue...</span>
          </div>
        ) : items.length === 0 ? (
          <div className="p-8 rounded-lg border border-dashed border-border text-center">
            <FontAwesomeIcon icon={faInbox} className="text-muted text-xl mb-2" />
            <p className="text-xs font-mono text-muted font-medium">Queue is empty</p>
            <p className="text-[11px] text-muted/70 mt-1 mb-3">
              Add drafts to the schedule to begin automatic dispatching.
            </p>
            <Button
              size="sm"
              variant="secondary"
              icon={faPlus}
              onClick={onOpenDraftPicker}
            >
              Queue a Draft
            </Button>
          </div>
        ) : (
          items.map((item, index) => {
            const platformKey = item.platform || "Bluesky";
            const badgeClass =
              PLATFORM_BADGE_COLORS[platformKey] ||
              "bg-zinc-800 text-muted border-border";
            const iconDef = PLATFORM_ICONS[platformKey];
            const isPaused = item.status === "Paused";

            return (
              <div
                key={item.id}
                data-testid={`queue-item-${item.id}`}
                draggable
                onDragStart={() => handleDragStart(index)}
                onDragOver={handleDragOver}
                onDrop={() => handleDrop(index)}
                className={`p-3 rounded-lg border transition bg-card hover:bg-zinc-800/30 ${
                  isPaused ? "border-yellow-500/30 opacity-75" : "border-border"
                } ${draggedIndex === index ? "opacity-50 ring-2 ring-accent" : ""}`}
              >
                <div className="flex items-start justify-between gap-2 mb-2">
                  <div className="flex items-center gap-2 min-w-0">
                    <span className="cursor-grab text-muted hover:text-foreground">
                      <FontAwesomeIcon icon={faGripVertical} className="text-xs" />
                    </span>
                    <span className="text-xs font-mono font-bold text-accent">
                      #{index + 1}
                    </span>
                    <span
                      className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono border ${badgeClass}`}
                    >
                      {iconDef && <FontAwesomeIcon icon={iconDef} className="text-[8px]" />}
                      {platformKey}
                    </span>
                    {item.priorityScore != null && (
                      <span
                        title={
                          item.effectivePriorityScore != null
                            ? `Base: ${item.priorityScore}/10 | Effective: ${item.effectivePriorityScore.toFixed(1)}/10`
                            : `Priority: ${item.priorityScore}/10`
                        }
                        className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-mono border ${
                          item.priorityScore >= 9
                            ? "bg-red-950/40 text-red-400 border-red-800/40"
                            : item.priorityScore >= 7
                            ? "bg-amber-950/40 text-amber-400 border-amber-800/40"
                            : "bg-zinc-800 text-zinc-400 border-zinc-700"
                        }`}
                      >
                        {item.priorityScore >= 9 ? "P1" : item.priorityScore >= 7 ? "P2" : "P3"} {item.priorityScore}/10
                        {item.effectivePriorityScore != null && Math.abs(item.effectivePriorityScore - item.priorityScore) > 0.05 && (
                          <span className="opacity-75">({item.effectivePriorityScore.toFixed(1)})</span>
                        )}
                      </span>
                    )}
                  </div>

                  <Badge
                    variant={
                      item.status === "Queued"
                        ? "accent"
                        : item.status === "Paused"
                        ? "warning"
                        : item.status === "Dispatched"
                        ? "neutral"
                        : "danger"
                    }
                    size="xs"
                  >
                    {item.status}
                  </Badge>
                </div>

                <h4 className="text-xs font-semibold text-foreground truncate mb-1">
                  {item.draftTitle || "Untitled Draft"}
                </h4>

                {item.assignedSlotUtc && (
                  <div className="flex items-center gap-1 text-[10px] font-mono text-muted mb-2">
                    <FontAwesomeIcon icon={faClock} className="text-[9px]" />
                    <span>Slot: {formatTime(item.assignedSlotUtc)}</span>
                  </div>
                )}

                {item.lastError && (
                  <div className="text-[10px] font-mono text-red-400 bg-red-500/10 p-1.5 rounded mb-2">
                    {item.lastError}
                  </div>
                )}

                <div className="flex items-center justify-between pt-1 border-t border-border/50">
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      disabled={index === 0}
                      onClick={() => handleMove(index, index - 1)}
                      className="p-1 rounded text-muted hover:text-foreground disabled:opacity-30 transition text-xs"
                      title="Move up in queue"
                      data-testid={`move-up-${item.id}`}
                    >
                      <FontAwesomeIcon icon={faArrowUp} />
                    </button>
                    <button
                      type="button"
                      disabled={index === items.length - 1}
                      onClick={() => handleMove(index, index + 1)}
                      className="p-1 rounded text-muted hover:text-foreground disabled:opacity-30 transition text-xs"
                      title="Move down in queue"
                      data-testid={`move-down-${item.id}`}
                    >
                      <FontAwesomeIcon icon={faArrowDown} />
                    </button>
                  </div>

                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      onClick={() => handlePauseToggle(item)}
                      className={`p-1 rounded transition text-xs ${
                        isPaused
                          ? "text-green-400 hover:bg-green-500/10"
                          : "text-yellow-400 hover:bg-yellow-500/10"
                      }`}
                      title={isPaused ? "Resume publishing" : "Pause publishing"}
                      data-testid={`pause-toggle-${item.id}`}
                    >
                      <FontAwesomeIcon icon={isPaused ? faPlay : faPause} />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleRemove(item.id)}
                      className="p-1 rounded text-muted hover:text-red-400 hover:bg-red-500/10 transition text-xs"
                      title="Remove from queue"
                      data-testid={`remove-item-${item.id}`}
                    >
                      <FontAwesomeIcon icon={faTrash} />
                    </button>
                  </div>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};
