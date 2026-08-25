import { type ReactNode } from "react";

export interface SettingsSectionProps {
  title?: string;
  description?: string;
  badge?: ReactNode;
  action?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function SettingsSection({
  title,
  description,
  badge,
  action,
  children,
  className = "",
}: SettingsSectionProps) {
  const hasHeader = Boolean(title || description || badge || action);

  return (
    <div className={`bg-bg/40 border border-border rounded-lg overflow-hidden ${className}`}>
      {hasHeader && (
        <div className="px-5 py-4 border-b border-border bg-panel/30 flex items-center justify-between gap-4 flex-wrap">
          <div>
            <div className="flex items-center gap-2">
              {title && (
                <h3 className="text-xs font-mono uppercase tracking-wider text-muted font-semibold">
                  {title}
                </h3>
              )}
              {badge}
            </div>
            {description && (
              <p className="text-xs text-muted/80 font-sans mt-0.5">
                {description}
              </p>
            )}
          </div>
          {action && <div className="flex items-center gap-2">{action}</div>}
        </div>
      )}
      <div className="p-5">{children}</div>
    </div>
  );
}
