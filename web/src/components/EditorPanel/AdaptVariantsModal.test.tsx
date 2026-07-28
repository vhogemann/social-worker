import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { useDraftStore } from "../../store/draftStore";
import * as draftsApi from "../../api/drafts";

vi.mock("../../api/drafts", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../api/drafts")>();
  return {
    ...actual,
    generateVariants: vi.fn(),
  };
});

describe("AdaptVariantsModal", () => {
  const loadDrafts = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
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

  it("generates variants for the selected target platforms", async () => {
    vi.mocked(draftsApi.generateVariants).mockResolvedValueOnce({
      variants: [
        { targetPlatform: "Twitter" },
        { targetPlatform: "LinkedIn" },
      ],
    } as any);

    render(<AdaptVariantsModal isOpen={true} onClose={vi.fn()} draftId="draft-1" />);

    expect(screen.getByText("From Bluesky to:")).toBeInTheDocument();
    expect(screen.queryByRole("checkbox", { name: /bluesky/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /generate variants/i }));

    await waitFor(() => {
      expect(draftsApi.generateVariants).toHaveBeenCalledWith("draft-1", ["Twitter", "LinkedIn"]);
    });

    await waitFor(() => {
      expect(screen.getByText(/Created 2 variant\(s\): Twitter, LinkedIn/)).toBeInTheDocument();
      expect(loadDrafts).toHaveBeenCalled();
    });
  });

  it("closes when the close button is clicked", () => {
    const onClose = vi.fn();

    render(<AdaptVariantsModal isOpen={true} onClose={onClose} draftId="draft-1" />);

    fireEvent.click(screen.getByRole("button", { name: /close/i }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});