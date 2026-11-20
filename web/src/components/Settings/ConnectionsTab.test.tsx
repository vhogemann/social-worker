import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { ConnectionsTab } from "./ConnectionsTab";

vi.mock("../../api/accounts", () => ({
  listAccounts: vi.fn(),
  saveAccount: vi.fn(),
  deleteAccount: vi.fn(),
  updateAccountTags: vi.fn(),
  getLinkedInAuthUrl: vi.fn(),
  getMetaThreadsAuthUrl: vi.fn(),
  getTwitterAuthUrl: vi.fn(),
  getInstagramAuthUrl: vi.fn(),
  getMastodonAuthUrl: vi.fn(),
  connectGitHubBlog: vi.fn(),
  getGitHubBlogSettings: vi.fn(),
  updateGitHubBlogSettings: vi.fn(),
}));

import { listAccounts, saveAccount, deleteAccount, updateAccountTags, getLinkedInAuthUrl, getMetaThreadsAuthUrl, connectGitHubBlog, getGitHubBlogSettings, updateGitHubBlogSettings } from "../../api/accounts";
const mockListAccounts = listAccounts as Mock;
const mockSaveAccount = saveAccount as Mock;
const mockDeleteAccount = deleteAccount as Mock;
const mockUpdateAccountTags = updateAccountTags as Mock;
const mockGetLinkedInAuthUrl = getLinkedInAuthUrl as Mock;
const mockGetMetaThreadsAuthUrl = getMetaThreadsAuthUrl as Mock;
const mockConnectGitHubBlog = connectGitHubBlog as Mock;
const mockGetGitHubBlogSettings = getGitHubBlogSettings as Mock;
const mockUpdateGitHubBlogSettings = updateGitHubBlogSettings as Mock;

const makeAccount = (id = "a1") => ({
  id,
  platform: "Bluesky",
  handle: "alice.bsky.social",
  status: "Active",
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
});

