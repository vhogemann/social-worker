import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCircleExclamation,
  faCircleCheck,
  faTriangleExclamation,
  faCircleInfo,
  faXmark,
} from "@fortawesome/free-solid-svg-icons";

export interface SettingsAlertProps {
  type?: "error" | "success" | "warning" | "info";
  message: string;
  onDismiss?: () => void;
  className?: string;
}

export function SettingsAlert({
  type = "error",
  message,
  onDismiss,
  className = "",
}: SettingsAlertProps) {
  if (!message) return null;

  const config = {
    error: {
      container: "bg-red-950/40 border-red-800 text-red-400",
      icon: faCircleExclamation,
    },
    success: {
      container: "bg-green-950/40 border-green-800 text-green-400",
      icon: faCircleCheck,
    },
    warning: {
      container: "bg-amber-950/40 border-amber-800 text-amber-400",
      icon: faTriangleExclamation,
    },
    info: {
      container: "bg-blue-950/40 border-blue-800 text-blue-400",
      icon: faCircleInfo,
    },
  }[type];

  return (
    <div
      className={`p-3.5 border rounded-lg flex items-start justify-between gap-3 text-xs font-mono ${config.container} ${className}`}
      role="alert"
    >
      <div className="flex items-start gap-2.5 min-w-0">
        <FontAwesomeIcon icon={config.icon} className="w-4 h-4 shrink-0 mt-0.5" />
        <span className="break-words leading-relaxed">{message}</span>
      </div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          aria-label="Dismiss alert"
          className="opacity-70 hover:opacity-100 p-0.5 rounded transition-opacity shrink-0"
        >
          <FontAwesomeIcon icon={faXmark} className="w-3.5 h-3.5" />
        </button>
      )}
    </div>
  );
}
