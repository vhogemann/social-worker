import { render, screen, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, it, expect, beforeEach } from "vitest";
import { ColorModeProvider, useColorMode } from "./ColorModeContext";

const TestConsumer = () => {
  const { mode, toggleColorMode, setColorMode } = useColorMode();
  return (
    <div>
      <span data-testid="mode-display">{mode}</span>
      <button onClick={toggleColorMode}>Toggle</button>
      <button onClick={() => setColorMode("light")}>Set Light</button>
      <button onClick={() => setColorMode("dark")}>Set Dark</button>
    </div>
  );
};

describe("ColorModeContext", () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.className = "";
  });

  it("defaults to dark mode if no local storage preference exists", () => {
    render(
      <ColorModeProvider>
        <TestConsumer />
      </ColorModeProvider>
    );

    expect(screen.getByTestId("mode-display")).toHaveTextContent("dark");
    expect(document.documentElement.classList.contains("dark")).toBe(true);
  });

  it("toggles between dark and light modes", async () => {
    const user = userEvent.setup();
    render(
      <ColorModeProvider>
        <TestConsumer />
      </ColorModeProvider>
    );

    expect(screen.getByTestId("mode-display")).toHaveTextContent("dark");
    await user.click(screen.getByText("Toggle"));

    expect(screen.getByTestId("mode-display")).toHaveTextContent("light");
    expect(localStorage.getItem("social-worker-theme-mode")).toBe("light");
    expect(document.documentElement.classList.contains("dark")).toBe(false);

    await user.click(screen.getByText("Toggle"));
    expect(screen.getByTestId("mode-display")).toHaveTextContent("dark");
    expect(localStorage.getItem("social-worker-theme-mode")).toBe("dark");
    expect(document.documentElement.classList.contains("dark")).toBe(true);
  });

  it("respects saved local storage mode on mount", () => {
    localStorage.setItem("social-worker-theme-mode", "light");

    render(
      <ColorModeProvider>
        <TestConsumer />
      </ColorModeProvider>
    );

    expect(screen.getByTestId("mode-display")).toHaveTextContent("light");
  });
});
