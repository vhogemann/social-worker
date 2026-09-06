import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faLayerGroup,
  faPenNib,
  faPlus,
  faSpinner
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchWorkflows,
  createWorkflow,
  updateWorkflow,
  deleteWorkflow,
  triggerWorkflow,
  type AutomationWorkflowDto
} from "../../api/workflows";
import { fetchFeeds, type FeedSubscriptionDto } from "../../api/feeds";
import { listAccounts, type AccountDto } from "../../api/accounts";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { AlertBanner } from "../common/AlertBanner";
import { ConfirmModal } from "../common/ConfirmModal";
import { WorkflowCard } from "./WorkflowCard";
import { WorkflowFormModal, type WorkflowFormData } from "./WorkflowFormModal";

export const WorkflowsPage: React.FC = () => {
  const navigate = useNavigate();
  const [workflows, setWorkflows] = useState<AutomationWorkflowDto[]>([]);
  const [availableFeeds, setAvailableFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [connectedAccounts, setConnectedAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);

  // Modal form & delete confirmation state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingWorkflow, setEditingWorkflow] = useState<AutomationWorkflowDto | null>(null);
  const [deletingWorkflowId, setDeletingWorkflowId] = useState<string | null>(null);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [wfList, feedList, accountsList] = await Promise.all([
        fetchWorkflows(),
        fetchFeeds(),
        listAccounts().catch(() => [] as AccountDto[])
      ]);
      setWorkflows(wfList);
      setAvailableFeeds(feedList);
      setConnectedAccounts(accountsList);
    } catch (err: any) {
      setError(err?.message || "Failed to load automation workflows.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadData();
  }, []);

  const handleOpenCreateModal = () => {
    setEditingWorkflow(null);
    setIsModalOpen(true);
  };

  const handleOpenEditModal = (wf: AutomationWorkflowDto) => {
    setEditingWorkflow(wf);
    setIsModalOpen(true);
  };

  const handleSaveWorkflow = async (data: WorkflowFormData) => {
    setError(null);
    const payload = {
      title: data.title,
      instructionPrompt: data.instructionPrompt,
      feedSubscriptionIds: data.feedSubscriptionIds,
      destinations: data.destinations.map((d) => ({
        platform: d.platform,
        autoPublish: d.autoPublish,
        publishingMode: d.publishingMode,
        scheduleId: d.scheduleId,
        accountId: d.accountId,
        accountHandle: d.accountHandle
      }))
    };

    try {
      if (editingWorkflow) {
        await updateWorkflow(editingWorkflow.id, payload);
      } else {
        await createWorkflow(payload);
      }
      setIsModalOpen(false);
      setEditingWorkflow(null);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to save workflow.");
      throw err;
    }
  };

  const handleConfirmDelete = async () => {
    if (!deletingWorkflowId) return;
    try {
      await deleteWorkflow(deletingWorkflowId);
      setDeletingWorkflowId(null);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to delete workflow.");
    }
  };

  const handleTrigger = async (id: string) => {
    setTriggeringId(id);
    setError(null);
    try {
      await triggerWorkflow(id);
      await loadData();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger workflow execution.");
    } finally {
      setTriggeringId(null);
    }
  };

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto" data-testid="workflows-dashboard">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Top Header */}
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div>
            <h1 className="text-lg font-bold text-foreground">Automation Workflows</h1>
            <p className="text-[11px] text-muted font-mono">
              Autonomous multi-source ingestion, 2-tier generation, proofreader validation, and multi-platform publishing
            </p>
          </div>
          <div className="flex gap-2">
            <Link
              to="/composer"
              className="px-3 py-1.5 rounded-lg border border-border bg-panel hover:bg-bg text-muted hover:text-foreground text-xs font-mono flex items-center gap-1.5 transition"
            >
              <FontAwesomeIcon icon={faPenNib} />
              <span>Ad-hoc Composer</span>
            </Link>
            <Button
              variant="primary"
              size="sm"
              icon={faPlus}
              onClick={handleOpenCreateModal}
            >
              New Workflow
            </Button>
          </div>
        </div>

        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {/* Workflows Grid */}
        {loading && workflows.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : workflows.length === 0 ? (
          <EmptyState
            icon={faLayerGroup}
            title="No automation workflows yet"
            description="Create a workflow to connect RSS/YouTube feeds to automated 2-tier thread generation and multi-platform publishing."
            action={
              <Button variant="primary" size="sm" onClick={handleOpenCreateModal}>
                Create First Workflow
              </Button>
            }
          />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {workflows.map((wf) => (
              <WorkflowCard
                key={wf.id}
                workflow={wf}
                isTriggering={triggeringId === wf.id}
                onClick={(id) => navigate(`/workflows/${id}`)}
                onTrigger={handleTrigger}
                onEdit={handleOpenEditModal}
                onDelete={(id) => setDeletingWorkflowId(id)}
              />
            ))}
          </div>
        )}

        {/* Workflow Create / Edit Modal */}
        <WorkflowFormModal
          isOpen={isModalOpen}
          editingWorkflow={editingWorkflow}
          availableFeeds={availableFeeds}
          connectedAccounts={connectedAccounts}
          onClose={() => {
            setIsModalOpen(false);
            setEditingWorkflow(null);
          }}
          onSave={handleSaveWorkflow}
        />

        {/* Delete Confirmation Modal */}
        <ConfirmModal
          isOpen={Boolean(deletingWorkflowId)}
          title="Delete Workflow"
          message="Are you sure you want to delete this workflow? Generated drafts will not be deleted."
          confirmLabel="Delete"
          onConfirm={handleConfirmDelete}
          onCancel={() => setDeletingWorkflowId(null)}
        />
      </div>
    </div>
  );
};

export { WorkflowsPage as WorkflowsPanel };
