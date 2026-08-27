import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { FeedSubscriptionCard } from "./FeedSubscriptionCard";
import { type FeedSubscriptionDto } from "../../api/feeds";

const mockFeed: FeedSubscriptionDto = {
  id: "feed-1",
  title: "Tech News",
  feedUrl: "https://example.com/rss",
  websiteUrl: "https://example.com",
  includeFilters: "tech,ai",
  excludeFilters: "crypto",
  lastPolledAt: "2026-08-01T12:00:00Z",
  createdAt: "2026-08-01T10:00:00Z"
};

describe("FeedSubscriptionCard", () => {
  it("renders subscription details and triggers actions", () => {
    const handleTrigger = vi.fn();
    const handleEdit = vi.fn();
    const handleDelete = vi.fn();

    render(
      <FeedSubscriptionCard
        feed={mockFeed}
        onTrigger={handleTrigger}
        onEdit={handleEdit}
        onDelete={handleDelete}
      />
    );

    expect(screen.getByText("Tech News")).toBeInTheDocument();
    expect(screen.getByText("https://example.com/rss")).toBeInTheDocument();
    expect(screen.getByText("tech,ai")).toBeInTheDocument();
    expect(screen.getByText("crypto")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /poll now/i }));
    expect(handleTrigger).toHaveBeenCalledWith("feed-1");

    fireEvent.click(screen.getByRole("button", { name: /edit/i }));
    expect(handleEdit).toHaveBeenCalledWith(mockFeed);

    fireEvent.click(screen.getByLabelText(/delete subscription/i));
    expect(handleDelete).toHaveBeenCalledWith("feed-1");
  });
});
