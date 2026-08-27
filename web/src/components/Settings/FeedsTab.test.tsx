import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { FeedsTab } from "./FeedsTab";
import * as feedsApi from "../../api/feeds";

vi.mock("../../api/feeds", () => ({
  fetchFeeds: vi.fn(),
  createFeed: vi.fn(),
  updateFeed: vi.fn(),
  deleteFeed: vi.fn(),
  triggerFeed: vi.fn(),
  discoverFeed: vi.fn(),
}));

describe("FeedsTab", () => {
  const mockFeeds: feedsApi.FeedSubscriptionDto[] = [
    {
      id: "feed-1",
      title: ".NET Blog",
      feedUrl: "https://devblogs.microsoft.com/dotnet/feed/",
      websiteUrl: "https://devblogs.microsoft.com/dotnet/",
      includeFilters: "csharp, dotnet",
      excludeFilters: "jobs",
      lastPolledAt: "2026-08-20T10:00:00Z",
      createdAt: "2026-08-20T09:00:00Z",
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue(mockFeeds);
  });

  it("renders feed subscriptions loaded on mount", async () => {
    render(<FeedsTab />);

    expect(screen.getByText(/Subscriptions/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.getByText(".NET Blog")).toBeInTheDocument();
      expect(screen.getByText("https://devblogs.microsoft.com/dotnet/feed/")).toBeInTheDocument();
      expect(screen.getByText("csharp, dotnet")).toBeInTheDocument();
      expect(screen.getByText("jobs")).toBeInTheDocument();
      expect(screen.getByText("Poll Now")).toBeInTheDocument();
    });
  });

  it("triggers polling on Poll Now click", async () => {
    vi.mocked(feedsApi.triggerFeed).mockResolvedValue();

    render(<FeedsTab />);

    await waitFor(() => {
      expect(screen.getByText("Poll Now")).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText("Poll Now"));

    await waitFor(() => {
      expect(feedsApi.triggerFeed).toHaveBeenCalledWith("feed-1");
    });
  });

  it("opens create modal when clicking New Feed and discovers feed", async () => {
    vi.mocked(feedsApi.discoverFeed).mockResolvedValue({
      success: true,
      feedUrl: "https://news.ycombinator.com/rss",
      title: "Hacker News",
      websiteUrl: "https://news.ycombinator.com",
    });

    render(<FeedsTab />);

    const newBtn = screen.getByRole("button", { name: /New Feed/i });
    fireEvent.click(newBtn);

    expect(screen.getByRole("heading", { name: "Add Feed Subscription" })).toBeInTheDocument();

    const urlInput = screen.getByPlaceholderText(/e\.g\. https:\/\/youtube\.com/i);
    fireEvent.change(urlInput, { target: { value: "https://news.ycombinator.com" } });

    const discoverBtn = screen.getByRole("button", { name: "Discover" });
    fireEvent.click(discoverBtn);

    await waitFor(() => {
      expect(feedsApi.discoverFeed).toHaveBeenCalledWith("https://news.ycombinator.com");
      expect(screen.getByDisplayValue("Hacker News")).toBeInTheDocument();
      expect(screen.getByDisplayValue("https://news.ycombinator.com/rss")).toBeInTheDocument();
    });
  });

  it("renders empty state when no feeds exist", async () => {
    vi.mocked(feedsApi.fetchFeeds).mockResolvedValue([]);

    render(<FeedsTab />);

    await waitFor(() => {
      expect(screen.getByText("No feed subscriptions yet")).toBeInTheDocument();
      expect(screen.getByText("Add First Feed")).toBeInTheDocument();
    });
  });
});
