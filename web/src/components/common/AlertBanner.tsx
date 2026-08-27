import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faXmark } from "@fortawesome/free-solid-svg-icons";

export type AlertBannerVariant = "error" | "warning" | "info" | "success";

export interface AlertBannerProps {
  variant?: AlertBannerVariant;
  message?: string;
  children?: React.ReactNode;
  onDismiss?: () => void;
  className?: string;
}

const variantStyles: Record<AlertBannerVariant, string> = {
  error: "bg-red-500/10 border-red-500/20 text-red-400",
  warning: "bg-yellow-500/10 border-yellow-500/20 text-yellow-400",
  info: "bg-blue-500/10 border-blue-500/20 text-blue-400",
  success: "bg-green-500/10 border-green-500/20 text-green-400"
};

export const AlertBanner: React.FC<AlertBannerProps> = ({
  variant = "error",
  message,
  children,
  onDismiss,
  className = ""
}) => {
  return (
    <div
      className={`p-3 border rounded-lg text-xs flex justify-between items-center ${variantStyles[variant]} ${className}`}
    >
      <div className="flex-1 pr-2">{message || children}</div>
      {onDismiss && (
        <button
          type="button"
          onClick={onDismiss}
          className="opacity-70 hover:opacity-100 transition p-0.5"
          aria-label="Dismiss alert"
        >
          <FontAwesomeIcon icon={faXmark} />
        </button>
      )}
    </div>
  );
};
