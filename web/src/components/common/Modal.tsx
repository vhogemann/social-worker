import { useEffect, type ReactNode } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faXmark } from "@fortawesome/free-solid-svg-icons";

export interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  maxWidth?: "sm" | "md" | "lg" | "xl" | "2xl";
  children: ReactNode;
  footer?: ReactNode;
  testId?: string;
}

const MAX_WIDTH_CLASSES: Record<NonNullable<ModalProps["maxWidth"]>, string> = {
  sm: "max-w-sm",
  md: "max-w-md",
  lg: "max-w-lg",
  xl: "max-w-xl",
  "2xl": "max-w-2xl",
};

export function Modal({
  isOpen,
  onClose,
  title,
  description,
  maxWidth = "md",
  children,
  footer,
  testId = "modal",
}: ModalProps) {
  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const widthClass = MAX_WIDTH_CLASSES[maxWidth] || "max-w-md";

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-bg/80 backdrop-blur-sm p-4 animate-fade-in font-sans"
      onClick={onClose}
      data-testid={`${testId}-overlay`}
    >
      <div
        className={`bg-panel border border-border rounded-xl shadow-2xl w-full ${widthClass} mx-auto overflow-hidden flex flex-col max-h-[90vh]`}
        onClick={(e) => e.stopPropagation()}
        data-testid={testId}
        role="dialog"
        aria-modal="true"
        aria-labelledby={`${testId}-title`}
      >
        <div className="px-5 py-4 border-b border-border flex items-start justify-between gap-3 shrink-0 bg-panel/50">
          <div>
            <h2 id={`${testId}-title`} className="text-sm font-semibold text-foreground">
              {title}
            </h2>
            {description && (
              <p className="text-xs text-muted mt-0.5 font-sans leading-normal">
                {description}
              </p>
            )}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close modal"
            className="text-muted hover:text-foreground p-1 rounded-md hover:bg-border/20 transition-colors -mr-1 -mt-1"
          >
            <FontAwesomeIcon icon={faXmark} className="w-4 h-4" />
          </button>
        </div>

        <div className="p-5 overflow-y-auto min-h-0 flex-1 space-y-4">
          {children}
        </div>

        {footer && (
          <div className="px-5 py-3.5 border-t border-border bg-panel/30 flex items-center justify-end gap-2 shrink-0">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
