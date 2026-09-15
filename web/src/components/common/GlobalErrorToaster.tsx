import React, { useEffect, useState, useRef, useCallback } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCircleExclamation, faXmark } from "@fortawesome/free-solid-svg-icons";

export interface GlobalToastError {
  id: string;
  message: string;
  timestamp: number;
}

const AUTO_DISMISS_MS = 6000;
const DEDUPE_WINDOW_MS = 3000;
const MAX_VISIBLE_TOASTS = 3;

export function formatErrorMessage(raw: unknown): string | null {
  if (!raw) return "An unexpected error occurred.";

  if (typeof raw === "string") {
    const trimmed = raw.trim();
    if (trimmed.toLowerCase().includes("aborterror") || trimmed.toLowerCase().includes("user aborted")) {
      return null;
    }
    return trimmed || "An unexpected error occurred.";
  }

  if (raw instanceof Error) {
    if (raw.name === "AbortError" || raw.message.toLowerCase().includes("user aborted")) {
      return null;
    }
    return raw.message || "An unexpected error occurred.";
  }

  if (typeof raw === "object") {
    const obj = raw as Record<string, unknown>;
    if (obj.name === "AbortError") return null;
    if (typeof obj.error === "string") return obj.error;
    if (typeof obj.message === "string") return obj.message;
    if (typeof obj.detail === "string") return obj.detail;
  }

  return "An unexpected error occurred.";
}

export const GlobalErrorToaster: React.FC = () => {
  const [errors, setErrors] = useState<GlobalToastError[]>([]);
  const recentErrorsRef = useRef<Map<string, number>>(new Map());
  const [hoveredId, setHoveredId] = useState<string | null>(null);

  const dismissError = useCallback((id: string) => {
    setErrors((prev) => prev.filter((e) => e.id !== id));
  }, []);

  const pushError = useCallback((formattedMsg: string) => {
    const now = Date.now();
    const lastSeen = recentErrorsRef.current.get(formattedMsg) ?? 0;
    if (now - lastSeen < DEDUPE_WINDOW_MS) {
      return;
    }
    recentErrorsRef.current.set(formattedMsg, now);

    const newError: GlobalToastError = {
      id: `${now}-${Math.random().toString(36).substring(2, 9)}`,
      message: formattedMsg,
      timestamp: now
    };

    setErrors((prev) => {
      const next = [newError, ...prev];
      return next.slice(0, MAX_VISIBLE_TOASTS);
    });
  }, []);

  useEffect(() => {
    const handleError = (event: ErrorEvent) => {
      const msg = formatErrorMessage(event.error || event.message);
      if (msg) {
        pushError(msg);
      }
    };

    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      const msg = formatErrorMessage(event.reason);
      if (msg) {
        pushError(msg);
      }
    };

    window.addEventListener("error", handleError);
    window.addEventListener("unhandledrejection", handleUnhandledRejection);

    return () => {
      window.removeEventListener("error", handleError);
      window.removeEventListener("unhandledrejection", handleUnhandledRejection);
    };
  }, [pushError]);

  useEffect(() => {
    if (errors.length === 0) return;

    const interval = setInterval(() => {
      const now = Date.now();
      setErrors((prev) =>
        prev.filter((item) => {
          if (item.id === hoveredId) return true;
          return now - item.timestamp < AUTO_DISMISS_MS;
        })
      );
    }, 500);

    return () => clearInterval(interval);
  }, [errors.length, hoveredId]);

  if (errors.length === 0) {
    return null;
  }

  return (
    <div
      data-testid="global-error-toaster"
      className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-md w-full pointer-events-none px-4 sm:px-0"
      aria-live="assertive"
      aria-atomic="true"
    >
      {errors.map((item) => (
        <div
          key={item.id}
          data-testid="global-error-toast-item"
          role="alert"
          onMouseEnter={() => setHoveredId(item.id)}
          onMouseLeave={() => setHoveredId((curr) => (curr === item.id ? null : curr))}
          className="pointer-events-auto flex items-start gap-3 p-3.5 bg-zinc-900/95 border border-red-500/30 text-zinc-100 rounded-lg shadow-2xl backdrop-blur transition transform translate-y-0"
        >
          <div className="text-red-400 mt-0.5 flex-shrink-0">
            <FontAwesomeIcon icon={faCircleExclamation} />
          </div>
          <div className="flex-1 min-w-0">
            <div className="text-xs font-semibold text-red-400 uppercase tracking-wider mb-0.5">
              Unexpected Error
            </div>
            <div className="text-xs text-zinc-300 break-words line-clamp-3">
              {item.message}
            </div>
          </div>
          <button
            type="button"
            data-testid="dismiss-toast-btn"
            onClick={() => dismissError(item.id)}
            className="text-zinc-400 hover:text-zinc-100 transition p-1 -mr-1 -mt-1 flex-shrink-0"
            aria-label="Dismiss error"
          >
            <FontAwesomeIcon icon={faXmark} className="w-3.5 h-3.5" />
          </button>
        </div>
      ))}
    </div>
  );
};
