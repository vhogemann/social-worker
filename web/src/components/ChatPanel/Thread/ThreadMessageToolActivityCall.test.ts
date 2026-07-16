import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ThreadMessageToolActivityCall, getToolActivityLabel } from "./ThreadMessageToolActivityCall";
import { useEditorStore } from "../../../store/editorStore";
import { useChatStore } from "../../../store/chatStore";
import { useDraftStore } from "../../../store/draftStore";

vi.mock("@assistant-ui/react", () => ({
  useMessage: (selector: (m: { id: string }) => unknown) => selector({ id: "m-1" }),
}));

describe("getToolActivityLabel", () => {
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
    useEditorStore.setState({ panelMode: "edit" });

    render(React.createElement(ThreadMessageToolActivityCall, { name: "publish" }));

    expect(useEditorStore.getState().panelMode).toBe("preview");
  });

  it("does not add top-rail activity card for validate_draft", () => {
    useDraftStore.setState({ activeDraftId: "d1" });
    useChatStore.setState({
      activityCardsByDraft: {},
      addActivityCard: useChatStore.getState().addActivityCard,
    });

    render(React.createElement(ThreadMessageToolActivityCall, { name: "validate_draft" }));

    const cards = useChatStore.getState().activityCardsByDraft["d1"];
    expect(cards).toBeUndefined();
  });

  it("adds a unified activity card for non-validation tool execution", () => {
    useDraftStore.setState({ activeDraftId: "d1" });
    useChatStore.setState({
      activityCardsByDraft: {},
      addActivityCard: useChatStore.getState().addActivityCard,
    });

    render(React.createElement(ThreadMessageToolActivityCall, { name: "web_search" }));

    const cards = useChatStore.getState().activityCardsByDraft["d1"];
    expect(cards).toBeDefined();
    expect(cards[cards.length - 1].title).toBe("web search");
  });
});
