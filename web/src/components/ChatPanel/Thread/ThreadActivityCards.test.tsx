import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { ThreadActivityCards } from "./ThreadActivityCards";

describe("ThreadActivityCards", () => {
  it("renders activity cards for errors", () => {
    render(
      <ThreadActivityCards
        cards={[
          {
            id: "c1",
            title: "publish failed",
            message: "Post exceeds 300 character limit.",
            kind: "error",
            createdAt: "2026-07-16T00:00:00Z",
          },
        ]}
      />
    );

    expect(screen.getByText("activity: publish failed")).toBeInTheDocument();
    expect(screen.getByText("Post exceeds 300 character limit.")).toBeInTheDocument();
  });

  it("renders nothing when there are no cards", () => {
    const { container } = render(<ThreadActivityCards cards={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
