import React from "react";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import Switch from "@mui/material/Switch";
import FormControlLabel from "@mui/material/FormControlLabel";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import ToggleButton from "@mui/material/ToggleButton";
import FormControl from "@mui/material/FormControl";
import InputLabel from "@mui/material/InputLabel";
import Select from "@mui/material/Select";
import MenuItem from "@mui/material/MenuItem";
import Alert from "@mui/material/Alert";
import Avatar from "@mui/material/Avatar";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faShareNodes, faClock, faBolt, faEye } from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_ICONS, getPlatformLabel } from "../../config/platformBadges";
import { type AccountDto } from "../../api/accounts";
import { type ScheduleSummaryDto } from "../../api/schedules";

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
    const selectedScheduleId =
      currentDest?.scheduleId || (availableSchedules.length > 0 ? availableSchedules[0].id : "");
    const icon = PLATFORM_ICONS[platform];

    return (
      <Card
        key={accountId || platform}
        variant="outlined"
        sx={{
          borderRadius: 2.5,
          borderColor: isEnabled ? "primary.main" : "divider",
          bgcolor: isEnabled ? "background.paper" : "action.hover",
          transition: "border-color 0.2s ease, background-color 0.2s ease"
        }}
      >
        <CardContent sx={{ p: 2.5, "&:last-child": { pb: 2.5 } }}>
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              gap: 2
            }}
          >
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, minWidth: 0 }}>
              <Avatar
                sx={{
                  width: 40,
                  height: 40,
                  bgcolor: isEnabled ? "primary.main" : "action.disabledBackground",
                  color: isEnabled ? "primary.contrastText" : "text.secondary",
                  fontSize: "1.1rem"
                }}
              >
                {icon ? (
                  <FontAwesomeIcon icon={icon} />
                ) : (
                  <FontAwesomeIcon icon={faShareNodes} />
                )}
              </Avatar>
              <Box sx={{ minWidth: 0 }}>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                    {label}
                  </Typography>
                  {sublabel && (
                    <Typography
                      variant="caption"
                      sx={{
                        fontFamily: "monospace",
                        color: "text.secondary",
                        bgcolor: "action.selected",
                        px: 0.75,
                        py: 0.25,
                        borderRadius: 1
                      }}
                    >
                      {sublabel}
                    </Typography>
                  )}
                </Box>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  {isEnabled
                    ? `Active destination · ${
                        mode === "Manual"
                          ? "Manual Review"
                          : mode === "Automatic"
                          ? "Auto Publish"
                          : "Scheduled"
                      }`
                    : "Destination inactive"}
                </Typography>
              </Box>
            </Box>

            <FormControlLabel
              control={
                <Switch
                  checked={isEnabled}
                  onChange={() => onToggleDestination(platform, accountId, sublabel)}
                  color="primary"
                  inputProps={{
                    "aria-label": `Enable ${label}`,
                    ...({ "data-testid": `toggle-dest-${platform.toLowerCase()}` } as React.InputHTMLAttributes<HTMLInputElement>)
                  }}
                />
              }
              label={
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 600,
                    color: isEnabled ? "primary.main" : "text.secondary"
                  }}
                >
                  {isEnabled ? "Enabled" : "Disabled"}
                </Typography>
              }
              sx={{ mr: 0 }}
            />
          </Box>

          {isEnabled && (
            <Box
              sx={{
                mt: 2.5,
                pt: 2,
                borderTop: 1,
                borderColor: "divider",
                display: "flex",
                flexDirection: "column",
                gap: 2
              }}
            >
              <Box>
                <Typography
                  variant="caption"
                  sx={{
                    fontWeight: 700,
                    textTransform: "uppercase",
                    letterSpacing: "0.05em",
                    color: "text.secondary",
                    mb: 1,
                    display: "block"
                  }}
                >
                  Publishing Mode
                </Typography>

                <ToggleButtonGroup
                  value={mode}
                  exclusive
                  onChange={(_, newMode) => {
                    if (newMode) {
                      handleModeChange(platform, newMode, selectedScheduleId, accountId);
                    }
                  }}
                  fullWidth
                  size="small"
                >
                  <ToggleButton
                    value="Manual"
                    data-testid={`mode-manual-${platform.toLowerCase()}`}
                    onClick={() => handleModeChange(platform, "Manual", undefined, accountId)}
                    sx={{ textTransform: "none", fontWeight: 600, py: 1 }}
                  >
                    <FontAwesomeIcon icon={faEye} className="mr-1.5 text-xs" />
                    Manual Review
                  </ToggleButton>

                  <ToggleButton
                    value="Automatic"
                    data-testid={`mode-automatic-${platform.toLowerCase()}`}
                    onClick={() => handleModeChange(platform, "Automatic", undefined, accountId)}
                    sx={{ textTransform: "none", fontWeight: 600, py: 1 }}
                  >
                    <FontAwesomeIcon icon={faBolt} className="mr-1.5 text-xs" />
                    Auto Publish
                  </ToggleButton>

                  <ToggleButton
                    value="Scheduled"
                    data-testid={`mode-scheduled-${platform.toLowerCase()}`}
                    onClick={() =>
                      handleModeChange(
                        platform,
                        "Scheduled",
                        selectedScheduleId,
                        accountId
                      )
                    }
                    sx={{ textTransform: "none", fontWeight: 600, py: 1 }}
                  >
                    <FontAwesomeIcon icon={faClock} className="mr-1.5 text-xs" />
                    Add to Schedule
                  </ToggleButton>
                </ToggleButtonGroup>
              </Box>

              {/* Mode Description Box */}
              <Box
                sx={{
                  p: 1.5,
                  borderRadius: 1.5,
                  bgcolor: "action.hover",
                  border: 1,
                  borderColor: "divider"
                }}
              >
                {mode === "Manual" && (
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary", display: "block", lineHeight: 1.5 }}
                  >
                    <strong>Manual Review:</strong> Generated threads are saved to your drafts
                    list. You can review, refine, and manually approve before publishing.
                  </Typography>
                )}
                {mode === "Automatic" && (
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary", display: "block", lineHeight: 1.5 }}
                  >
                    <strong>Auto Publish:</strong> Synthesized threads that meet your quality
                    cutoff are automatically published to {label} without human review.
                  </Typography>
                )}
                {mode === "Scheduled" && (
                  <Typography
                    variant="caption"
                    sx={{ color: "text.secondary", display: "block", lineHeight: 1.5 }}
                  >
                    <strong>Add to Schedule:</strong> Generated threads are enqueued into a
                    calendar schedule queue and posted autonomously during configured time slots.
                  </Typography>
                )}
              </Box>

              {/* Schedule Select */}
              {mode === "Scheduled" && (
                <Box sx={{ mt: 0.5 }}>
                  {availableSchedules.length === 0 ? (
                    <Alert severity="warning" sx={{ py: 0.5 }}>
                      No active schedules found. Please create a schedule in the Calendar first.
                    </Alert>
                  ) : (
                    <FormControl size="small" fullWidth>
                      <InputLabel id={`schedule-select-label-${platform.toLowerCase()}`}>
                        Target Publishing Schedule
                      </InputLabel>
                      <Select
                        labelId={`schedule-select-label-${platform.toLowerCase()}`}
                        value={selectedScheduleId}
                        label="Target Publishing Schedule"
                        inputProps={{
                          "data-testid": `schedule-select-${platform.toLowerCase()}`
                        }}
                        onChange={(e) =>
                          handleModeChange(
                            platform,
                            "Scheduled",
                            e.target.value as string,
                            accountId
                          )
                        }
                      >
                        {availableSchedules.map((s) => (
                          <MenuItem key={s.id} value={s.id}>
                            {s.name} ({s.timezone})
                          </MenuItem>
                        ))}
                      </Select>
                    </FormControl>
                  )}
                </Box>
              )}
            </Box>
          )}
        </CardContent>
      </Card>
    );
  };

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
      <Typography
        variant="caption"
        sx={{
          fontWeight: 700,
          textTransform: "uppercase",
          letterSpacing: "0.05em",
          color: "text.secondary"
        }}
      >
        Publishing Destinations & Workflow Modes
      </Typography>
      <Box sx={{ display: "flex", flexDirection: "column", gap: 2 }}>
        {connectedAccounts.length > 0 ? (
          connectedAccounts.map((acc) =>
            renderDestinationRow(
              acc.platform,
              getPlatformLabel(acc.platform),
              `@${acc.handle}`,
              acc.id
            )
          )
        ) : (
          renderDestinationRow("Bluesky", "Bluesky", "(Default)")
        )}
      </Box>
    </Box>
  );
};
