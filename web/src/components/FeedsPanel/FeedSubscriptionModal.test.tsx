import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { FeedSubscriptionModal } from "./FeedSubscriptionModal";

describe("FeedSubscriptionModal", () => {
  it("renders create form and submits new subscription data", async () => {
    const handleSave = vi.fn().mockResolvedValue(undefined);
    const handleClose = vi.fn();

    render(
      <FeedSubscriptionModal
        isOpen={true}
        editingFeed={null}
        onClose={handleClose}
        onSave={handleSave}
      />
    );

    expect(screen.getByText("Add Feed Subscription")).toBeInTheDocument();

    const titleInput = screen.getByLabelText(/feed title/i);
    const urlInput = screen.getByLabelText(/feed url/i);

    fireEvent.change(titleInput, { target: { value: "My RSS Feed" } });
    fireEvent.change(urlInput, { target: { value: "https://example.com/rss.xml" } });

    const submitBtn = screen.getByRole("button", { name: /create subscription/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(handleSave).toHaveBeenCalledWith({
        title: "My RSS Feed",
        feedUrl: "https://example.com/rss.xml",
        websiteUrl: undefined,
        includeFilters: undefined,
        excludeFilters: undefined
      });
      expect(handleClose).toHaveBeenCalled();
    });
  });
});
