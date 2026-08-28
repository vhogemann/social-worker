import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { MemoryRouter } from "react-router-dom";
import { DraftVariantTabs } from "./DraftVariantTabs";
import type { DraftDto } from "../../api/drafts";

const mockNavigate = vi.fn();
vi.mock("react-router-dom", async () => {
  const actual = await vi.importActual("react-router-dom");
  return {
    ...actual,
    useNavigate: () => mockNavigate,
  };
});

describe("DraftVariantTabs", () => {
  const mockVariants: DraftDto[] = [
    {
      id: "var-bsky-1",
      title: "Bluesky Variant",
      content: "Bluesky content",
      status: "Editing",
      targetPlatform: "Bluesky",
      canonicalDraftId: "canonical-1",
      threads: [],
      createdAt: "2026-08-27T10:00:00Z",
      updatedAt: "2026-08-27T10:00:00Z",
    },
    {
      id: "var-tw-2",
      title: "Twitter Variant",
      content: "Twitter content",
      status: "Editing",
      targetPlatform: "Twitter",
      canonicalDraftId: "canonical-1",
      threads: [],
      createdAt: "2026-08-27T10:00:00Z",
      updatedAt: "2026-08-27T10:00:00Z",
    },
  ];

  it("renders canonical tab and all platform variant tabs", () => {
    render(
      <MemoryRouter>
        <DraftVariantTabs
          workflowId="wf-1"
          canonicalDraftId="canonical-1"
          activeDraftId="canonical-1"
          variants={mockVariants}
          onOpenAdaptModal={vi.fn()}
        />
      </MemoryRouter>
    );

    expect(screen.getByTestId("tab-canonical")).toBeInTheDocument();
    expect(screen.getByText("Canonical")).toBeInTheDocument();
    expect(screen.getByTestId("tab-variant-var-bsky-1")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByTestId("tab-variant-var-tw-2")).toBeInTheDocument();
    expect(screen.getByText("Twitter")).toBeInTheDocument();
    expect(screen.getByText("Adapt")).toBeInTheDocument();
  });

  it("navigates to variant route on variant tab click", () => {
    mockNavigate.mockClear();
    render(
      <MemoryRouter>
        <DraftVariantTabs
          workflowId="wf-1"
          canonicalDraftId="canonical-1"
          activeDraftId="canonical-1"
          variants={mockVariants}
          onOpenAdaptModal={vi.fn()}
        />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByTestId("tab-variant-var-tw-2"));
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/canonical-1/variant/var-tw-2");
  });

  it("navigates to canonical route on canonical tab click", () => {
    mockNavigate.mockClear();
    render(
      <MemoryRouter>
        <DraftVariantTabs
          workflowId="wf-1"
          canonicalDraftId="canonical-1"
          activeDraftId="var-tw-2"
          variants={mockVariants}
          onOpenAdaptModal={vi.fn()}
        />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByTestId("tab-canonical"));
    expect(mockNavigate).toHaveBeenCalledWith("/workflows/wf-1/draft/canonical-1");
  });

  it("calls onOpenAdaptModal when Adapt button is clicked", () => {
    const onOpenAdaptModal = vi.fn();
    render(
      <MemoryRouter>
        <DraftVariantTabs
          canonicalDraftId="canonical-1"
          activeDraftId="canonical-1"
          variants={mockVariants}
          onOpenAdaptModal={onOpenAdaptModal}
        />
      </MemoryRouter>
    );

    fireEvent.click(screen.getByText("Adapt"));
    expect(onOpenAdaptModal).toHaveBeenCalledTimes(1);
  });
});
