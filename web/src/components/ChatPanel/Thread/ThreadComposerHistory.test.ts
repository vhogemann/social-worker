import { describe, expect, it } from "vitest";
import { navigatePromptHistory } from "./ThreadComposerHistory";

describe("navigatePromptHistory", () => {
  it("navigates up from current draft to latest history entry", () => {
    const result = navigatePromptHistory("up", {
      history: ["first", "second"],
      historyIndex: null,
      draftBuffer: "",
      composerText: "in-progress",
    });

    expect(result.consumed).toBe(true);
    expect(result.nextText).toBe("second");
    expect(result.nextHistoryIndex).toBe(1);
    expect(result.nextDraftBuffer).toBe("in-progress");
  });

  it("navigates down to restore draft buffer after newest history entry", () => {
    const result = navigatePromptHistory("down", {
      history: ["first", "second"],
      historyIndex: 1,
      draftBuffer: "in-progress",
      composerText: "second",
    });

    expect(result.consumed).toBe(true);
    expect(result.nextText).toBe("in-progress");
    expect(result.nextHistoryIndex).toBeNull();
  });

  it("does not consume when navigating empty history", () => {
    const result = navigatePromptHistory("up", {
      history: [],
      historyIndex: null,
      draftBuffer: "",
      composerText: "abc",
    });

    expect(result.consumed).toBe(false);
    expect(result.nextText).toBe("abc");
  });
});
