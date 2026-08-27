import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { DraftVariantItem } from "./DraftVariantItem";
import type { DraftDto } from "../../api/drafts";

const mockVariant: DraftDto = {
  id: "var-1",
  title: "Twitter Thread",
  content: "Adapted tweet content",
  status: "Draft",
  targetPlatform: "Bluesky",
  targetAccountHandle: "alice.bsky.social",
  canonicalDraftId: "draft-1",
  threads: [],
  createdAt: "2026-08-01T12:00:00Z",
  updatedAt: "2026-08-01T12:00:00Z"
};

describe("DraftVariantItem", () => {
  it("renders variant title and platform handle", () => {
    const onSelect = vi.fn();
    render(
      <DraftVariantItem
        variant={mockVariant}
        isActive={false}
        onSelect={onSelect}
      />
    );

    expect(screen.getByText("Twitter Thread")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();
    expect(screen.getByText("@alice.bsky.social")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button"));
    expect(onSelect).toHaveBeenCalledWith("var-1");
  });
});
