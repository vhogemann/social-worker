import React from "react";
import { type AccountDto } from "../../api/accounts";
import { type ScheduleSummaryDto } from "../../api/schedules";
import { getPlatformLabel } from "../../config/platformBadges";

export type PublishingMode = "Manual" | "Automatic" | "Scheduled";

export interface WorkflowDestinationItem {
  platform: string;
  autoPublish: boolean;
  publishingMode?: PublishingMode;
  scheduleId?: string | null;
  accountId?: string;
  accountHandle?: string;
}

export interface WorkflowDestinationsSelectorProps {
  connectedAccounts: AccountDto[];
  destinations: WorkflowDestinationItem[];
  availableSchedules?: ScheduleSummaryDto[];
  onToggleDestination: (platform: string, accountId?: string, accountHandle?: string) => void;
  onToggleAutoPublish?: (platform: string, accountId?: string) => void;
  onChangePublishingMode?: (
    platform: string,
    mode: PublishingMode,
    scheduleId?: string,
    accountId?: string
  ) => void;
}

export const WorkflowDestinationsSelector: React.FC<WorkflowDestinationsSelectorProps> = ({
  connectedAccounts,
  destinations,
  availableSchedules = [],
  onToggleDestination,
  onToggleAutoPublish,
  onChangePublishingMode
}) => {
  const handleModeChange = (
    platform: string,
    mode: PublishingMode,
    scheduleId?: string,
    accountId?: string
  ) => {
    if (onChangePublishingMode) {
      onChangePublishingMode(platform, mode, scheduleId, accountId);
    } else if (onToggleAutoPublish) {
      onToggleAutoPublish(platform, accountId);
    }
  };

  const renderDestinationRow = (
    platform: string,
    label: string,
    sublabel?: string,
    accountId?: string
  ) => {
    const currentDest = destinations.find((d) =>
      accountId ? d.accountId === accountId : d.platform === platform
    );
    const isEnabled = !!currentDest;
    const mode: PublishingMode =
      currentDest?.publishingMode || (currentDest?.autoPublish ? "Automatic" : "Manual");
    const selectedScheduleId = currentDest?.scheduleId || (availableSchedules.length > 0 ? availableSchedules[0].id : "");

    return (
      <div
        key={accountId || platform}
        className="flex flex-col gap-2 p-3 rounded-lg bg-panel border border-border/70"
      >
        <div className="flex justify-between items-center">
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={isEnabled}
              data-testid={`toggle-dest-${platform.toLowerCase()}`}
              onChange={() => onToggleDestination(platform, accountId, sublabel)}
              className="rounded border-border text-accent bg-bg focus:ring-0"
            />
            <span className="text-xs text-foreground font-semibold">{label}</span>
            {sublabel && (
              <span className="text-[10px] text-muted font-mono">{sublabel}</span>
            )}
          </label>
        </div>

        {isEnabled && (
          <div className="pt-2 border-t border-border/40 pl-6 space-y-2">
            <span className="text-[10px] font-mono text-muted uppercase tracking-wider block">
              Publishing Mode
            </span>

            <div className="grid grid-cols-3 gap-2">
              <button
                type="button"
                data-testid={`mode-manual-${platform.toLowerCase()}`}
                onClick={() => handleModeChange(platform, "Manual", undefined, accountId)}
                className={`py-1.5 px-2 rounded border text-[11px] font-mono transition text-center ${
                  mode === "Manual"
                    ? "border-accent bg-accent/15 text-accent font-semibold"
                    : "border-border bg-bg text-muted hover:text-foreground"
                }`}
              >
                Manual Review
              </button>

              <button
                type="button"
                data-testid={`mode-automatic-${platform.toLowerCase()}`}
                onClick={() => handleModeChange(platform, "Automatic", undefined, accountId)}
                className={`py-1.5 px-2 rounded border text-[11px] font-mono transition text-center ${
                  mode === "Automatic"
                    ? "border-accent bg-accent/15 text-accent font-semibold"
                    : "border-border bg-bg text-muted hover:text-foreground"
                }`}
              >
                Auto Publish
              </button>

              <button
                type="button"
                data-testid={`mode-scheduled-${platform.toLowerCase()}`}
                onClick={() =>
                  handleModeChange(
                    platform,
                    "Scheduled",
                    selectedScheduleId,
                    accountId
                  )
                }
                className={`py-1.5 px-2 rounded border text-[11px] font-mono transition text-center ${
                  mode === "Scheduled"
                    ? "border-accent bg-accent/15 text-accent font-semibold"
                    : "border-border bg-bg text-muted hover:text-foreground"
                }`}
              >
                Add to Schedule
              </button>
            </div>

            {mode === "Scheduled" && (
              <div className="pt-1">
                <label className="block text-[10px] font-mono text-muted mb-1">
                  Target Publishing Schedule:
                </label>
                {availableSchedules.length === 0 ? (
                  <p className="text-[10px] font-mono text-yellow-400/90">
                    No active schedules found. Please create a schedule in the Calendar.
                  </p>
                ) : (
                  <select
                    value={selectedScheduleId}
                    data-testid={`schedule-select-${platform.toLowerCase()}`}
                    onChange={(e) =>
                      handleModeChange(platform, "Scheduled", e.target.value, accountId)
                    }
                    className="w-full rounded border border-border bg-bg px-2.5 py-1 text-xs font-mono text-foreground outline-none focus:border-accent"
                  >
                    {availableSchedules.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.name} ({s.timezone})
                      </option>
                    ))}
                  </select>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <div>
      <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
        Publishing Destinations & Workflow Modes
      </label>
      <div className="space-y-2 bg-bg border border-border rounded-lg p-3">
        {connectedAccounts.length > 0 ? (
          connectedAccounts.map((acc) =>
            renderDestinationRow(acc.platform, getPlatformLabel(acc.platform), `@${acc.handle}`, acc.id)
          )
        ) : (
          renderDestinationRow("Bluesky", "Bluesky", "(Default)")
        )}
      </div>
    </div>
  );
};
