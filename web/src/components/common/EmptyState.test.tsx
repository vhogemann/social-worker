import React from "react";
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { EmptyState } from "./EmptyState";
import { faRss } from "@fortawesome/free-solid-svg-icons";

describe("EmptyState", () => {
  it("renders title, description and action", () => {
    render(
      <EmptyState
        icon={faRss}
        title="No Feeds Found"
        description="Add a feed subscription to start."
        action={<button>Add Feed</button>}
      />
    );

    expect(screen.getByText("No Feeds Found")).toBeInTheDocument();
    expect(screen.getByText("Add a feed subscription to start.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add Feed" })).toBeInTheDocument();
  });
});
