import React from "react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorkflowImportModal } from "./WorkflowImportModal";
import * as workflowsApi from "../../api/workflows";

vi.mock("../../api/workflows", () => ({
  importWorkflows: vi.fn(),
}));

describe("WorkflowImportModal", () => {
  const samplePackage: workflowsApi.WorkflowExportPackage = {
    version: 1,
    exportedAt: "2026-10-19T10:00:00Z",
    workflows: [
      {
        title: "Daily Tech Highlights",
        instructionPrompt: "Summarize top tech news.",
        minRelevanceScore: 7,
        sources: [
          {
            kind: "Feed",
            feed: {
              title: "Hacker News",
              feedUrl: "https://news.ycombinator.com/rss",
            },
          },
        ],
        destinations: [
          {
            platform: "Bluesky",
            autoPublish: true,
            publishingMode: "Automatic",
          },
        ],
      },
    ],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("does not render when isOpen is false", () => {
    render(
      <WorkflowImportModal
        isOpen={false}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );
    expect(screen.queryByTestId("workflow-import-modal")).not.toBeInTheDocument();
  });

  it("renders upload UI and textarea when open", () => {
    render(
      <WorkflowImportModal
        isOpen={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    expect(screen.getByRole("heading", { name: "Import Workflows" })).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/Paste exported JSON here/i)).toBeInTheDocument();
    expect(screen.getByText(/Choose a JSON file/i)).toBeInTheDocument();
  });

  it("parses valid JSON text and displays preview", async () => {
    render(
      <WorkflowImportModal
        isOpen={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const textarea = screen.getByPlaceholderText(/Paste exported JSON here/i);
    fireEvent.change(textarea, { target: { value: JSON.stringify(samplePackage) } });

    await waitFor(() => {
      expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
      expect(screen.getByText("Package v1")).toBeInTheDocument();
      expect(screen.getByText("1 workflow found")).toBeInTheDocument();
    });
  });

  it("shows error for invalid JSON", async () => {
    render(
      <WorkflowImportModal
        isOpen={true}
        onClose={vi.fn()}
        onSuccess={vi.fn()}
      />
    );

    const textarea = screen.getByPlaceholderText(/Paste exported JSON here/i);
    fireEvent.change(textarea, { target: { value: "{ invalid json" } });

    await waitFor(() => {
      expect(screen.getByText(/Invalid JSON syntax/i)).toBeInTheDocument();
    });
  });

  it("submits import and displays success results", async () => {
    vi.mocked(workflowsApi.importWorkflows).mockResolvedValue({
      importedWorkflowsCount: 1,
      createdFeedsCount: 1,
      reusedFeedsCount: 0,
      importedWorkflowIds: ["wf-123"],
    });

    const handleSuccess = vi.fn();
    const handleClose = vi.fn();

    render(
      <WorkflowImportModal
        isOpen={true}
        onClose={handleClose}
        onSuccess={handleSuccess}
      />
    );

    const textarea = screen.getByPlaceholderText(/Paste exported JSON here/i);
    fireEvent.change(textarea, { target: { value: JSON.stringify(samplePackage) } });

    await waitFor(() => {
      expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
    });

    const importButton = screen.getByRole("button", { name: /Import 1 Workflow/i });
    fireEvent.click(importButton);

    await waitFor(() => {
      expect(workflowsApi.importWorkflows).toHaveBeenCalledWith(samplePackage);
      expect(screen.getByText(/Successfully imported 1 workflow/i)).toBeInTheDocument();
      expect(screen.getByText(/Created 1 new feed/i)).toBeInTheDocument();
      expect(handleSuccess).toHaveBeenCalled();
    });
  });
});
