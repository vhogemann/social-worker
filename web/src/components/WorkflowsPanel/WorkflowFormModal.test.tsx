import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { WorkflowFormModal } from "./WorkflowFormModal";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AccountDto } from "../../api/accounts";

const mockFeeds: FeedSubscriptionDto[] = [
  {
    id: "feed-1",
    title: "AI Daily",
    feedUrl: "https://example.com/ai.xml",
    websiteUrl: "https://example.com",
    lastPolledAt: null,
    createdAt: "2026-08-01T12:00:00Z"
  }
];

const mockAccounts: AccountDto[] = [
  {
    id: "acc-1",
    platform: "Bluesky",
    handle: "alice.bsky.social",
    status: "Active",
    createdAt: "2026-08-01T12:00:00Z",
    updatedAt: "2026-08-01T12:00:00Z"
  }
];

describe("WorkflowFormModal", () => {
  it("renders and submits workflow creation data", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <WorkflowFormModal
        isOpen={true}
        editingWorkflow={null}
        availableFeeds={mockFeeds}
        connectedAccounts={mockAccounts}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    expect(screen.getByText("Create Automation Workflow")).toBeInTheDocument();
    const titleInput = screen.getByLabelText(/workflow title/i);
    fireEvent.change(titleInput, { target: { value: "Tech News Auto-Draft" } });

    const submitBtn = screen.getByRole("button", { name: /create workflow/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith({
        title: "Tech News Auto-Draft",
        instructionPrompt: "Summarize this article as an engaging thread.",
        feedSubscriptionIds: ["feed-1"],
        destinations: [
          {
            platform: "Bluesky",
            autoPublish: false,
            accountId: "acc-1",
            accountHandle: "alice.bsky.social"
          }
        ]
      });
      expect(handleClose).toHaveBeenCalled();
    });
  });
});
