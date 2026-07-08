import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { StageStepper } from "../components/EditorPanel/StageStepper";
import { useDraftStore } from "../store/draftStore";

describe("StageStepper component", () => {
  beforeEach(() => {
    useDraftStore.setState({
      activeDraftId: "1",
      activePlatform: "Bluesky",
      drafts: [
        {
          id: "1",
          title: "Test Draft",
          status: "Editing",
          content: "some text",
          threads: [
            {
              id: "t1",
              draftId: "1",
              platform: "Bluesky",
              stage: "Draft",
              content: "some text",
            },
          ],
          createdAt: "",
          updatedAt: "",
        },
      ],
    });
  });

  it("renders 3 stages when thread variant exists", () => {
    render(<StageStepper />);
    expect(screen.getByText("Draft")).toBeInTheDocument();
    expect(screen.getByText("Ready")).toBeInTheDocument();
    expect(screen.getByText("Sent")).toBeInTheDocument();
  });

  it("calls updateThreadStage when clicking a stage", async () => {
    const updateThreadStageSpy = vi.fn();
    useDraftStore.setState({ updateThreadStage: updateThreadStageSpy });

    render(<StageStepper />);
    
    const readyButton = screen.getByText("Ready").closest("button");
    expect(readyButton).toBeDefined();
    if (readyButton) {
      fireEvent.click(readyButton);
    }
    
    expect(updateThreadStageSpy).toHaveBeenCalledWith("1", "t1", "Ready");
  });

  it("renders activate variant block when thread variant does not exist", () => {
    useDraftStore.setState({ activePlatform: "Twitter" });
    render(<StageStepper />);

    expect(screen.getByText("No thread active for Twitter.")).toBeInTheDocument();
    expect(screen.getByText("Activate variant")).toBeInTheDocument();
  });
});
