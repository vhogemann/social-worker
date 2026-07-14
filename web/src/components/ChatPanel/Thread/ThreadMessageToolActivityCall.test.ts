import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ThreadMessageToolActivityCall, getToolActivityLabel } from "./ThreadMessageToolActivityCall";

describe("getToolActivityLabel", () => {
  it("returns friendly labels for known tools", () => {
    expect(getToolActivityLabel("web_search")).toBe("web search");
    expect(getToolActivityLabel("validate_draft")).toBe("draft validated");
  });

  it("falls back to underscore replacement for unknown tools", () => {
    expect(getToolActivityLabel("custom_tool_name")).toBe("custom tool name");
  });

  it("renders compact activity card text", () => {
    render(ThreadMessageToolActivityCall({ name: "validate_draft" }));
    expect(screen.getByText("activity: draft validated")).toBeInTheDocument();
  });
});
