import React, { useState, useMemo } from "react";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import Autocomplete from "@mui/material/Autocomplete";
import TextField from "@mui/material/TextField";
import List from "@mui/material/List";
import ListItem from "@mui/material/ListItem";
import ListItemText from "@mui/material/ListItemText";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Chip from "@mui/material/Chip";
import Paper from "@mui/material/Paper";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faRss, faTrashCan } from "@fortawesome/free-solid-svg-icons";
import { type FeedSubscriptionDto } from "../../api/feeds";
import { type AutomationWorkflowDto, type WorkflowSourceKind } from "../../api/workflows";

export interface WorkflowSourcesSelectorProps {
  sourceKind: WorkflowSourceKind;
  onSourceKindChange: (kind: WorkflowSourceKind) => void;
  availableFeeds: FeedSubscriptionDto[];
  selectedFeedIds: string[];
  onToggleFeed: (feedId: string) => void;
  availableWorkflows: AutomationWorkflowDto[];
  selectedSourceWorkflowIds: string[];
  onToggleSourceWorkflow: (workflowId: string) => void;
  lookbackDays: number;
  onLookbackDaysChange: (days: number) => void;
}

export const WorkflowSourcesSelector: React.FC<WorkflowSourcesSelectorProps> = ({
  sourceKind,
  onSourceKindChange,
  availableFeeds,
  selectedFeedIds,
  onToggleFeed,
  availableWorkflows,
  selectedSourceWorkflowIds,
  onToggleSourceWorkflow,
  lookbackDays,
  onLookbackDaysChange
}) => {
  const [feedSearchInput, setFeedSearchInput] = useState("");

  const selectedFeeds = useMemo(() => {
    return selectedFeedIds.map((id) => {
      const found = availableFeeds.find((f) => f.id === id);
      return (
        found || {
          id,
          title: `Feed (${id})`,
          feedUrl: "",
          createdAt: ""
        }
      );
    });
  }, [selectedFeedIds, availableFeeds]);

  const unselectedFeeds = useMemo(() => {
    return availableFeeds.filter((f) => !selectedFeedIds.includes(f.id));
  }, [availableFeeds, selectedFeedIds]);

  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: 3 }}>
      <div className="flex items-center justify-between">
        <Typography
          variant="caption"
          sx={{
            fontWeight: 700,
            textTransform: "uppercase",
            letterSpacing: "0.05em",
            color: "text.secondary"
          }}
        >
          Source Type
        </Typography>
        <div className="flex gap-1 bg-bg p-0.5 rounded-lg border border-border">
          <button
            type="button"
            onClick={() => onSourceKindChange("Feed")}
            className={`px-2.5 py-1 text-xs rounded-md font-medium transition ${
              sourceKind === "Feed"
                ? "bg-accent text-accent-fg shadow-sm"
                : "text-muted hover:text-foreground"
            }`}
          >
            RSS / YouTube Feeds
          </button>
          <button
            type="button"
            onClick={() => onSourceKindChange("WorkflowHighlights")}
            className={`px-2.5 py-1 text-xs rounded-md font-medium transition ${
              sourceKind === "WorkflowHighlights"
                ? "bg-accent text-accent-fg shadow-sm"
                : "text-muted hover:text-foreground"
            }`}
          >
            Workflow Highlights
          </button>
        </div>
      </div>

      {sourceKind === "Feed" ? (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 2.5 }}>
          {/* Autocomplete Search Input */}
          <Box sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
            <label
              htmlFor="feed-autocomplete-search"
              className="block text-[10px] font-mono uppercase tracking-wider text-muted"
            >
              Search & Add Feeds
            </label>
            <Autocomplete
              id="feed-autocomplete-search"
              options={unselectedFeeds}
              value={null}
              inputValue={feedSearchInput}
              onInputChange={(_, newInputValue, reason) => {
                if (reason === "reset") {
                  setFeedSearchInput("");
                } else {
                  setFeedSearchInput(newInputValue);
                }
              }}
              onChange={(_, selectedOption) => {
                if (selectedOption && typeof selectedOption !== "string") {
                  onToggleFeed(selectedOption.id);
                  setFeedSearchInput("");
                }
              }}
              getOptionLabel={(option) =>
                typeof option === "string" ? option : `${option.title} (${option.feedUrl})`
              }
              isOptionEqualToValue={(option, value) => option.id === value.id}
              filterOptions={(options, state) => {
                const q = state.inputValue.toLowerCase().trim();
                if (!q) return options;
                return options.filter(
                  (opt) =>
                    opt.title.toLowerCase().includes(q) ||
                    opt.feedUrl.toLowerCase().includes(q)
                );
              }}
              noOptionsText={
                availableFeeds.length === 0
                  ? "No feed subscriptions found. Add feeds in Settings > Feeds first."
                  : unselectedFeeds.length === 0
                  ? "All available feeds have already been added."
                  : "No matching feeds found"
              }
              renderInput={(params) => (
                <TextField
                  {...params}
                  size="small"
                  placeholder="Type feed title or URL to search and add..."
                  inputProps={{
                    ...params.inputProps,
                    "data-testid": "feed-search-input"
                  }}
                  helperText={
                    availableFeeds.length === 0
                      ? "No feed subscriptions configured in your workspace."
                      : `${unselectedFeeds.length} available feed${
                          unselectedFeeds.length === 1 ? "" : "s"
                        } ready to add.`
                  }
                />
              )}
              renderOption={(props, option) => (
                <Box
                  component="li"
                  {...props}
                  key={option.id}
                  sx={{ display: "flex", gap: 1.5, alignItems: "center", py: 1 }}
                >
                  <Box
                    sx={{
                      color: "primary.main",
                      display: "flex",
                      alignItems: "center"
                    }}
                  >
                    <FontAwesomeIcon icon={faRss} className="text-xs" />
                  </Box>
                  <Box sx={{ minWidth: 0, flex: 1 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600 }}>
                      {option.title}
                    </Typography>
                    <Typography
                      variant="caption"
                      sx={{
                        color: "text.secondary",
                        fontFamily: "monospace",
                        display: "block",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap"
                      }}
                    >
                      {option.feedUrl}
                    </Typography>
                  </Box>
                </Box>
              )}
            />
          </Box>

          {/* Selected Feeds List with Remove Buttons */}
          <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
              <Typography
                variant="caption"
                sx={{
                  fontWeight: 700,
                  textTransform: "uppercase",
                  letterSpacing: "0.05em",
                  color: "text.secondary"
                }}
              >
                Configured Ingestion Sources
              </Typography>
              <Chip
                size="small"
                label={`${selectedFeeds.length} ${
                  selectedFeeds.length === 1 ? "feed" : "feeds"
                } selected`}
                variant="outlined"
                data-testid="selected-feeds-count"
              />
            </Box>

            {selectedFeeds.length === 0 ? (
              <Paper
                variant="outlined"
                sx={{
                  p: 3,
                  textAlign: "center",
                  borderStyle: "dashed",
                  bgcolor: "action.hover",
                  borderRadius: 2
                }}
              >
                <Typography variant="body2" sx={{ color: "text.secondary", fontWeight: 500 }}>
                  No feeds selected for ingestion.
                </Typography>
                <Typography variant="caption" sx={{ color: "text.disabled", mt: 0.5, display: "block" }}>
                  Use the search box above with autocomplete to find and add feeds.
                </Typography>
              </Paper>
            ) : (
              <List
                disablePadding
                sx={{
                  display: "flex",
                  flexDirection: "column",
                  gap: 1,
                  maxHeight: 320,
                  overflowY: "auto"
                }}
              >
                {selectedFeeds.map((feed) => (
                  <ListItem
                    key={feed.id}
                    data-testid={`selected-feed-${feed.id}`}
                    secondaryAction={
                      <Tooltip title="Remove feed from workflow">
                        <IconButton
                          edge="end"
                          size="small"
                          color="error"
                          aria-label={`Remove ${feed.title}`}
                          data-testid={`remove-feed-${feed.id}`}
                          onClick={() => onToggleFeed(feed.id)}
                        >
                          <FontAwesomeIcon icon={faTrashCan} className="text-xs" />
                        </IconButton>
                      </Tooltip>
                    }
                    sx={{
                      border: 1,
                      borderColor: "divider",
                      borderRadius: 2,
                      bgcolor: "background.paper",
                      py: 1,
                      px: 2,
                      "&:hover": {
                        bgcolor: "action.hover"
                      }
                    }}
                  >
                    <Box
                      sx={{
                        mr: 1.5,
                        color: "primary.main",
                        display: "flex",
                        alignItems: "center"
                      }}
                    >
                      <FontAwesomeIcon icon={faRss} className="text-xs" />
                    </Box>
                    <ListItemText
                      primary={
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>
                          {feed.title}
                        </Typography>
                      }
                      secondary={
                        <Typography
                          variant="caption"
                          sx={{
                            color: "text.secondary",
                            fontFamily: "monospace",
                            wordBreak: "break-all"
                          }}
                        >
                          {feed.feedUrl}
                        </Typography>
                      }
                    />
                  </ListItem>
                ))}
              </List>
            )}
          </Box>
        </Box>
      ) : (
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-3 p-2.5 bg-bg/50 border border-border rounded-lg text-xs">
            <label
              htmlFor="lookback-days"
              className="font-medium text-foreground whitespace-nowrap"
            >
              Lookback Window:
            </label>
            <input
              id="lookback-days"
              type="number"
              min={1}
              max={90}
              value={lookbackDays}
              onChange={(e) =>
                onLookbackDaysChange(Math.max(1, parseInt(e.target.value) || 7))
              }
              className="w-20 rounded border border-border bg-bg px-2 py-1 text-xs text-foreground outline-none focus:border-accent"
            />
            <span className="text-muted text-[11px]">days of published posts to aggregate</span>
          </div>

          <div>
            <div className="flex justify-between items-center mb-1.5">
              <span className="text-[10px] font-mono uppercase tracking-wider text-muted">
                Source Workflows to Aggregate ({selectedSourceWorkflowIds.length} selected)
              </span>
            </div>
            {availableWorkflows.length === 0 ? (
              <p className="text-xs text-muted">No other workflows available to aggregate.</p>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-2 max-h-40 overflow-y-auto p-2 bg-bg border border-border rounded-lg">
                {availableWorkflows.map((wf) => {
                  const isSelected = selectedSourceWorkflowIds.includes(wf.id);
                  return (
                    <label
                      key={wf.id}
                      className={`flex items-center gap-2 p-2 rounded-lg border cursor-pointer text-xs transition select-none ${
                        isSelected
                          ? "bg-panel border-accent text-foreground"
                          : "border-border text-muted hover:text-foreground hover:bg-panel/50"
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => onToggleSourceWorkflow(wf.id)}
                        className="rounded border-border text-accent focus:ring-0"
                      />
                      <div className="truncate">
                        <p className="font-medium truncate">{wf.title}</p>
                        <p className="text-[10px] text-muted font-mono truncate">
                          {wf.destinations.map((d) => d.platform).join(", ") ||
                            "No destinations"}
                        </p>
                      </div>
                    </label>
                  );
                })}
              </div>
            )}
          </div>
        </div>
      )}
    </Box>
  );
};
