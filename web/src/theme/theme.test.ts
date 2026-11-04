import { describe, it, expect } from "vitest";
import { createAppTheme } from "./theme";

describe("createAppTheme", () => {
  it("creates a dark theme with standard MUI default dark palette", () => {
    const theme = createAppTheme("dark");
    expect(theme.palette.mode).toBe("dark");
    expect(theme.palette.background.default).toBe("#121212");
    expect(theme.palette.background.paper).toBe("#121212");
    expect(theme.palette.primary.main).toBe("#90caf9");
  });

  it("creates a light theme with standard MUI default light palette", () => {
    const theme = createAppTheme("light");
    expect(theme.palette.mode).toBe("light");
    expect(theme.palette.background.default).toBe("#fff");
    expect(theme.palette.background.paper).toBe("#fff");
    expect(theme.palette.primary.main).toBe("#1976d2");
  });

  it("sets button disableElevation and sans-serif font family", () => {
    const theme = createAppTheme("dark");
    expect(theme.components?.MuiButton?.defaultProps?.disableElevation).toBe(true);
    expect(theme.typography.fontFamily).toContain("ui-sans-serif");
  });
});
