import React, { Component, ErrorInfo, ReactNode } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faTriangleExclamation, faRotateRight } from "@fortawesome/free-solid-svg-icons";
import { Button } from "./Button";

export interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = {
      hasError: false,
      error: null
    };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return {
      hasError: true,
      error
    };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    if (this.props.onError) {
      this.props.onError(error, errorInfo);
    }
  }

  handleReset = (): void => {
    this.setState({ hasError: false, error: null });
  };

  handleReload = (): void => {
    window.location.reload();
  };

  render(): ReactNode {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
      }

      return (
        <div
          data-testid="error-boundary-fallback"
          className="min-h-screen bg-zinc-950 text-zinc-100 flex items-center justify-center p-4"
        >
          <div className="max-w-md w-full bg-zinc-900 border border-red-500/20 rounded-xl p-6 shadow-2xl text-center space-y-4">
            <div className="w-12 h-12 bg-red-500/10 text-red-400 rounded-full flex items-center justify-center mx-auto text-xl">
              <FontAwesomeIcon icon={faTriangleExclamation} />
            </div>

            <div>
              <h2 className="text-lg font-semibold text-zinc-100">Something went wrong</h2>
              <p className="text-sm text-zinc-400 mt-1">
                An unexpected application error occurred while rendering.
              </p>
            </div>

            {this.state.error?.message && (
              <div className="bg-zinc-950 p-3 rounded-lg border border-zinc-800 text-left text-xs font-mono text-red-300 break-words max-h-32 overflow-y-auto">
                {this.state.error.message}
              </div>
            )}

            <div className="flex items-center justify-center gap-3 pt-2">
              <Button
                variant="secondary"
                size="sm"
                onClick={this.handleReset}
                data-testid="error-boundary-retry-btn"
              >
                Try Again
              </Button>
              <Button
                variant="primary"
                size="sm"
                onClick={this.handleReload}
                data-testid="error-boundary-reload-btn"
              >
                <FontAwesomeIcon icon={faRotateRight} className="mr-1.5" />
                Reload Page
              </Button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
