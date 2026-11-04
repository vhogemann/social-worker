import React, { useState } from "react";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Typography from "@mui/material/Typography";
import Chip from "@mui/material/Chip";
import TextField from "@mui/material/TextField";
import IconButton from "@mui/material/IconButton";
import Tooltip from "@mui/material/Tooltip";
import Divider from "@mui/material/Divider";
import Paper from "@mui/material/Paper";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPenToSquare,
  faBoxArchive,
  faArrowRotateLeft,
  faTrashCan,
  faBolt,
  faLayerGroup,
  faClock
} from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_ICONS } from "../../config/platformBadges";
import type { DraftDto } from "../../api/drafts";

export interface DraftCardProps {
  draft: DraftDto;
  onSelect: (id: string, workflowId?: string | null) => void;
  onSelectVariant?: (canonicalId: string, variantId: string, workflowId?: string | null) => void;
  onArchive: (id: string) => void;
  onUnarchive: (id: string) => void;
  onDelete: (id: string) => void;
  onRename: (id: string, newTitle: string) => void;
}

export const DraftCard: React.FC<DraftCardProps> = ({
  draft,
  onSelect,
  onSelectVariant,
  onArchive,
  onUnarchive,
  onDelete,
  onRename
}) => {
  const [isEditing, setIsEditing] = useState(false);
  const [editTitle, setEditTitle] = useState(draft.title);

  const handleSaveTitle = () => {
    setIsEditing(false);
    if (editTitle.trim() && editTitle.trim() !== draft.title) {
      onRename(draft.id, editTitle.trim());
    } else {
      setEditTitle(draft.title);
    }
  };

  const formattedDate = new Date(draft.updatedAt).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric"
  });

  const variantCount = draft.variants?.length || 0;

  const handleVariantClick = (e: React.MouseEvent, variantId: string) => {
    e.stopPropagation();
    if (onSelectVariant) {
      onSelectVariant(draft.id, variantId, draft.automationWorkflowId);
    } else {
      onSelect(variantId, draft.automationWorkflowId);
    }
  };

  return (
    <Card
      variant="outlined"
      onClick={() => onSelect(draft.id, draft.automationWorkflowId)}
      data-testid="draft-card"
      sx={{
        borderRadius: 3,
        display: "flex",
        flexDirection: "column",
        justifyContent: "space-between",
        cursor: "pointer",
        transition: "border-color 0.2s, box-shadow 0.2s",
        "&:hover": {
          borderColor: "primary.main",
          boxShadow: 2
        }
      }}
    >
      <CardContent sx={{ p: 2.5, display: "flex", flexDirection: "column", gap: 1.5 }}>
        {/* Title and Status Badge */}
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 1.5 }}>
          {isEditing ? (
            <TextField
              size="small"
              value={editTitle}
              autoFocus
              onClick={(e) => e.stopPropagation()}
              onChange={(e) => setEditTitle(e.target.value)}
              onBlur={handleSaveTitle}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleSaveTitle();
                if (e.key === "Escape") {
                  setIsEditing(false);
                  setEditTitle(draft.title);
                }
              }}
              fullWidth
              sx={{ "& input": { py: 0.5, px: 1, fontWeight: 700, fontSize: "0.875rem" } }}
            />
          ) : (
            <Typography
              variant="subtitle2"
              sx={{
                fontWeight: 700,
                lineHeight: 1.35,
                overflow: "hidden",
                display: "-webkit-box",
                WebkitLineClamp: 2,
                WebkitBoxOrient: "vertical",
                "&:hover": { color: "primary.main" }
              }}
            >
              {draft.title}
            </Typography>
          )}

          <Box sx={{ shrink: 0 }} onClick={(e) => e.stopPropagation()}>
            <Chip
              size="small"
              label={
                draft.status === "Discarded" && draft.relevanceScore != null
                  ? `Discarded (${draft.relevanceScore}/10)`
                  : draft.status
              }
              color={
                draft.status === "Published" || draft.status === "Sent"
                  ? "success"
                  : draft.status === "Archived"
                  ? "default"
                  : draft.status === "Scheduled" || draft.status === "Discarded"
                  ? "warning"
                  : "primary"
              }
              variant="outlined"
              sx={{ fontWeight: 600, fontSize: "0.7rem", height: 22 }}
            />
          </Box>
        </Box>

        {/* Discard Reason Alert Box */}
        {draft.status === "Discarded" && draft.discardReason && (
          <Paper
            variant="outlined"
            sx={{
              p: 1.25,
              bgcolor: "action.hover",
              fontStyle: "italic",
              borderRadius: 1.5
            }}
          >
            <Typography
              variant="caption"
              sx={{
                color: "text.secondary",
                display: "-webkit-box",
                WebkitLineClamp: 2,
                WebkitBoxOrient: "vertical",
                overflow: "hidden"
              }}
            >
              "{draft.discardReason}"
            </Typography>
          </Paper>
        )}

        {/* Content Snippet */}
        {draft.content && draft.status !== "Discarded" && (
          <Typography
            variant="body2"
            sx={{
              color: "text.secondary",
              fontSize: "0.8125rem",
              lineHeight: 1.5,
              overflow: "hidden",
              display: "-webkit-box",
              WebkitLineClamp: 3,
              WebkitBoxOrient: "vertical"
            }}
          >
            {draft.content}
          </Typography>
        )}

        {/* Workflow Title Pill */}
        {draft.automationWorkflowTitle && (
          <Box sx={{ display: "flex", alignItems: "center", gap: 1, color: "primary.main" }}>
            <FontAwesomeIcon icon={faBolt} className="text-xs" />
            <Typography
              variant="caption"
              sx={{
                fontWeight: 600,
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap"
              }}
            >
              {draft.automationWorkflowTitle}
            </Typography>
          </Box>
        )}

        {/* Clickable Variant Chips */}
        {draft.variants && draft.variants.length > 0 && (
          <Box
            sx={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 0.75, mt: 0.5 }}
            onClick={(e) => e.stopPropagation()}
          >
            {draft.variants.map((v) => (
              <Chip
                key={v.id}
                component="button"
                clickable
                size="small"
                onClick={(e) => handleVariantClick(e, v.id)}
                icon={
                  v.targetPlatform && PLATFORM_ICONS[v.targetPlatform] ? (
                    <FontAwesomeIcon
                      icon={PLATFORM_ICONS[v.targetPlatform]}
                      style={{ fontSize: "0.75rem", marginLeft: 6 }}
                    />
                  ) : undefined
                }
                label={v.targetPlatform || v.title}
                title={`Open ${v.targetPlatform} variant`}
                data-testid={`card-variant-${v.id}`}
                variant="outlined"
                sx={{ height: 22, fontSize: "0.7rem", fontWeight: 600 }}
              />
            ))}
          </Box>
        )}
      </CardContent>

      {/* Footer Actions and Metadata */}
      <Box sx={{ mt: "auto" }}>
        <Divider />
        <Box
          sx={{
            px: 2.5,
            py: 1.5,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center"
          }}
        >
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            {draft.targetPlatform && (
              <Chip
                size="small"
                icon={
                  PLATFORM_ICONS[draft.targetPlatform] ? (
                    <FontAwesomeIcon
                      icon={PLATFORM_ICONS[draft.targetPlatform]}
                      style={{ fontSize: "0.75rem", marginLeft: 6 }}
                    />
                  ) : undefined
                }
                label={draft.targetPlatform}
                variant="filled"
                sx={{ height: 22, fontSize: "0.7rem", fontWeight: 600 }}
              />
            )}

            {variantCount > 0 && (
              <Chip
                size="small"
                icon={
                  <FontAwesomeIcon icon={faLayerGroup} style={{ fontSize: "0.7rem", marginLeft: 6 }} />
                }
                label={`${variantCount} ${variantCount === 1 ? "variant" : "variants"}`}
                variant="outlined"
                sx={{ height: 22, fontSize: "0.7rem", fontFamily: "monospace" }}
              />
            )}
          </Box>

          <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 0.5, color: "text.secondary" }}>
              <FontAwesomeIcon icon={faClock} className="text-[10px]" />
              <Typography variant="caption" sx={{ fontSize: "0.7rem" }}>
                {formattedDate}
              </Typography>
            </Box>

            <Box
              sx={{ display: "flex", alignItems: "center", gap: 0.5 }}
              onClick={(e) => e.stopPropagation()}
            >
              {draft.status === "Archived" ? (
                <>
                  <Tooltip title="Restore Draft">
                    <IconButton
                      size="small"
                      onClick={() => onUnarchive(draft.id)}
                      sx={{ p: 0.5 }}
                      aria-label="Restore Draft"
                    >
                      <FontAwesomeIcon icon={faArrowRotateLeft} className="text-xs" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete Draft">
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => onDelete(draft.id)}
                      sx={{ p: 0.5 }}
                      aria-label="Delete Draft"
                    >
                      <FontAwesomeIcon icon={faTrashCan} className="text-xs" />
                    </IconButton>
                  </Tooltip>
                </>
              ) : (
                <>
                  <Tooltip title="Rename Draft">
                    <IconButton
                      size="small"
                      onClick={() => setIsEditing(true)}
                      sx={{ p: 0.5 }}
                      aria-label="Rename Draft"
                    >
                      <FontAwesomeIcon icon={faPenToSquare} className="text-xs" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Archive Draft">
                    <IconButton
                      size="small"
                      onClick={() => onArchive(draft.id)}
                      sx={{ p: 0.5 }}
                      aria-label="Archive Draft"
                    >
                      <FontAwesomeIcon icon={faBoxArchive} className="text-xs" />
                    </IconButton>
                  </Tooltip>
                  <Tooltip title="Delete Draft">
                    <IconButton
                      size="small"
                      color="error"
                      onClick={() => onDelete(draft.id)}
                      sx={{ p: 0.5 }}
                      aria-label="Delete Draft"
                    >
                      <FontAwesomeIcon icon={faTrashCan} className="text-xs" />
                    </IconButton>
                  </Tooltip>
                </>
              )}
            </Box>
          </Box>
        </Box>
      </Box>
    </Card>
  );
};
