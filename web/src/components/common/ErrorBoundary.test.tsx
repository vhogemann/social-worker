import React from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ErrorBoundary } from "./ErrorBoundary";

const ThrowingComponent: React.FC<{ shouldThrow?: boolean }> = ({ shouldThrow }) => {
  if (shouldThrow) {
    throw new Error("Simulated rendering crash");
  }
  return <div>Normal Content</div>;
};

describe("ErrorBoundary", () => {
  it("renders children when no error occurs", () => {
    render(
      <ErrorBoundary>
        <ThrowingComponent shouldThrow={false} />
      </ErrorBoundary>
    );

    expect(screen.getByText("Normal Content")).toBeInTheDocument();
    expect(screen.queryByTestId("error-boundary-fallback")).toBeNull();
  });

  it("renders fallback UI when child throws during render", () => {
    const onErrorMock = vi.fn();
    const consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    render(
      <ErrorBoundary onError={onErrorMock}>
        <ThrowingComponent shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.getByTestId("error-boundary-fallback")).toBeInTheDocument();
    expect(screen.getByText(/Something went wrong/i)).toBeInTheDocument();
    expect(screen.getByText(/Simulated rendering crash/i)).toBeInTheDocument();
    expect(onErrorMock).toHaveBeenCalled();

    consoleErrorSpy.mockRestore();
  });

  it("resets error state when Try Again is clicked", () => {
    const consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    let shouldThrow = true;
    const DynamicComponent: React.FC = () => {
      if (shouldThrow) {
        throw new Error("Temporary crash");
      }
      return <div>Recovered Content</div>;
    };

    const { rerender } = render(
      <ErrorBoundary>
        <DynamicComponent />
      </ErrorBoundary>
    );

    expect(screen.getByTestId("error-boundary-fallback")).toBeInTheDocument();

    shouldThrow = false;
    fireEvent.click(screen.getByTestId("error-boundary-retry-btn"));

    rerender(
      <ErrorBoundary>
        <DynamicComponent />
      </ErrorBoundary>
    );

    expect(screen.getByText("Recovered Content")).toBeInTheDocument();
    expect(screen.queryByTestId("error-boundary-fallback")).toBeNull();

    consoleErrorSpy.mockRestore();
  });

  it("renders custom fallback when provided", () => {
    const consoleErrorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    render(
      <ErrorBoundary fallback={<div>Custom Error View</div>}>
        <ThrowingComponent shouldThrow={true} />
      </ErrorBoundary>
    );

    expect(screen.getByText("Custom Error View")).toBeInTheDocument();
    expect(screen.queryByTestId("error-boundary-fallback")).toBeNull();

    consoleErrorSpy.mockRestore();
  });
});
