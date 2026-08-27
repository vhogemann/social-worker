import React from "react";
import { type AccountDto } from "../../api/accounts";

export interface WorkflowDestinationItem {
  platform: string;
  autoPublish: boolean;
  accountId?: string;
  accountHandle?: string;
}

export interface WorkflowDestinationsSelectorProps {
  connectedAccounts: AccountDto[];
  destinations: WorkflowDestinationItem[];
  onToggleDestination: (platform: string, accountId?: string, accountHandle?: string) => void;
  onToggleAutoPublish: (platform: string, accountId?: string) => void;
}

export const WorkflowDestinationsSelector: React.FC<WorkflowDestinationsSelectorProps> = ({
  connectedAccounts,
  destinations,
  onToggleDestination,
  onToggleAutoPublish
}) => {
  return (
    <div>
      <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
        Publishing Destinations & Auto-Publish
      </label>
      <div className="space-y-2 bg-bg border border-border rounded-lg p-3">
        {connectedAccounts.length > 0 ? (
          connectedAccounts.map((acc) => {
            const currentDest = destinations.find((d) => d.accountId === acc.id);
            const isEnabled = !!currentDest;
            const isAutoPublish = currentDest?.autoPublish ?? false;

            return (
              <div
                key={acc.id}
                className="flex justify-between items-center p-2 rounded-lg bg-panel border border-border/70"
              >
                <label className="flex items-center gap-2 cursor-pointer select-none">
                  <input
                    type="checkbox"
                    checked={isEnabled}
                    onChange={() => onToggleDestination(acc.platform, acc.id, acc.handle)}
                    className="rounded border-border text-accent bg-bg focus:ring-0"
                  />
                  <span className="text-xs text-foreground font-semibold">{acc.platform}</span>
                  <span className="text-[10px] text-muted font-mono">@{acc.handle}</span>
                </label>

                {isEnabled && (
                  <label className="flex items-center gap-2 cursor-pointer select-none text-xs">
                    <input
                      type="checkbox"
                      checked={isAutoPublish}
                      onChange={() => onToggleAutoPublish(acc.platform, acc.id)}
                      className="rounded border-border text-accent bg-bg focus:ring-0"
                    />
                    <span className="text-[11px] text-muted font-mono">Auto-Publish</span>
                  </label>
                )}
              </div>
            );
          })
        ) : (
          <div
            key="Bluesky"
            className="flex justify-between items-center p-2 rounded-lg bg-panel border border-border/70"
          >
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={destinations.some((d) => d.platform === "Bluesky")}
                onChange={() => onToggleDestination("Bluesky")}
                className="rounded border-border text-accent bg-bg focus:ring-0"
              />
              <span className="text-xs text-foreground font-semibold">Bluesky</span>
              <span className="text-[10px] text-muted font-mono">(Default)</span>
            </label>

            {destinations.some((d) => d.platform === "Bluesky") && (
              <label className="flex items-center gap-2 cursor-pointer select-none text-xs">
                <input
                  type="checkbox"
                  checked={destinations.find((d) => d.platform === "Bluesky")?.autoPublish ?? false}
                  onChange={() => onToggleAutoPublish("Bluesky")}
                  className="rounded border-border text-accent bg-bg focus:ring-0"
                />
                <span className="text-[11px] text-muted font-mono">Auto-Publish</span>
              </label>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
