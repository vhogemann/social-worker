import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ThreadMessageToolActivityCall, getToolActivityLabel } from "./ThreadMessageToolActivityCall";

const mockAddActivityCard = vi.fn();
const mockSetPanelMode = vi.fn();

vi.mock("../../Composer/ComposerDraftContext", () => ({
  useComposerDraft: () => ({
    activeDraftId: "d1",
    addActivityCard: mockAddActivityCard,
    setPanelMode: mockSetPanelMode,
  }),
}));

vi.mock("@assistant-ui/react", () => ({
  useMessage: (selector: (m: { id: string }) => unknown) => selector({ id: "m-1" }),
}));

describe("getToolActivityLabel", () => {
  beforeEach(() => {
    mockAddActivityCard.mockClear();
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

  it("renders compact activity card text", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "validate_draft" }));
    expect(screen.queryByText("activity: draft validated")).not.toBeInTheDocument();
  });

  it("switches editor to preview when publish activity renders", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "publish" }));
    expect(mockSetPanelMode).toHaveBeenCalledWith("preview");
  });

  it("does not add top-rail activity card for validate_draft", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "validate_draft" }));
    expect(mockAddActivityCard).not.toHaveBeenCalled();
  });

  it("adds a unified activity card for non-validation tool execution", () => {
    render(React.createElement(ThreadMessageToolActivityCall, { name: "web_search" }));
    expect(mockAddActivityCard).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "web search",
        message: "web search executed.",
        kind: "info",
      })
    );
  });
});
