import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ThreadMessageToolActivityCall, getToolActivityLabel } from "./ThreadMessageToolActivityCall";

const mockSetPanelMode = vi.fn();

vi.mock("../../Composer/ComposerDraftContext", () => ({
  useComposerDraft: () => ({
    activeDraftId: "d1",
    setPanelMode: mockSetPanelMode,
  }),
}));

describe("ThreadMessageToolActivityCall", () => {
  beforeEach(() => {
    mockSetPanelMode.mockClear();
  });

  it("returns friendly labels for known tools", () => {
    expect(getToolActivityLabel("web_search")).toBe("web search");
    expect(getToolActivityLabel("validate_draft")).toBe("draft validated");
    expect(getToolActivityLabel("set_bluesky_reply_target")).toBe("reply target set");
  });

  it("falls back to underscore replacement for unknown tools", () => {
    expect(getToolActivityLabel("custom_tool_name")).toBe("custom tool name");
  });

  it("renders inline activity pill with tool label", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "validate_draft" }));
    expect(screen.getByTestId("thread-tool-activity")).toBeInTheDocument();
    expect(screen.getByText("draft validated")).toBeInTheDocument();
  });

  it("renders search query details from args", () => {
    render(
      React.createElement(ThreadMessageToolActivityCall, {
        name: "web_search",
        args: { query: "black hole discoveries" },
      })
    );
    expect(screen.getByText("web search")).toBeInTheDocument();
    expect(screen.getByText('"black hole discoveries"')).toBeInTheDocument();
  });

  it("renders running indicator when status is running", () => {
    render(
      React.createElement(ThreadMessageToolActivityCall, {
        name: "fetch_source",
        status: { type: "running" },
      })
    );
    expect(screen.getByText("running...")).toBeInTheDocument();
  });

  it("renders failure indicator on error status or result error", () => {
    render(
      React.createElement(ThreadMessageToolActivityCall, {
        name: "publish",
        status: { type: "error" },
      })
    );
    expect(screen.getByText("failed")).toBeInTheDocument();
  });

  it("switches editor to preview when publish activity renders", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "publish" }));
    expect(mockSetPanelMode).toHaveBeenCalledWith("preview");
  });
});
