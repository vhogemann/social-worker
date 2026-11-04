import React, { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import Box from "@mui/material/Box";
import Drawer from "@mui/material/Drawer";
import Typography from "@mui/material/Typography";
import IconButton from "@mui/material/IconButton";
import Divider from "@mui/material/Divider";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemIcon from "@mui/material/ListItemIcon";
import ListItemText from "@mui/material/ListItemText";
import Chip from "@mui/material/Chip";
import CloseIcon from "@mui/icons-material/Close";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faEye,
  faPenToSquare,
  faComments,
  faLink,
  faCheck,
  faLayerGroup,
  faFolderOpen,
  faBolt
} from "@fortawesome/free-solid-svg-icons";

export interface DraftMenuDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  activeView: "preview" | "editor" | "chat";
  onSelectView: (view: "preview" | "editor" | "chat") => void;
  onOpenAdaptModal?: () => void;
  workflowId?: string | null;
  canonicalDraftId?: string | null;
  activeDraftId?: string | null;
}

export const DraftMenuDrawer: React.FC<DraftMenuDrawerProps> = ({
  isOpen,
  onClose,
  activeView,
  onSelectView,
  onOpenAdaptModal,
  workflowId
}) => {
  const navigate = useNavigate();
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!isOpen) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onClose();
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose]);

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Best-effort copy fallback
    }
  };

  const handleSelect = (view: "preview" | "editor" | "chat") => {
    onSelectView(view);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <Drawer
      anchor="left"
      open={isOpen}
      onClose={onClose}
      slotProps={{
        backdrop: {
          ...({ "data-testid": "draft-menu-drawer-backdrop" } as any)
        }
      }}
      PaperProps={{
        "data-testid": "draft-menu-drawer",
        sx: {
          width: 300,
          bgcolor: "background.paper",
          display: "flex",
          flexDirection: "column",
          justifyContent: "space-between"
        }
      }}
    >
      <Box sx={{ p: 2, display: "flex", flexDirection: "column", gap: 2 }}>
        {/* Header */}
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            pb: 1.5,
            borderBottom: 1,
            borderColor: "divider"
          }}
        >
          <Typography
            variant="caption"
            sx={{
              fontWeight: 700,
              textTransform: "uppercase",
              letterSpacing: "0.08em",
              color: "text.secondary"
            }}
          >
            Draft Menu
          </Typography>
          <IconButton
            size="small"
            onClick={onClose}
            data-testid="draft-menu-close-btn"
            aria-label="Close menu"
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>

        {/* Section 1: Views */}
        <Box>
          <Typography
            variant="caption"
            sx={{
              px: 1,
              py: 0.5,
              display: "block",
              fontWeight: 700,
              textTransform: "uppercase",
              letterSpacing: "0.05em",
              color: "text.secondary",
              fontSize: "0.6875rem"
            }}
          >
            Views
          </Typography>
          <List disablePadding sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            <ListItemButton
              data-testid="menu-view-preview"
              selected={activeView === "preview"}
              onClick={() => handleSelect("preview")}
              sx={{
                borderRadius: 2,
                "&.Mui-selected": {
                  bgcolor: "action.selected",
                  fontWeight: 700
                }
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 36,
                  color: activeView === "preview" ? "primary.main" : "text.secondary"
                }}
              >
                <FontAwesomeIcon icon={faEye} />
              </ListItemIcon>
              <ListItemText
                primary="Thread Preview"
                primaryTypographyProps={{
                  variant: "body2",
                  fontWeight: activeView === "preview" ? 700 : 500
                }}
              />
              {activeView === "preview" && (
                <Chip
                  label="Active"
                  size="small"
                  color="primary"
                  sx={{ height: 20, fontSize: "0.65rem", fontWeight: 700 }}
                />
              )}
            </ListItemButton>

            <ListItemButton
              data-testid="menu-view-editor"
              selected={activeView === "editor"}
              onClick={() => handleSelect("editor")}
              sx={{
                borderRadius: 2,
                "&.Mui-selected": {
                  bgcolor: "action.selected",
                  fontWeight: 700
                }
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 36,
                  color: activeView === "editor" ? "primary.main" : "text.secondary"
                }}
              >
                <FontAwesomeIcon icon={faPenToSquare} />
              </ListItemIcon>
              <ListItemText
                primary="Markdown Editor"
                primaryTypographyProps={{
                  variant: "body2",
                  fontWeight: activeView === "editor" ? 700 : 500
                }}
              />
              {activeView === "editor" && (
                <Chip
                  label="Active"
                  size="small"
                  color="primary"
                  sx={{ height: 20, fontSize: "0.65rem", fontWeight: 700 }}
                />
              )}
            </ListItemButton>

            <ListItemButton
              data-testid="menu-view-chat"
              selected={activeView === "chat"}
              onClick={() => handleSelect("chat")}
              sx={{
                borderRadius: 2,
                "&.Mui-selected": {
                  bgcolor: "action.selected",
                  fontWeight: 700
                }
              }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 36,
                  color: activeView === "chat" ? "primary.main" : "text.secondary"
                }}
              >
                <FontAwesomeIcon icon={faComments} />
              </ListItemIcon>
              <ListItemText
                primary="AI Agent Chat"
                primaryTypographyProps={{
                  variant: "body2",
                  fontWeight: activeView === "chat" ? 700 : 500
                }}
              />
              {activeView === "chat" && (
                <Chip
                  label="Active"
                  size="small"
                  color="primary"
                  sx={{ height: 20, fontSize: "0.65rem", fontWeight: 700 }}
                />
              )}
            </ListItemButton>
          </List>
        </Box>

        <Divider />

        {/* Section 2: Actions */}
        <Box>
          <Typography
            variant="caption"
            sx={{
              px: 1,
              py: 0.5,
              display: "block",
              fontWeight: 700,
              textTransform: "uppercase",
              letterSpacing: "0.05em",
              color: "text.secondary",
              fontSize: "0.6875rem"
            }}
          >
            Actions
          </Typography>
          <List disablePadding sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            <ListItemButton
              data-testid="menu-copy-draft-link"
              onClick={handleCopyLink}
              sx={{ borderRadius: 2 }}
            >
              <ListItemIcon
                sx={{
                  minWidth: 36,
                  color: copied ? "success.main" : "text.secondary"
                }}
              >
                <FontAwesomeIcon icon={copied ? faCheck : faLink} />
              </ListItemIcon>
              <ListItemText
                primary={copied ? "Link Copied!" : "Copy Draft Link"}
                primaryTypographyProps={{ variant: "body2" }}
              />
            </ListItemButton>

            {onOpenAdaptModal && (
              <ListItemButton
                data-testid="menu-adapt-platforms"
                onClick={() => {
                  onClose();
                  onOpenAdaptModal();
                }}
                sx={{ borderRadius: 2 }}
              >
                <ListItemIcon sx={{ minWidth: 36, color: "text.secondary" }}>
                  <FontAwesomeIcon icon={faLayerGroup} />
                </ListItemIcon>
                <ListItemText
                  primary="Adapt for Platforms"
                  primaryTypographyProps={{ variant: "body2" }}
                />
              </ListItemButton>
            )}
          </List>
        </Box>

        <Divider />

        {/* Section 3: Navigation */}
        <Box>
          <Typography
            variant="caption"
            sx={{
              px: 1,
              py: 0.5,
              display: "block",
              fontWeight: 700,
              textTransform: "uppercase",
              letterSpacing: "0.05em",
              color: "text.secondary",
              fontSize: "0.6875rem"
            }}
          >
            Navigation
          </Typography>
          <List disablePadding sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            <ListItemButton
              data-testid="menu-nav-drafts"
              onClick={() => {
                onClose();
                navigate("/drafts");
              }}
              sx={{ borderRadius: 2 }}
            >
              <ListItemIcon sx={{ minWidth: 36, color: "text.secondary" }}>
                <FontAwesomeIcon icon={faFolderOpen} />
              </ListItemIcon>
              <ListItemText
                primary="Drafts Library"
                primaryTypographyProps={{ variant: "body2" }}
              />
            </ListItemButton>

            {workflowId && (
              <ListItemButton
                data-testid="menu-nav-workflow"
                onClick={() => {
                  onClose();
                  navigate(`/workflows/${workflowId}`);
                }}
                sx={{ borderRadius: 2 }}
              >
                <ListItemIcon sx={{ minWidth: 36, color: "primary.main" }}>
                  <FontAwesomeIcon icon={faBolt} />
                </ListItemIcon>
                <ListItemText
                  primary="Back to Workflow"
                  primaryTypographyProps={{ variant: "body2" }}
                />
              </ListItemButton>
            )}
          </List>
        </Box>
      </Box>
    </Drawer>
  );
};
