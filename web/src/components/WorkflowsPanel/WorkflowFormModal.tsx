import React, { useState, useEffect } from "react";
import { Modal } from "../common/Modal";
import { Input } from "../common/Input";
import { Button } from "../common/Button";
import { WorkflowSourcesSelector } from "./WorkflowSourcesSelector";
import {
  WorkflowDestinationsSelector,
  type WorkflowDestinationItem,
  type PublishingMode
} from "./WorkflowDestinationsSelector";
import {
  type AutomationWorkflowDto,
  type WorkflowSourceKind,
  type CreateWorkflowSourceRequest
} from "../../api/workflows";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AccountDto } from "../../api/accounts";
import { fetchSchedules, type ScheduleSummaryDto } from "../../api/schedules";

export interface WorkflowFormData {
  title: string;
  instructionPrompt?: string;
  feedSubscriptionIds?: string[];
  sources?: CreateWorkflowSourceRequest[];
  destinations: {
    platform: string;
    autoPublish: boolean;
    publishingMode?: PublishingMode;
    scheduleId?: string | null;
    accountId?: string;
    accountHandle?: string;
  }[];
  scheduleId?: string | null;
}

export interface WorkflowFormModalProps {
  isOpen: boolean;
  editingWorkflow: AutomationWorkflowDto | null;
  availableFeeds: FeedSubscriptionDto[];
  availableWorkflows?: AutomationWorkflowDto[];
  connectedAccounts: AccountDto[];
  onClose: () => void;
  onSave: (data: WorkflowFormData) => Promise<void>;
}

