import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { type IconDefinition } from "@fortawesome/free-solid-svg-icons";

export interface EmptyStateProps {
  title?: string;
  description?: string;
  icon?: IconDefinition;
  action?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export const EmptyState: React.FC<EmptyStateProps> = ({
  title,
  description,
  icon,
  action,
  children,
  className = ""
}) => {
  return (
    <div
      className={`text-center py-12 px-4 bg-panel border border-border rounded-xl flex flex-col items-center justify-center gap-3 ${className}`}
    >
      {icon && <FontAwesomeIcon icon={icon} className="text-2xl text-muted/60 mb-1" />}
      {title && <h3 className="text-sm font-semibold text-foreground">{title}</h3>}
      {description && <p className="text-xs text-muted max-w-sm">{description}</p>}
      {children}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
};
