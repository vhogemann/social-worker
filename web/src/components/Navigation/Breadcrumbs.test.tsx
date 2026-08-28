import { describe, it, expect, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { Breadcrumbs } from "./Breadcrumbs";
import { useDraftStore } from "../../store/draftStore";
import { useWorkflowStore } from "../../store/workflowStore";
import { useTemplateEditorStore } from "../TemplateStudio/editor/templateEditorStore";

describe("Breadcrumbs", () => {
  beforeEach(() => {
    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Deep Dive into .NET 10",
          content: "Draft content",
          status: "Formatting",
          targetPlatform: null,
          canonicalDraftId: null,
          threads: [],
          variants: [
            {
              id: "var-tw-1",
              title: "Twitter Variant",
              content: "Twitter thread",
              status: "Formatting",
              targetPlatform: "Twitter",
              canonicalDraftId: "draft-1",
              threads: [],
              createdAt: "2026-08-20T10:00:00Z",
              updatedAt: "2026-08-20T10:00:00Z",
            },
          ],
          createdAt: "2026-08-20T10:00:00Z",
          updatedAt: "2026-08-20T10:00:00Z",
        },
      ],
      activeDraftId: "draft-1",
    });

    useWorkflowStore.setState({
      activeWorkflow: {
        id: "wf-1",
        title: "Daily Tech Highlights",
        instructionPrompt: "Prompt",
        sources: [],
        destinations: [],
        createdAt: "2026-08-20T10:00:00Z",
        updatedAt: "2026-08-20T10:00:00Z",
      },
    });

    useTemplateEditorStore.setState({
      name: "Announcement Banner",
    });
  });

  const renderWithRoute = (path: string) => {
    return render(
      <MemoryRouter initialEntries={[path]}>
        <Breadcrumbs />
      </MemoryRouter>
    );
  };

  it("renders Workflows parent link and workflow title for /workflows/:id", () => {
    renderWithRoute("/workflows/wf-1");
    expect(screen.getByRole("link", { name: "Workflows" })).toHaveAttribute("href", "/");
    expect(screen.getByText("Daily Tech Highlights")).toBeInTheDocument();
  });

  it("renders Workflows > WorkflowTitle > DraftTitle for /workflows/:wfId/draft/:id", () => {
    renderWithRoute("/workflows/wf-1/draft/draft-1");
    expect(screen.getByRole("link", { name: "Workflows" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Daily Tech Highlights" })).toHaveAttribute("href", "/workflows/wf-1");
    expect(screen.getByText("Deep Dive into .NET 10")).toBeInTheDocument();
  });

  it("renders 4-tier breadcrumbs for /workflows/:wfId/draft/:draftId/variant/:variantId", () => {
    renderWithRoute("/workflows/wf-1/draft/draft-1/variant/var-tw-1");
    expect(screen.getByRole("link", { name: "Workflows" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Daily Tech Highlights" })).toHaveAttribute("href", "/workflows/wf-1");
    expect(screen.getByRole("link", { name: "Deep Dive into .NET 10" })).toHaveAttribute(
      "href",
      "/workflows/wf-1/draft/draft-1"
    );
    expect(screen.getByText("Twitter")).toBeInTheDocument();
  });

  it("renders 3-tier breadcrumbs for /draft/:draftId/variant/:variantId", () => {
    renderWithRoute("/draft/draft-1/variant/var-tw-1");
    expect(screen.getByRole("link", { name: "Drafts" })).toHaveAttribute("href", "/drafts");
    expect(screen.getByRole("link", { name: "Deep Dive into .NET 10" })).toHaveAttribute("href", "/draft/draft-1");
    expect(screen.getByText("Twitter")).toBeInTheDocument();
  });

  it("renders Drafts and draft title for /draft/:id", () => {
    renderWithRoute("/draft/draft-1");
    expect(screen.getByRole("link", { name: "Drafts" })).toHaveAttribute("href", "/drafts");
    expect(screen.getByText("Deep Dive into .NET 10")).toBeInTheDocument();
  });

  it("renders Templates and template name for /templates/editor", () => {
    renderWithRoute("/templates/editor");
    expect(screen.getByRole("link", { name: "Templates" })).toHaveAttribute("href", "/templates");
    expect(screen.getByText("Announcement Banner")).toBeInTheDocument();
  });

  it("renders Settings parent link and tab label for /settings/feeds", () => {
    renderWithRoute("/settings/feeds");
    expect(screen.getByRole("link", { name: "Settings" })).toHaveAttribute("href", "/settings/account");
    expect(screen.getByText("Feeds")).toBeInTheDocument();
  });
});
