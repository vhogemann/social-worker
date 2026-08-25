import { type ReactNode } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { type IconDefinition } from "@fortawesome/fontawesome-svg-core";
import { faInbox } from "@fortawesome/free-solid-svg-icons";

export interface SettingsEmptyStateProps {
  icon?: IconDefinition;
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
  action?: ReactNode;
}

export function SettingsEmptyState({
  icon = faInbox,
  title,
  description,
  actionLabel,
  onAction,
  action,
}: SettingsEmptyStateProps) {
  return (
    <div className="p-8 border border-border border-dashed rounded-lg flex flex-col items-center justify-center text-center space-y-3 bg-bg/10">
      <div className="w-10 h-10 rounded-full bg-border/20 border border-border flex items-center justify-center text-muted">
        <FontAwesomeIcon icon={icon} className="w-4 h-4" />
      </div>
      <div className="max-w-sm space-y-1">
        <h4 className="text-sm font-medium text-foreground">{title}</h4>
        {description && <p className="text-xs text-muted leading-normal">{description}</p>}
      </div>
      {action ? (
        action
      ) : actionLabel && onAction ? (
        <button
          type="button"
          onClick={onAction}
          className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider shadow-sm mt-1"
        >
          {actionLabel}
        </button>
      ) : null}
    </div>
  );
}
