import { beforeEach, describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { GitHubBlogPreview, parseBlogDocument } from "./GitHubBlogPreview";
import { useDraftStore } from "../../../store/draftStore";

describe("GitHubBlogPreview", () => {
  beforeEach(() => {
    useDraftStore.setState({
      activeDraftId: "draft-blog-1",
      drafts: [
        {
          id: "draft-blog-1",
          title: "Fallback Blog Title",
          status: "Editing",
          content: "",
          targetPlatform: "GitHubBlog",
          targetAccountHandle: "alice/alice.github.io",
          canonicalDraftId: null,
          threads: [
            {
              id: "thread-blog-1",
              draftId: "draft-blog-1",
              platform: "GitHubBlog",
              stage: "Draft",
              content: "",
              posts: [],
            },
          ],
          mediaAssets: [],
          createdAt: "2026-11-19T00:00:00Z",
          updatedAt: "2026-11-19T00:00:00Z",
        },
      ],
    });
  });

  it("renders frontmatter metadata, prose, and word count", () => {
    const md = `---
title: Shipping Long-Form Posts
tags: [jekyll, github]
categories: [engineering]
layout: post
---

## Architecture

This is a **full** article with a horizontal rule.

---

Second section.`;

    render(<GitHubBlogPreview content={md} />);

    expect(screen.getByTestId("github-blog-preview")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1, name: "Shipping Long-Form Posts" })).toBeInTheDocument();
    expect(screen.getByText("jekyll")).toBeInTheDocument();
    expect(screen.getByText("engineering")).toBeInTheDocument();
    expect(screen.getByText(/min read/i)).toBeInTheDocument();
    expect(screen.getByText("Second section.")).toBeInTheDocument();
  });

  it("promotes leading H1 when frontmatter is omitted", () => {
    const parsed = parseBlogDocument("# Direct Heading\n\nParagraph text.");
    expect(parsed.title).toBe("Direct Heading");
    expect(parsed.body).toBe("Paragraph text.");
  });

  it("renders unclosed frontmatter warning", () => {
    render(<GitHubBlogPreview content={"---\ntitle: Unclosed\n\nBody"} />);
    expect(screen.getByTestId("github-blog-frontmatter-error")).toBeInTheDocument();
  });

  it("renders published banner when post URL exists", () => {
    useDraftStore.setState((s) => ({
      ...s,
      drafts: s.drafts.map((d) =>
        d.id === "draft-blog-1"
          ? {
              ...d,
              threads: [
                {
                  id: "thread-blog-1",
                  draftId: "draft-blog-1",
                  platform: "GitHubBlog",
                  stage: "Sent",
                  content: "# Live Post\n\nHello",
                  posts: [
                    {
                      id: "p1",
                      platformThreadId: "thread-blog-1",
                      segmentIndex: 0,
                      platform: "GitHubBlog",
                      url: "https://alice.github.io/2026/11/19/live-post/",
                    },
                  ],
                },
              ],
            }
          : d
      ),
    }));

    render(<GitHubBlogPreview content={"# Live Post\n\nHello"} />);
    const banner = screen.getByTestId("github-blog-published-banner");
    expect(banner).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /open post/i })).toHaveAttribute(
      "href",
      "https://alice.github.io/2026/11/19/live-post/"
    );
  });
});
