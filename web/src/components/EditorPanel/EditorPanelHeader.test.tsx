import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { EditorPanelHeader } from "./EditorPanelHeader";

describe("EditorPanelHeader", () => {
  it("renders editor label, mode switcher, adapt and publish buttons", () => {
    const onPublish = vi.fn();
    const onAdapt = vi.fn();
    render(
      <EditorPanelHeader
        targetPlatform="Bluesky"
        mode="edit"
        onChangeMode={vi.fn()}
        isCanonical={true}
        isSent={false}
        canPublish={true}
        isPublishing={false}
        onOpenAdaptModal={onAdapt}
        onPublish={onPublish}
      />
    );

    expect(screen.getByText("editor")).toBeInTheDocument();
    expect(screen.getByText("Bluesky")).toBeInTheDocument();

    const adaptBtn = screen.getByRole("button", { name: /adapt/i });
    expect(adaptBtn).toBeInTheDocument();
    fireEvent.click(adaptBtn);
    expect(onAdapt).toHaveBeenCalledTimes(1);

    const publishBtn = screen.getByRole("button", { name: /publish/i });
    expect(publishBtn).toBeInTheDocument();
    fireEvent.click(publishBtn);
    expect(onPublish).toHaveBeenCalledTimes(1);
  });
});
