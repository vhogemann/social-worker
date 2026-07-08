import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { ThreadPreview } from "../components/EditorPanel/ThreadPreview";

describe("ThreadPreview component", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    Object.assign(navigator, {
      clipboard: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
    });
  });

  it("renders multiple cards split by ---", () => {
    const text = "Post 1 content\n---\nPost 2 content";
    render(<ThreadPreview content={text} />);

    expect(screen.getByText("Post 1 content")).toBeInTheDocument();
    expect(screen.getByText("Post 2 content")).toBeInTheDocument();
    expect(screen.getByText("1/2")).toBeInTheDocument();
    expect(screen.getByText("2/2")).toBeInTheDocument();
  });

  it("copies segment text when clicking copy button", async () => {
    const text = "Copy me segment";
    render(<ThreadPreview content={text} />);

    const copyBtn = screen.getByText("Copy");
    fireEvent.click(copyBtn);

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith("Copy me segment");
    
    expect(await screen.findByText("Copied")).toBeInTheDocument();
  });
});
