import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { useDraftStore } from "../../store/draftStore";
import * as draftsApi from "../../api/drafts";
import * as accountsApi from "../../api/accounts";

vi.mock("../../api/drafts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../api/drafts")>();
  return {
    ...actual,
    generateVariants: vi.fn(),
  };
});

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
}));

function renderModal(props: { isOpen: boolean; onClose: () => void; draftId: string | null }) {
  return render(
    <MemoryRouter>
      <AdaptVariantsModal {...props} />
    </MemoryRouter>
  );
}

describe("AdaptVariantsModal", () => {
  const loadDrafts = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      { id: "acc-1", platform: "Bluesky", handle: "user.bsky.social", status: "Active", createdAt: "", updatedAt: "" },
      { id: "acc-2", platform: "Twitter", handle: "@user", status: "Active", createdAt: "", updatedAt: "" },
      { id: "acc-3", platform: "LinkedIn", handle: "user-li", status: "Active", createdAt: "", updatedAt: "" },
    ]);

    useDraftStore.setState({
      drafts: [
        {
          id: "draft-1",
          title: "Draft",
          status: "Draft",
          content: "Thread body",
          targetPlatform: "Bluesky",
          canonicalDraftId: null,
          threads: [],
          mediaAssets: [],
          createdAt: "2026-07-10T00:00:00Z",
          updatedAt: "2026-07-10T00:00:00Z",
        },
      ],
      loadDrafts,
    });
  });

  it("generates variants for the selected configured target accounts", async () => {
    vi.mocked(draftsApi.generateVariants).mockResolvedValueOnce({
      variants: [
        { targetPlatform: "Twitter", targetAccountHandle: "user" },
        { targetPlatform: "LinkedIn", targetAccountHandle: "user-li" },
      ],
    } as any);

    renderModal({ isOpen: true, onClose: vi.fn(), draftId: "draft-1" });

    expect(screen.getByText(/From Bluesky to:/)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText("@user")).toBeInTheDocument();
      expect(screen.getByText("@user-li")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /generate variants/i }));

    await waitFor(() => {
      expect(draftsApi.generateVariants).toHaveBeenCalledWith("draft-1", undefined, ["acc-2", "acc-3"]);
    });

    await waitFor(() => {
      expect(screen.getByText(/Created 2 variant\(s\): Twitter \(@user\), LinkedIn \(@user-li\)/)).toBeInTheDocument();
      expect(loadDrafts).toHaveBeenCalled();
    });
  });

  it("shows empty state when no other accounts are configured", async () => {
    vi.mocked(accountsApi.listAccounts).mockResolvedValue([
      { id: "acc-1", platform: "Bluesky", handle: "user.bsky.social", status: "Active", createdAt: "", updatedAt: "" },
    ]);

    renderModal({ isOpen: true, onClose: vi.fn(), draftId: "draft-1" });

    await waitFor(() => {
      expect(screen.getByText(/no other social accounts configured/i)).toBeInTheDocument();
      expect(screen.getByRole("link", { name: /configure connections/i })).toHaveAttribute("href", "/settings/connections");
    });

    expect(screen.getByRole("button", { name: /generate variants/i })).toBeDisabled();
  });

  it("closes when the close button is clicked", async () => {
    const onClose = vi.fn();

    renderModal({ isOpen: true, onClose, draftId: "draft-1" });

    await waitFor(() => {
      expect(screen.getByRole("button", { name: /close/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole("button", { name: /close/i }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});