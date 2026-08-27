import React, { useState, useEffect } from "react";
import { Modal } from "../common/Modal";
import { Input } from "../common/Input";
import { Button } from "../common/Button";
import { WorkflowSourcesSelector } from "./WorkflowSourcesSelector";
import {
  WorkflowDestinationsSelector,
  type WorkflowDestinationItem
} from "./WorkflowDestinationsSelector";
import { type AutomationWorkflowDto } from "../../api/workflows";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AccountDto } from "../../api/accounts";

export interface WorkflowFormData {
  title: string;
  instructionPrompt?: string;
  feedSubscriptionIds: string[];
  destinations: {
    platform: string;
    autoPublish: boolean;
    accountId?: string;
    accountHandle?: string;
  }[];
}

export interface WorkflowFormModalProps {
  isOpen: boolean;
  editingWorkflow: AutomationWorkflowDto | null;
  availableFeeds: FeedSubscriptionDto[];
  connectedAccounts: AccountDto[];
  onClose: () => void;
  onSave: (data: WorkflowFormData) => Promise<void>;
}

export const WorkflowFormModal: React.FC<WorkflowFormModalProps> = ({
  isOpen,
  editingWorkflow,
  availableFeeds,
  connectedAccounts,
  onClose,
  onSave
}) => {
  const [title, setTitle] = useState("");
  const [instructionPrompt, setInstructionPrompt] = useState(
    "Summarize this article as an engaging thread."
  );
  const [selectedFeedIds, setSelectedFeedIds] = useState<string[]>([]);
  const [destinations, setDestinations] = useState<WorkflowDestinationItem[]>([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (editingWorkflow) {
      setTitle(editingWorkflow.title);
      setInstructionPrompt(editingWorkflow.instructionPrompt || "");
      setSelectedFeedIds(editingWorkflow.sources.map((s) => s.feedSubscriptionId));
      setDestinations(
        editingWorkflow.destinations.map((d) => ({
          platform: d.platform,
          autoPublish: d.autoPublish,
          accountId: d.accountId ?? undefined,
          accountHandle: d.accountHandle ?? undefined
        }))
      );
    } else {
      setTitle("");
      setInstructionPrompt("Summarize this article as an engaging thread.");
      setSelectedFeedIds(availableFeeds.map((f) => f.id));
      if (connectedAccounts.length > 0) {
        setDestinations(
          connectedAccounts.map((a) => ({
            platform: a.platform,
            autoPublish: false,
            accountId: a.id,
            accountHandle: a.handle
          }))
        );
      } else {
        setDestinations([{ platform: "Bluesky", autoPublish: false }]);
      }
    }
  }, [editingWorkflow, availableFeeds, connectedAccounts, isOpen]);

  const handleToggleFeed = (feedId: string) => {
    setSelectedFeedIds((prev) =>
      prev.includes(feedId) ? prev.filter((id) => id !== feedId) : [...prev, feedId]
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
        return [...prev, { platform, autoPublish: false, accountId, accountHandle }];
      }
    });
  };

  const handleToggleAutoPublish = (platform: string, accountId?: string) => {
    setDestinations((prev) =>
      prev.map((d) => {
        const matches = accountId ? d.accountId === accountId : d.platform === platform;
        return matches ? { ...d, autoPublish: !d.autoPublish } : d;
      })
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;

    setSaving(true);
    try {
      await onSave({
        title: title.trim(),
        instructionPrompt: instructionPrompt.trim() || undefined,
        feedSubscriptionIds: selectedFeedIds,
        destinations
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
          availableFeeds={availableFeeds}
          selectedFeedIds={selectedFeedIds}
          onToggleFeed={handleToggleFeed}
        />

        <WorkflowDestinationsSelector
          connectedAccounts={connectedAccounts}
          destinations={destinations}
          onToggleDestination={handleToggleDestination}
          onToggleAutoPublish={handleToggleAutoPublish}
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
