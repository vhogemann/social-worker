import React from "react";
import { render, screen, act, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { GlobalErrorToaster, formatErrorMessage } from "./GlobalErrorToaster";

describe("formatErrorMessage", () => {
  it("formats string errors", () => {
    expect(formatErrorMessage("Failed to fetch")).toBe("Failed to fetch");
  });

  it("extracts message from Error instance", () => {
    expect(formatErrorMessage(new Error("Database disconnected"))).toBe("Database disconnected");
  });

  it("extracts error property from object", () => {
    expect(formatErrorMessage({ error: "Server 500 error" })).toBe("Server 500 error");
  });

  it("extracts message property from object", () => {
    expect(formatErrorMessage({ message: "Network timeout" })).toBe("Network timeout");
  });

  it("extracts detail property from object", () => {
    expect(formatErrorMessage({ detail: "Validation failed" })).toBe("Validation failed");
  });

  it("returns null for AbortError", () => {
    const abortErr = new Error("The user aborted a request");
    abortErr.name = "AbortError";
    expect(formatErrorMessage(abortErr)).toBeNull();
    expect(formatErrorMessage("AbortError: aborted")).toBeNull();
  });

  it("returns default message for null or empty input", () => {
    expect(formatErrorMessage(null)).toBe("An unexpected error occurred.");
    expect(formatErrorMessage("")).toBe("An unexpected error occurred.");
  });
});

describe("GlobalErrorToaster", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("renders nothing when there are no errors", () => {
    render(<GlobalErrorToaster />);
    expect(screen.queryByTestId("global-error-toaster")).toBeNull();
  });

  it("renders toast when window error event is dispatched", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      window.dispatchEvent(
        new ErrorEvent("error", {
          message: "Uncaught ReferenceError: foo is not defined",
          error: new Error("Uncaught ReferenceError: foo is not defined")
        })
      );
    });

    expect(screen.getByTestId("global-error-toaster")).toBeInTheDocument();
    expect(screen.getByText(/Uncaught ReferenceError: foo is not defined/i)).toBeInTheDocument();
  });

  it("renders toast when unhandledrejection event is dispatched", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      const event = new Event("unhandledrejection") as any;
      event.reason = new Error("Backend service unavailable (500)");
      window.dispatchEvent(event);
    });

    expect(screen.getByTestId("global-error-toaster")).toBeInTheDocument();
    expect(screen.getByText(/Backend service unavailable \(500\)/i)).toBeInTheDocument();
  });

  it("allows manual dismissal via close button", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      window.dispatchEvent(
        new ErrorEvent("error", {
          message: "Manual dismiss test",
          error: new Error("Manual dismiss test")
        })
      );
    });

    expect(screen.getByText(/Manual dismiss test/i)).toBeInTheDocument();

    const dismissBtn = screen.getByTestId("dismiss-toast-btn");
    fireEvent.click(dismissBtn);

    expect(screen.queryByText(/Manual dismiss test/i)).toBeNull();
    expect(screen.queryByTestId("global-error-toaster")).toBeNull();
  });

  it("auto-dismisses error after 6 seconds", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      window.dispatchEvent(
        new ErrorEvent("error", {
          message: "Auto dismiss test",
          error: new Error("Auto dismiss test")
        })
      );
    });

    expect(screen.getByText(/Auto dismiss test/i)).toBeInTheDocument();

    act(() => {
      vi.advanceTimersByTime(6500);
    });

    expect(screen.queryByText(/Auto dismiss test/i)).toBeNull();
    expect(screen.queryByTestId("global-error-toaster")).toBeNull();
  });

  it("deduplicates identical errors within 3-second window", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      window.dispatchEvent(
        new ErrorEvent("error", {
          message: "Duplicate error message",
          error: new Error("Duplicate error message")
        })
      );
    });

    act(() => {
      window.dispatchEvent(
        new ErrorEvent("error", {
          message: "Duplicate error message",
          error: new Error("Duplicate error message")
        })
      );
    });

    const items = screen.getAllByTestId("global-error-toast-item");
    expect(items).toHaveLength(1);
  });

  it("limits maximum visible toasts to 3", () => {
    render(<GlobalErrorToaster />);

    act(() => {
      window.dispatchEvent(new ErrorEvent("error", { error: new Error("Error 1") }));
      window.dispatchEvent(new ErrorEvent("error", { error: new Error("Error 2") }));
      window.dispatchEvent(new ErrorEvent("error", { error: new Error("Error 3") }));
      window.dispatchEvent(new ErrorEvent("error", { error: new Error("Error 4") }));
    });

    const items = screen.getAllByTestId("global-error-toast-item");
    expect(items).toHaveLength(3);
    expect(screen.queryByText("Error 1")).toBeNull();
    expect(screen.getByText("Error 4")).toBeInTheDocument();
  });
});