describe("ConnectionsTab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("confirm", () => true);
    mockListAccounts.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("lists accounts loaded on mount", async () => {
    mockListAccounts.mockResolvedValueOnce([makeAccount("a1")]);
    render(<ConnectionsTab />);

    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
  });

  it("shows error when loading accounts fails", async () => {
    mockListAccounts.mockRejectedValueOnce(new Error("Network error"));
    render(<ConnectionsTab />);

    expect(await screen.findByText("Network error")).toBeInTheDocument();
  });

  it("opens modal and does not submit when required fields are empty", async () => {
    render(<ConnectionsTab />);
    
    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    expect(screen.getByText("Add Social Connection")).toBeInTheDocument();

    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));
    await waitFor(() => expect(mockSaveAccount).not.toHaveBeenCalled());
  });

  it("saves account and refreshes list on valid submit", async () => {
    mockListAccounts
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([makeAccount("a2")]);
    mockSaveAccount.mockResolvedValueOnce(undefined);
    render(<ConnectionsTab />);

    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    await userEvent.type(screen.getByPlaceholderText("e.g. user.bsky.social"), "alice.bsky.social");
    await userEvent.type(screen.getByPlaceholderText("••••••••••••"), "app-pw");
    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    await waitFor(() => expect(mockSaveAccount).toHaveBeenCalledWith({
      platform: "Bluesky",
      handle: "alice.bsky.social",
      appPassword: "app-pw",
      tags: []
    }));
    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
  });

  it("deletes account and refreshes list", async () => {
    mockListAccounts
      .mockResolvedValueOnce([makeAccount("a1")])
      .mockResolvedValueOnce([]);
    mockDeleteAccount.mockResolvedValueOnce(undefined);
    render(<ConnectionsTab />);

    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(mockDeleteAccount).toHaveBeenCalledWith("a1"));
    await waitFor(() => expect(screen.queryByText(/alice\.bsky\.social/)).not.toBeInTheDocument());
  });

  it("shows error when saveAccount fails", async () => {
    mockSaveAccount.mockRejectedValueOnce(new Error("Failed to save connection"));
    render(<ConnectionsTab />);

    // Open modal
    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    await userEvent.type(screen.getByPlaceholderText("e.g. user.bsky.social"), "x");
    await userEvent.type(screen.getByPlaceholderText("••••••••••••"), "y");
    fireEvent.submit(screen.getByRole("button", { name: /save connection/i }));

    expect(await screen.findByText("Failed to save connection")).toBeInTheDocument();
  });

  it("renders Company Page badge and Personal badge for LinkedIn accounts", async () => {
    mockListAccounts.mockResolvedValueOnce([
      {
        id: "li-1",
        platform: "LinkedIn",
        handle: "urn:li:person:user123",
        status: "Active",
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
      {
        id: "li-2",
        platform: "LinkedIn",
        handle: "Acme Corp (urn:li:organization:998877)",
        status: "Active",
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
    ]);
    render(<ConnectionsTab />);

    expect(await screen.findByText("Personal")).toBeInTheDocument();
    expect(await screen.findByText("Company Page")).toBeInTheDocument();
    expect(screen.getByText(/urn:li:person:user123/)).toBeInTheDocument();
    expect(screen.getByText(/Acme Corp/)).toBeInTheDocument();
  });

  it("initiates personal LinkedIn connection flow", async () => {
    mockGetLinkedInAuthUrl.mockResolvedValueOnce({ authUrl: "https://linkedin.com/auth?personal" });
    render(<ConnectionsTab />);

    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    const select = screen.getByDisplayValue("Bluesky");
    fireEvent.change(select, { target: { value: "LinkedIn" } });

    const connectBtn = screen.getByRole("button", { name: /connect personal profile/i });
    fireEvent.click(connectBtn);

    await waitFor(() => {
      expect(mockGetLinkedInAuthUrl).toHaveBeenCalledWith("personal");
    });
  });

  it("initiates company LinkedIn connection flow when Company Page is selected", async () => {
    mockGetLinkedInAuthUrl.mockResolvedValueOnce({ authUrl: "https://linkedin.com/auth?company" });
    render(<ConnectionsTab />);

    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    const select = screen.getByDisplayValue("Bluesky");
    fireEvent.change(select, { target: { value: "LinkedIn" } });

    const companyTypeBtn = screen.getByRole("button", { name: /company page/i });
    fireEvent.click(companyTypeBtn);

    const connectBtn = screen.getByRole("button", { name: /connect company page/i });
    fireEvent.click(connectBtn);

    await waitFor(() => {
      expect(mockGetLinkedInAuthUrl).toHaveBeenCalledWith("company");
    });
  });

  it("initiates Threads reconnection flow when Reconnect button is clicked on Threads account card", async () => {
    mockListAccounts.mockResolvedValueOnce([
      {
        id: "th-1",
        platform: "MetaThreads",
        handle: "my_threads_handle",
        status: "Active",
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
    ]);
    mockGetMetaThreadsAuthUrl.mockResolvedValueOnce({ authUrl: "https://threads.net/oauth/authorize?test" });
    render(<ConnectionsTab />);

    expect(await screen.findByText(/my_threads_handle/)).toBeInTheDocument();
    const reconnectBtn = screen.getByRole("button", { name: /reconnect/i });
    fireEvent.click(reconnectBtn);

    await waitFor(() => {
      expect(mockGetMetaThreadsAuthUrl).toHaveBeenCalledTimes(1);
    });
  });

  it("displays DEFAULT badge for default connections and opens tag editor", async () => {
    mockListAccounts.mockResolvedValueOnce([
      {
        id: "a-default",
        platform: "Bluesky",
        handle: "alice.bsky.social",
        status: "Active",
        tags: ["default", "personal"],
        createdAt: "2026-01-01",
        updatedAt: "2026-01-01",
      },
    ]);
    mockUpdateAccountTags.mockResolvedValueOnce({ id: "a-default", tags: ["default", "personal", "news"] });

    render(<ConnectionsTab />);

    expect(await screen.findByText(/alice\.bsky\.social/)).toBeInTheDocument();
    expect(screen.getByTestId("default-badge-a-default")).toHaveTextContent("DEFAULT");
    expect(screen.getByText("personal")).toBeInTheDocument();

    // Click Tags button
    const tagsBtn = screen.getByTestId("edit-tags-btn-a-default");
    fireEvent.click(tagsBtn);

    expect(screen.getByText("Edit Connection Tags: @alice.bsky.social")).toBeInTheDocument();

    // Add tag
    const input = screen.getByTestId("tag-text-input");
    fireEvent.change(input, { target: { value: "news" } });
    fireEvent.keyDown(input, { key: "Enter" });

    // Save
    const saveBtn = screen.getByTestId("save-tags-submit-btn");
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(mockUpdateAccountTags).toHaveBeenCalledWith("a-default", {
        tags: ["default", "personal", "news"],
      });
    });
  });

  it("connects a GitHub Blog repository via connectGitHubBlog", async () => {
    mockListAccounts
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([
        {
          id: "gh-1",
          platform: "GitHubBlog",
          handle: "alice/alice.github.io",
          status: "Active",
          createdAt: "2026-11-19",
          updatedAt: "2026-11-19",
        },
      ]);
    mockConnectGitHubBlog.mockResolvedValueOnce({
      externalId: "999",
      handle: "alice/alice.github.io",
      branch: "main",
      preset: "jekyll",
    });

    render(<ConnectionsTab />);

    const addBtn = await screen.findByRole("button", { name: /\+? ?add connection/i });
    fireEvent.click(addBtn);

    fireEvent.change(screen.getByRole("combobox"), { target: { value: "GitHubBlog" } });

    await userEvent.type(screen.getByPlaceholderText("username/username.github.io"), "alice/alice.github.io");
    await userEvent.type(screen.getByPlaceholderText("github_pat_..."), "github_pat_123");
    await userEvent.type(screen.getByPlaceholderText("https://user.github.io"), "https://alice.github.io");

    fireEvent.submit(screen.getByTestId("github-blog-connect-form"));

    await waitFor(() => {
      expect(mockConnectGitHubBlog).toHaveBeenCalledWith(
        expect.objectContaining({
          owner: "alice",
          repo: "alice.github.io",
          accessToken: "github_pat_123",
          siteBaseUrl: "https://alice.github.io",
          preset: "jekyll",
        })
      );
    });
  });

  it("edits an existing GitHub Blog connection without requiring a new PAT", async () => {
    const ghAccount = {
      id: "gh-1",
      platform: "GitHubBlog",
      handle: "alice/alice.github.io",
      status: "Active",
      createdAt: "2026-11-19",
      updatedAt: "2026-11-19",
    };
    mockListAccounts.mockResolvedValue([ghAccount]);
    mockGetGitHubBlogSettings.mockResolvedValueOnce({
      owner: "alice",
      repo: "alice.github.io",
      branch: "main",
      preset: "jekyll",
      siteRootDirectory: "",
      postsDirectory: "_posts",
      assetsDirectory: "assets/images/posts",
      siteBaseUrl: "https://alice.github.io",
      permalinkFormat: "/{year}/{month}/{day}/{slug}/",
      defaultLayout: "post",
      author: "",
      defaultCategories: [],
    });
    mockUpdateGitHubBlogSettings.mockResolvedValueOnce({
      owner: "alice",
      repo: "alice.github.io",
      branch: "main",
      preset: "jekyll",
      siteRootDirectory: "docs",
      postsDirectory: "_posts",
      assetsDirectory: "assets/images/posts",
      siteBaseUrl: "https://alice.github.io",
      permalinkFormat: "/{year}/{month}/{day}/{slug}.html",
      defaultLayout: "post",
      author: "",
      defaultCategories: [],
    });

    render(<ConnectionsTab />);

    const editBtn = await screen.findByTestId("edit-github-blog-btn-gh-1");
    fireEvent.click(editBtn);

    const rootInput = await screen.findByPlaceholderText(/leave blank for repo root/i);
    await userEvent.type(rootInput, "docs");

    fireEvent.submit(screen.getByTestId("github-blog-connect-form"));

    await waitFor(() => {
      expect(mockUpdateGitHubBlogSettings).toHaveBeenCalledWith(
        "gh-1",
        expect.objectContaining({
          owner: "alice",
          repo: "alice.github.io",
          accessToken: undefined,
          siteRootDirectory: "docs",
        })
      );
    });
  });
});
