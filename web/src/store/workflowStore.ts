import { create } from "zustand";
import type { AutomationWorkflowDto } from "../api/workflows";

interface WorkflowState {
  activeWorkflow: AutomationWorkflowDto | null;
  setActiveWorkflow: (wf: AutomationWorkflowDto | null) => void;
}

export const useWorkflowStore = create<WorkflowState>((set) => ({
  activeWorkflow: null,
  setActiveWorkflow: (activeWorkflow) => set({ activeWorkflow }),
}));
