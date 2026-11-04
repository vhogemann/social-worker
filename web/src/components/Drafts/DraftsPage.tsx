import React, { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import TextField from "@mui/material/TextField";
import InputAdornment from "@mui/material/InputAdornment";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import ToggleButton from "@mui/material/ToggleButton";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faMagnifyingGlass,
  faFileLines
} from "@fortawesome/free-solid-svg-icons";
import { useDraftStore } from "../../store/draftStore";
import { DraftCard } from "./DraftCard";
import { CreateDraftModal } from "../DraftList/CreateDraftModal";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { Pagination } from "../common/Pagination";

type FilterTab = "all" | "standalone" | "workflow" | "archived" | "discarded";

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
  const [page, setPage] = useState(1);
  const pageSize = 12;
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setPage(1);
  }, [activeTab, search]);

  useEffect(() => {
    if (activeTab === "discarded") {
      loadDrafts({ includeDiscarded: true, status: "Discarded" });
    } else {
      loadDrafts();
    }
  }, [loadDrafts, activeTab]);

  const handleSelectDraft = (id: string, workflowId?: string | null) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${id}`);
    } else {
      navigate(`/draft/${id}`);
    }
  };

  const handleSelectVariant = (
    canonicalId: string,
    variantId: string,
    workflowId?: string | null
  ) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${canonicalId}/variant/${variantId}`);
    } else {
      navigate(`/draft/${canonicalId}/variant/${variantId}`);
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
      if (activeTab === "discarded") {
        if (d.status !== "Discarded") return false;
      } else if (activeTab === "archived") {
        if (d.status !== "Archived") return false;
      } else {
        if (d.status === "Archived" || d.status === "Discarded") return false;
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

  const totalPages = Math.max(1, Math.ceil(filteredDrafts.length / pageSize));

  useEffect(() => {
    if (page > totalPages) {
      setPage(Math.max(1, totalPages));
    }
  }, [page, totalPages]);

  const paginatedDrafts = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filteredDrafts.slice(start, start + pageSize);
  }, [filteredDrafts, page, pageSize]);

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    if (typeof containerRef.current?.scrollTo === "function") {
      containerRef.current.scrollTo({ top: 0, behavior: "smooth" });
    }
  };

  return (
    <Box
      ref={containerRef}
      sx={{
        flex: 1,
        overflowY: "auto",
        bgcolor: "background.default",
        p: { xs: 3, md: 4 },
        display: "flex",
        flexDirection: "column",
        gap: 3
      }}
      data-testid="drafts-page"
    >
      {/* Header */}
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 2
        }}
      >
        <Box>
          <Typography variant="h5" sx={{ fontWeight: 700, color: "text.primary" }}>
            Drafts Library
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Browse, manage, and compose standalone and workflow-generated drafts.
          </Typography>
        </Box>
        <Button
          variant="primary"
          icon={faPlus}
          onClick={() => setCreateModalOpen(true)}
        >
          New Draft
        </Button>
      </Box>

      {/* Filter and Search Bar */}
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", md: "row" },
          alignItems: { xs: "stretch", md: "center" },
          justifyContent: "space-between",
          gap: 2,
          pb: 2,
          borderBottom: 1,
          borderColor: "divider"
        }}
      >
        <ToggleButtonGroup
          value={activeTab}
          exclusive
          onChange={(_, newTab) => {
            if (newTab) setActiveTab(newTab);
          }}
          size="small"
          sx={{ flexWrap: "wrap" }}
        >
          <ToggleButton value="all" sx={{ textTransform: "none", fontWeight: 600, px: 2 }}>
            All Active
          </ToggleButton>
          <ToggleButton value="standalone" sx={{ textTransform: "none", fontWeight: 600, px: 2 }}>
            Standalone
          </ToggleButton>
          <ToggleButton value="workflow" sx={{ textTransform: "none", fontWeight: 600, px: 2 }}>
            Workflow Generated
          </ToggleButton>
          <ToggleButton value="archived" sx={{ textTransform: "none", fontWeight: 600, px: 2 }}>
            Archived
          </ToggleButton>
          <ToggleButton value="discarded" sx={{ textTransform: "none", fontWeight: 600, px: 2 }}>
            Discarded
          </ToggleButton>
        </ToggleButtonGroup>

        <TextField
          size="small"
          placeholder="Search drafts..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <FontAwesomeIcon icon={faMagnifyingGlass} className="text-muted text-xs" />
                </InputAdornment>
              )
            }
          }}
          sx={{ width: { xs: "100%", md: 280 } }}
        />
      </Box>

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
        <Box sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "1fr",
                md: "repeat(2, 1fr)",
                lg: "repeat(3, 1fr)"
              },
              gap: 3
            }}
          >
            {paginatedDrafts.map((draft) => (
              <DraftCard
                key={draft.id}
                draft={draft}
                onSelect={handleSelectDraft}
                onSelectVariant={handleSelectVariant}
                onArchive={archiveDraft}
                onUnarchive={unarchiveDraft}
                onDelete={deleteDraft}
                onRename={(id, title) => updateDraftTitle(id, title)}
              />
            ))}
          </Box>
          <Pagination
            currentPage={page}
            totalPages={totalPages}
            totalItems={filteredDrafts.length}
            onPageChange={handlePageChange}
            testId="drafts-pagination"
          />
        </Box>
      )}

      <CreateDraftModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onCreate={handleCreateDraft}
      />
    </Box>
  );
};