export const WorkflowFormModal: React.FC<WorkflowFormModalProps> = ({
  isOpen,
  editingWorkflow,
  availableFeeds,
  availableWorkflows = [],
  connectedAccounts,
  onClose,
  onSave
}) => {
  const [title, setTitle] = useState("");
  const [instructionPrompt, setInstructionPrompt] = useState(
    "Summarize this article as an engaging thread."
  );
  const [sourceKind, setSourceKind] = useState<WorkflowSourceKind>("Feed");
  const [selectedFeedIds, setSelectedFeedIds] = useState<string[]>([]);
  const [selectedSourceWorkflowIds, setSelectedSourceWorkflowIds] = useState<string[]>([]);
  const [lookbackDays, setLookbackDays] = useState(7);
  const [destinations, setDestinations] = useState<WorkflowDestinationItem[]>([]);
  const [schedules, setSchedules] = useState<ScheduleSummaryDto[]>([]);
  const [workflowScheduleId, setWorkflowScheduleId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    fetchSchedules()
      .then(setSchedules)
      .catch(() => setSchedules([]));
  }, []);

  useEffect(() => {
    if (editingWorkflow) {
      setTitle(editingWorkflow.title);
      setInstructionPrompt(editingWorkflow.instructionPrompt || "");
      setWorkflowScheduleId(editingWorkflow.scheduleId || null);

      const hasHighlights = editingWorkflow.sources.some((s) => s.kind === "WorkflowHighlights");
      if (hasHighlights) {
        setSourceKind("WorkflowHighlights");
        const hlSources = editingWorkflow.sources.filter((s) => s.kind === "WorkflowHighlights");
        const wfIds: string[] = [];
        let days = 7;
        for (const s of hlSources) {
          if (s.sourceWorkflowId) wfIds.push(s.sourceWorkflowId);
          if (s.configurationJson) {
            try {
              const parsed = JSON.parse(s.configurationJson);
              if (parsed.lookbackDays) days = parsed.lookbackDays;
              if (Array.isArray(parsed.sourceWorkflowIds)) {
                wfIds.push(...parsed.sourceWorkflowIds);
              }
            } catch {}
          }
        }
        setSelectedSourceWorkflowIds(Array.from(new Set(wfIds)));
        setLookbackDays(days);
        setSelectedFeedIds([]);
      } else {
        setSourceKind("Feed");
        setSelectedFeedIds(
          editingWorkflow.sources
            .map((s) => s.feedSubscriptionId)
            .filter((id): id is string => Boolean(id))
        );
        setSelectedSourceWorkflowIds([]);
        setLookbackDays(7);
      }

      setDestinations(
        editingWorkflow.destinations.map((d) => ({
          platform: d.platform,
          autoPublish: d.autoPublish,
          publishingMode: d.publishingMode || (d.autoPublish ? "Automatic" : "Manual"),
          scheduleId: d.scheduleId,
          accountId: d.accountId ?? undefined,
          accountHandle: d.accountHandle ?? undefined
        }))
      );
    } else {
      setTitle("");
      setInstructionPrompt("Summarize this article as an engaging thread.");
      setSourceKind("Feed");
      setSelectedFeedIds(availableFeeds.map((f) => f.id));
      setSelectedSourceWorkflowIds([]);
      setLookbackDays(7);
      setWorkflowScheduleId(null);
      if (connectedAccounts.length > 0) {
        setDestinations(
          connectedAccounts.map((a) => ({
            platform: a.platform,
            autoPublish: false,
            publishingMode: "Manual",
            accountId: a.id,
            accountHandle: a.handle
          }))
        );
      } else {
        setDestinations([{ platform: "Bluesky", autoPublish: false, publishingMode: "Manual" }]);
      }
    }
  }, [editingWorkflow, availableFeeds, connectedAccounts, isOpen]);

  const handleToggleFeed = (feedId: string) => {
    setSelectedFeedIds((prev) =>
      prev.includes(feedId) ? prev.filter((id) => id !== feedId) : [...prev, feedId]
    );
  };

  const handleToggleSourceWorkflow = (workflowId: string) => {
    setSelectedSourceWorkflowIds((prev) =>
      prev.includes(workflowId) ? prev.filter((id) => id !== workflowId) : [...prev, workflowId]
    );
  };

  const handleToggleDestination = (
    platform: string,
    accountId?: string,
    accountHandle?: string
  ) => {
    setDestinations((prev) => {
      const exists = prev.some((d) =>
        accountId ? d.accountId === accountId : d.platform === platform
      );
      if (exists) {
        return prev.filter((d) =>
          accountId ? d.accountId !== accountId : d.platform !== platform
        );
      } else {
        return [
          ...prev,
          {
            platform,
            autoPublish: false,
            publishingMode: "Manual",
            accountId,
            accountHandle
          }
        ];
      }
    });
  };

  const handleChangePublishingMode = (
    platform: string,
    mode: PublishingMode,
    scheduleId?: string,
    accountId?: string
  ) => {
    setDestinations((prev) =>
      prev.map((d) => {
        const matches = accountId ? d.accountId === accountId : d.platform === platform;
        if (!matches) return d;
        return {
          ...d,
          publishingMode: mode,
          autoPublish: mode === "Automatic",
          scheduleId:
            mode === "Scheduled"
              ? scheduleId || (schedules.length > 0 ? schedules[0].id : null)
              : null
        };
      })
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;

    setSaving(true);
    try {
      let sourcesPayload: CreateWorkflowSourceRequest[] = [];
      if (sourceKind === "Feed") {
        sourcesPayload = selectedFeedIds.map((id) => ({
          kind: "Feed" as const,
          feedSubscriptionId: id
        }));
      } else {
        sourcesPayload = selectedSourceWorkflowIds.map((wfId) => ({
          kind: "WorkflowHighlights" as const,
          sourceWorkflowId: wfId,
          configurationJson: JSON.stringify({ lookbackDays, sourceWorkflowIds: [wfId] })
        }));
      }

      await onSave({
        title: title.trim(),
        instructionPrompt: instructionPrompt.trim() || undefined,
        sources: sourcesPayload,
        destinations,
        scheduleId: workflowScheduleId
      });
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingWorkflow ? "Edit Automation Workflow" : "Create Automation Workflow"}
      maxWidth="2xl"
    >
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <Input
          required
          label="Workflow Title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="e.g. Daily Tech Highlights / RSS to Bluesky"
        />

        <div>
          <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
            Custom Instruction Prompt (Optional)
          </label>
          <textarea
            rows={3}
            value={instructionPrompt}
            onChange={(e) => setInstructionPrompt(e.target.value)}
            placeholder="Instructions for drafting the thread..."
            className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent"
          />
        </div>

        <WorkflowSourcesSelector
          sourceKind={sourceKind}
          onSourceKindChange={setSourceKind}
          availableFeeds={availableFeeds}
          selectedFeedIds={selectedFeedIds}
          onToggleFeed={handleToggleFeed}
          availableWorkflows={availableWorkflows}
          selectedSourceWorkflowIds={selectedSourceWorkflowIds}
          onToggleSourceWorkflow={handleToggleSourceWorkflow}
          lookbackDays={lookbackDays}
          onLookbackDaysChange={setLookbackDays}
        />

        <WorkflowDestinationsSelector
          connectedAccounts={connectedAccounts}
          destinations={destinations}
          availableSchedules={schedules}
          onToggleDestination={handleToggleDestination}
          onChangePublishingMode={handleChangePublishingMode}
        />

        <div className="flex justify-end gap-2 border-t border-border pt-4 mt-2">
          <Button variant="secondary" size="sm" onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button type="submit" variant="primary" size="sm" loading={saving}>
            {editingWorkflow ? "Save Changes" : "Create Workflow"}
          </Button>
        </div>
      </form>
    </Modal>
  );
};
