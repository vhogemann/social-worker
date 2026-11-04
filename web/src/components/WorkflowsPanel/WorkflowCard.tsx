import React from "react";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CardActions from "@mui/material/CardActions";
import Typography from "@mui/material/Typography";
import Box from "@mui/material/Box";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBolt,
  faDownload,
  faEdit,
  faLayerGroup,
  faRss,
  faTrashCan
} from "@fortawesome/free-solid-svg-icons";
import { type AutomationWorkflowDto } from "../../api/workflows";
import { Button } from "../common/Button";
import { getPlatformLabel } from "../../config/platformBadges";

export interface WorkflowCardProps {
  workflow: AutomationWorkflowDto;
  isTriggering?: boolean;
  onClick: (id: string) => void;
  onTrigger: (id: string) => void;
  onEdit: (workflow: AutomationWorkflowDto) => void;
  onDelete: (id: string) => void;
  onExport?: (workflow: AutomationWorkflowDto) => void;
}

export const WorkflowCard: React.FC<WorkflowCardProps> = ({
  workflow,
  isTriggering = false,
  onClick,
  onTrigger,
  onEdit,
  onDelete,
  onExport
}) => {
  return (
    <Card
      onClick={() => onClick(workflow.id)}
      variant="outlined"
      sx={{
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        borderRadius: 3,
        cursor: "pointer",
        transition: "all 0.2s ease-in-out",
        "&:hover": {
          borderColor: "primary.main",
          boxShadow: 2
        }
      }}
    >
      <CardContent sx={{ display: "flex", flexDirection: "column", gap: 1.5, p: 2.5, "&:last-child": { pb: 2.5 } }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1 }}>
          <Typography
            variant="subtitle2"
            component="h3"
            sx={{
              fontWeight: 700,
              fontSize: "0.875rem",
              lineHeight: 1.3,
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
              flex: 1
            }}
          >
            {workflow.title}
          </Typography>
          <span
            className="text-[10px] font-mono px-2 py-0.5 rounded border shrink-0 font-medium text-muted bg-bg border-border"
            data-testid="workflow-card-filter-badge"
          >
            {(workflow.minRelevanceScore ?? 0) > 0 ? `Filter: >= ${workflow.minRelevanceScore}/10` : "Filter: Off"}
          </span>
        </Box>

        <Typography
          variant="body2"
          sx={{
            fontSize: "0.75rem",
            color: "text.secondary",
            p: 1.25,
            borderRadius: 1.5,
            border: 1,
            borderColor: "divider",
            bgcolor: "action.hover",
            display: "-webkit-box",
            WebkitLineClamp: 2,
            WebkitBoxOrient: "vertical",
            overflow: "hidden"
          }}
        >
          "{workflow.instructionPrompt || "Summarize this article as a thread."}"
        </Typography>

        {/* Sources Section */}
        <Box>
          <Typography
            variant="caption"
            sx={{
              fontFamily: "monospace",
              fontSize: "0.625rem",
              textTransform: "uppercase",
              letterSpacing: 0.5,
              color: "text.secondary",
              display: "block",
              mb: 0.75
            }}
          >
            Ingestion Sources ({workflow.sources.length})
          </Typography>
          {workflow.sources.length === 0 ? (
            <Typography variant="caption" sx={{ fontSize: "0.6875rem", color: "text.secondary", fontStyle: "italic" }}>
              No linked sources
            </Typography>
          ) : (
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
              {workflow.sources.map((src, index) => {
                const isHighlights = src.kind === "WorkflowHighlights";
                return (
                  <Box
                    key={src.id || src.feedSubscriptionId || src.sourceWorkflowId || index}
                    component="span"
                    sx={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 0.5,
                      px: 1,
                      py: 0.25,
                      borderRadius: 1,
                      border: 1,
                      borderColor: "divider",
                      fontSize: "0.6875rem",
                      fontFamily: "monospace",
                      bgcolor: "background.paper"
                    }}
                  >
                    <FontAwesomeIcon
                      icon={isHighlights ? faLayerGroup : faRss}
                      className={`text-[9px] ${isHighlights ? "text-purple-400" : "text-accent"}`}
                    />
                    <span className="line-clamp-1">
                      {isHighlights
                        ? `Highlights: ${src.sourceWorkflowTitle || src.title || "Workflow"}`
                        : (src.title || "Feed Source")}
                    </span>
                  </Box>
                );
              })}
            </Box>
          )}
        </Box>

        {/* Destinations Section */}
        <Box>
          <Typography
            variant="caption"
            sx={{
              fontFamily: "monospace",
              fontSize: "0.625rem",
              textTransform: "uppercase",
              letterSpacing: 0.5,
              color: "text.secondary",
              display: "block",
              mb: 0.75
            }}
          >
            Target Destinations ({workflow.destinations.length})
          </Typography>
          {workflow.destinations.length === 0 ? (
            <Typography variant="caption" sx={{ fontSize: "0.6875rem", color: "text.secondary", fontStyle: "italic" }}>
              Drafts only (no publishing)
            </Typography>
          ) : (
            <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.75 }}>
              {workflow.destinations.map((dest) => (
                <Box
                  key={dest.id}
                  component="span"
                  sx={{
                    display: "inline-flex",
                    alignItems: "center",
                    gap: 0.75,
                    px: 1,
                    py: 0.25,
                    borderRadius: 1,
                    border: 1,
                    borderColor: "divider",
                    fontSize: "0.6875rem",
                    fontFamily: "monospace",
                    bgcolor: "background.paper"
                  }}
                >
                  <span>{getPlatformLabel(dest.platform)}</span>
                  {dest.accountHandle && (
                    <Box component="span" sx={{ opacity: 0.75, fontSize: "0.625rem" }}>
                      @{dest.accountHandle}
                    </Box>
                  )}
                  {dest.publishingMode === "Scheduled" ? (
                    <span className="text-[8px] bg-blue-500/20 text-blue-400 font-semibold px-1 py-0.5 rounded uppercase">
                      Scheduled {dest.scheduleName ? `(${dest.scheduleName})` : ""}
                    </span>
                  ) : dest.publishingMode === "Manual" ? (
                    <span className="text-[8px] bg-zinc-500/20 text-zinc-400 font-semibold px-1 py-0.5 rounded uppercase">
                      Outbox Review
                    </span>
                  ) : dest.autoPublish || dest.publishingMode === "Automatic" ? (
                    <span className="text-[8px] bg-accent/20 text-accent font-semibold px-1 py-0.5 rounded uppercase">
                      Auto-Publish
                    </span>
                  ) : null}
                </Box>
              ))}
            </Box>
          )}
        </Box>
      </CardContent>

      {/* Actions */}
      <CardActions
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          borderTop: 1,
          borderColor: "divider",
          px: 2.5,
          py: 1.5
        }}
      >
        <Button
          variant="secondary"
          size="xs"
          loading={isTriggering}
          icon={faBolt}
          onClick={(e) => {
            e.stopPropagation();
            onTrigger(workflow.id);
          }}
          className="font-mono text-foreground"
        >
          {isTriggering ? "Running..." : "Trigger Now"}
        </Button>

        <Box sx={{ display: "flex", gap: 1 }}>
          {onExport && (
            <Button
              variant="outline"
              size="xs"
              onClick={(e) => {
                e.stopPropagation();
                onExport(workflow);
              }}
              title="Export workflow"
              aria-label="Export workflow"
            >
              <FontAwesomeIcon icon={faDownload} />
            </Button>
          )}
          <Button
            variant="outline"
            size="xs"
            onClick={(e) => {
              e.stopPropagation();
              onEdit(workflow);
            }}
            title="Edit workflow"
            aria-label="Edit workflow"
          >
            <FontAwesomeIcon icon={faEdit} />
          </Button>
          <Button
            variant="danger"
            size="xs"
            onClick={(e) => {
              e.stopPropagation();
              onDelete(workflow.id);
            }}
            title="Delete workflow"
            aria-label="Delete workflow"
          >
            <FontAwesomeIcon icon={faTrashCan} />
          </Button>
        </Box>
      </CardActions>
    </Card>
  );
};
