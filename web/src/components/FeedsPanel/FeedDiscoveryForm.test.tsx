import React from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { FeedDiscoveryForm } from "./FeedDiscoveryForm";

describe("FeedDiscoveryForm", () => {
  it("renders input, handles change and triggers discovery", () => {
    const handleChange = vi.fn();
    const handleDiscover = vi.fn();

    render(
      <FeedDiscoveryForm
        inputUrl="https://youtube.com/@channel"
        onChangeInputUrl={handleChange}
        onDiscover={handleDiscover}
        discovering={false}
      />
    );

    const input = screen.getByDisplayValue("https://youtube.com/@channel");
    fireEvent.change(input, { target: { value: "https://example.com" } });
    expect(handleChange).toHaveBeenCalledWith("https://example.com");

    const discoverBtn = screen.getByRole("button", { name: /discover/i });
    fireEvent.click(discoverBtn);
    expect(handleDiscover).toHaveBeenCalled();
  });

  it("displays discovery error message", () => {
    render(
      <FeedDiscoveryForm
        inputUrl=""
        onChangeInputUrl={() => {}}
        onDiscover={() => {}}
        discovering={false}
        error="Feed not found"
      />
    );

    expect(screen.getByText("Feed not found")).toBeInTheDocument();
  });
});
