import React from "react";
import { Modal } from "../common/Modal";
import { WorkflowForm, type WorkflowFormData } from "./WorkflowForm";
import { type AutomationWorkflowDto } from "../../api/workflows";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AccountDto } from "../../api/accounts";

export type { WorkflowFormData };

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
  const handleSave = async (data: WorkflowFormData) => {
    await onSave(data);
    onClose();
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingWorkflow ? "Edit Automation Workflow" : "Create Automation Workflow"}
      maxWidth="2xl"
    >
      <WorkflowForm
        editingWorkflow={editingWorkflow}
        availableFeeds={availableFeeds}
        availableWorkflows={availableWorkflows}
        connectedAccounts={connectedAccounts}
        onCancel={onClose}
        onSave={handleSave}
      />
    </Modal>
  );
};
