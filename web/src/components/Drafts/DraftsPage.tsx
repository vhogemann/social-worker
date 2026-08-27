import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faMagnifyingGlass,
  faFileLines,
} from "@fortawesome/free-solid-svg-icons";
import { useDraftStore } from "../../store/draftStore";
import { DraftCard } from "./DraftCard";
import { CreateDraftModal } from "../DraftList/CreateDraftModal";
import { Button } from "../common/Button";
import { Input } from "../common/Input";
import { EmptyState } from "../common/EmptyState";

type FilterTab = "all" | "standalone" | "workflow" | "archived";

export const DraftsPage: React.FC = () => {
  const navigate = useNavigate();
  const drafts = useDraftStore((s) => s.drafts);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const createDraft = useDraftStore((s) => s.createDraft);
  const archiveDraft = useDraftStore((s) => s.archiveDraft);
  const unarchiveDraft = useDraftStore((s) => s.unarchiveDraft);
  const deleteDraft = useDraftStore((s) => s.deleteDraft);
  const updateDraftTitle = useDraftStore((s) => s.updateDraftTitle);

  const [search, setSearch] = useState("");
  const [activeTab, setActiveTab] = useState<FilterTab>("all");
  const [createModalOpen, setCreateModalOpen] = useState(false);

  useEffect(() => {
    loadDrafts();
  }, [loadDrafts]);

  const handleSelectDraft = (id: string, workflowId?: string | null) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${id}`);
    } else {
      navigate(`/draft/${id}`);
    }
  };

  const handleCreateDraft = async (
    title?: string,
    targetPlatform?: string,
    targetAccountId?: string
  ) => {
    const draft = await createDraft(title, undefined, targetPlatform, targetAccountId);
    navigate(`/draft/${draft.id}`);
  };

  const filteredDrafts = useMemo(() => {
    return drafts.filter((d) => {
      if (d.status === "Deleted") return false;

      // Tab filter
      if (activeTab === "archived") {
        if (d.status !== "Archived") return false;
      } else {
        if (d.status === "Archived") return false;
        if (activeTab === "standalone" && d.automationWorkflowId) return false;
        if (activeTab === "workflow" && !d.automationWorkflowId) return false;
      }

      // Search filter
      if (search.trim()) {
        const q = search.toLowerCase();
        const matchesTitle = d.title.toLowerCase().includes(q);
        const matchesContent = d.content?.toLowerCase().includes(q) ?? false;
        const matchesWorkflow = d.automationWorkflowTitle?.toLowerCase().includes(q) ?? false;
        return matchesTitle || matchesContent || matchesWorkflow;
      }

      return true;
    });
  }, [drafts, activeTab, search]);

  return (
    <div className="flex-1 overflow-y-auto bg-bg p-8 flex flex-col gap-6" data-testid="drafts-page">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold text-foreground">Drafts Library</h1>
          <p className="text-sm text-muted">
            Browse, manage, and compose standalone and workflow-generated drafts.
          </p>
        </div>
        <Button
          variant="primary"
          icon={faPlus}
          onClick={() => setCreateModalOpen(true)}
        >
          New Draft
        </Button>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-center justify-between gap-4 pb-2 border-b border-border">
        <div className="flex items-center gap-1 bg-panel border border-border rounded-lg p-1 w-full sm:w-auto text-xs font-medium">
          <button
            onClick={() => setActiveTab("all")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "all"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            All Active
          </button>
          <button
            onClick={() => setActiveTab("standalone")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "standalone"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            Standalone
          </button>
          <button
            onClick={() => setActiveTab("workflow")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "workflow"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            Workflow Generated
          </button>
          <button
            onClick={() => setActiveTab("archived")}
            className={`px-3 py-1.5 rounded-md transition ${
              activeTab === "archived"
                ? "bg-border text-foreground font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            Archived
          </button>
        </div>

        <div className="w-full sm:w-72">
          <Input
            placeholder="Search drafts..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
      </div>

      {/* Grid of Draft Cards */}
      {filteredDrafts.length === 0 ? (
        <EmptyState
          icon={faFileLines}
          title={search ? "No matching drafts" : "No drafts found"}
          description={
            search
              ? "Try adjusting your search query or filter."
              : activeTab === "archived"
              ? "You have no archived drafts."
              : "Create a new draft or trigger an automated workflow to get started."
          }
          action={
            !search && activeTab !== "archived" ? (
              <Button
                variant="primary"
                icon={faPlus}
                onClick={() => setCreateModalOpen(true)}
              >
                Create Your First Draft
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredDrafts.map((draft) => (
            <DraftCard
              key={draft.id}
              draft={draft}
              onSelect={handleSelectDraft}
              onArchive={archiveDraft}
              onUnarchive={unarchiveDraft}
              onDelete={deleteDraft}
              onRename={(id, title) => updateDraftTitle(id, title)}
            />
          ))}
        </div>
      )}

      <CreateDraftModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onCreate={handleCreateDraft}
      />
    </div>
  );
};
