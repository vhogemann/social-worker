import React, { useEffect, useState } from "react";
import { Link, useParams, useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner } from "@fortawesome/free-solid-svg-icons";
import {
  fetchWorkflow,
  fetchWorkflowDrafts,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
  type AutomationWorkflowDto,
  type WorkflowDraftSummaryDto
} from "../../api/workflows";
import { fetchFeeds, type FeedSubscriptionDto } from "../../api/feeds";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { WorkflowExecutionHistoryModal } from "./WorkflowExecutionHistoryModal";
import { WorkflowDetailHeader } from "./WorkflowDetailHeader";
import { WorkflowDraftsList } from "./WorkflowDraftsList";
import { WorkflowFormModal, type WorkflowFormData } from "../WorkflowsPanel/WorkflowFormModal";
import { AlertBanner } from "../common/AlertBanner";
import { ConfirmModal } from "../common/ConfirmModal";

export const WorkflowDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [workflow, setWorkflow] = useState<AutomationWorkflowDto | null>(null);
  const [drafts, setDrafts] = useState<WorkflowDraftSummaryDto[]>([]);
  const [availableFeeds, setAvailableFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [connectedAccounts, setConnectedAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [triggering, setTriggering] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);

  const loadData = async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const [wf, draftList, feedList, accountsList] = await Promise.all([
        fetchWorkflow(id),
        fetchWorkflowDrafts(id),
        fetchFeeds().catch(() => [] as FeedSubscriptionDto[]),
        listAccounts().catch(() => [] as AccountDto[])
      ]);
      setWorkflow(wf);
      setDrafts(draftList);
      setAvailableFeeds(feedList);
      setConnectedAccounts(accountsList);
    } catch (err: any) {
      setError(err?.message || "Failed to load workflow details.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, [id]);

  const handleSaveWorkflow = async (data: WorkflowFormData) => {
    if (!id) return;
    const payload = {
      title: data.title,
      instructionPrompt: data.instructionPrompt,
      feedSubscriptionIds: data.feedSubscriptionIds,
      destinations: data.destinations.map((d) => ({
        platform: d.platform,
        autoPublish: d.autoPublish,
        accountId: d.accountId,
        accountHandle: d.accountHandle
      }))
    };

    try {
      await updateWorkflow(id, payload);
      setIsEditModalOpen(false);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to update workflow.");
      throw err;
    }
  };

  const handleTrigger = async () => {
    if (!id) return;
    setTriggering(true);
    setError(null);
    try {
      await triggerWorkflow(id);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger workflow.");
    } finally {
      setTriggering(false);
    }
  };

  const handleConfirmDelete = async () => {
    if (!id) return;
    try {
      await deleteWorkflow(id);
      setIsDeleteModalOpen(false);
      navigate("/");
    } catch (err: any) {
      setError(err?.message || "Failed to delete workflow.");
    }
  };

  if (loading && !workflow) {
    return (
      <div className="flex flex-col h-full bg-bg items-center justify-center text-muted">
        <FontAwesomeIcon icon={faSpinner} spin className="text-2xl" />
        <span className="text-xs font-mono mt-3">Loading workflow...</span>
      </div>
    );
  }

  if (!workflow) {
    return (
      <div className="flex flex-col h-full bg-bg items-center justify-center p-6 text-center">
        <h2 className="text-base font-bold text-foreground mb-2">Workflow not found</h2>
        <p className="text-xs text-muted mb-4">The workflow you requested does not exist or has been deleted.</p>
        <Link to="/" className="px-4 py-2 rounded-lg bg-accent text-accent-foreground text-xs font-semibold">
          Back to Workflows
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto" data-testid="workflow-detail-page">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Summary Header */}
        <WorkflowDetailHeader
          workflow={workflow}
          isTriggering={triggering}
          onTrigger={handleTrigger}
          onOpenHistory={() => setIsHistoryModalOpen(true)}
          onOpenEdit={() => setIsEditModalOpen(true)}
          onDelete={() => setIsDeleteModalOpen(true)}
        />

        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {/* Main Content: Generated Drafts Grid */}
        <WorkflowDraftsList
          workflowId={workflow.id}
          drafts={drafts}
          isTriggering={triggering}
          onTrigger={handleTrigger}
        />

        {/* Edit Modal (shared with WorkflowsPanel) */}
        <WorkflowFormModal
          isOpen={isEditModalOpen}
          editingWorkflow={workflow}
          availableFeeds={availableFeeds}
          connectedAccounts={connectedAccounts}
          onClose={() => setIsEditModalOpen(false)}
          onSave={handleSaveWorkflow}
        />

        {/* Execution History Modal */}
        <WorkflowExecutionHistoryModal
          isOpen={isHistoryModalOpen}
          workflowId={workflow.id}
          workflowTitle={workflow.title}
          onClose={() => setIsHistoryModalOpen(false)}
        />

        {/* Delete Confirmation Modal */}
        <ConfirmModal
          isOpen={isDeleteModalOpen}
          title="Delete Workflow"
          message="Are you sure you want to delete this workflow? Generated drafts will not be deleted."
          confirmLabel="Delete"
          onConfirm={handleConfirmDelete}
          onCancel={() => setIsDeleteModalOpen(false)}
        />
      </div>
    </div>
  );
};
