import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { MastodonThreadPreview, calculateMastodonLength } from "./MastodonThreadPreview";
import { useDraftStore } from "../../../store/draftStore";

vi.mock("./PreviewCard", () => ({
  PreviewCard: ({ content, index, postUrl }: { content: string; index: number; postUrl?: string }) => (
    <div data-testid={`preview-card-${index}`}>
      <span>{content}</span>
      <span>{postUrl ?? "no-post-url"}</span>
    </div>
  ),
}));

describe("MastodonThreadPreview", () => {
  beforeEach(() => {
    useDraftStore.setState({
      activeDraftId: "draft-1",
      drafts: [
        {
          id: "draft-1",
          title: "Mastodon draft",
          status: "Draft",
          content: "Hello Mastodon",
          targetPlatform: "Mastodon",
          canonicalDraftId: null,
          threads: [],
          mediaAssets: [],
          createdAt: "2026-07-10T00:00:00Z",
          updatedAt: "2026-07-10T00:00:00Z",
        },
      ],
    });
  });

  it("counts URLs as 23 characters", () => {
    const longUrl = "https://example.com/very/long/path/that/exceeds/twenty/three/characters";
    expect(calculateMastodonLength(`Check ${longUrl}`)).toBe(6 + 23);
  });

  it("renders mastodon preview container and warns when over 500 characters", () => {
    const overLimit = "a".repeat(505);
    render(<MastodonThreadPreview content={overLimit} />);

    expect(screen.getByTestId("mastodon-preview")).toBeInTheDocument();
    expect(screen.getByText(/Exceeds 500-character Mastodon limit \(505\/500\)/)).toBeInTheDocument();
  });
});
