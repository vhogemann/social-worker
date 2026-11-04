import React, { useEffect, type ReactNode } from "react";
import IconButton from "@mui/material/IconButton";
import Typography from "@mui/material/Typography";
import Box from "@mui/material/Box";
import CloseIcon from "@mui/icons-material/Close";

export interface ModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  maxWidth?: "sm" | "md" | "lg" | "xl" | "2xl";
  children: ReactNode;
  footer?: ReactNode;
  testId?: string;
}

const MAX_WIDTH_MAP: Record<NonNullable<ModalProps["maxWidth"]>, number | string> = {
  sm: 400,
  md: 520,
  lg: 640,
  xl: 768,
  "2xl": 896,
};

export function Modal({
  isOpen,
  onClose,
  title,
  description,
  maxWidth = "md",
  children,
  footer,
  testId = "modal",
}: ModalProps) {
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

  if (!isOpen) return null;

  const widthVal = MAX_WIDTH_MAP[maxWidth] || 520;

  return (
    <Box
      sx={{
        position: "fixed",
        inset: 0,
        zIndex: 1300,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: "rgba(0, 0, 0, 0.6)",
        backdropFilter: "blur(4px)",
        p: 2,
      }}
      onClick={onClose}
      data-testid={`${testId}-overlay`}
    >
      <Box
        sx={{
          bgcolor: "background.paper",
          color: "text.primary",
          border: (theme) => `1px solid ${theme.palette.divider}`,
          borderRadius: 3,
          boxShadow: 24,
          width: "100%",
          maxWidth: widthVal,
          mx: "auto",
          overflow: "hidden",
          display: "flex",
          flexDirection: "column",
          maxHeight: "90vh",
        }}
        onClick={(e) => e.stopPropagation()}
        data-testid={testId}
        role="dialog"
        aria-modal="true"
        aria-labelledby={`${testId}-title`}
      >
        <Box
          sx={{
            px: 2.5,
            py: 2,
            borderBottom: (theme) => `1px solid ${theme.palette.divider}`,
            display: "flex",
            alignItems: "flex-start",
            justifyContent: "space-between",
            gap: 2,
            shrink: 0,
          }}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography
              id={`${testId}-title`}
              variant="subtitle1"
              component="h2"
              fontWeight={600}
              color="text.primary"
            >
              {title}
            </Typography>
            {description && (
              <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
                {description}
              </Typography>
            )}
          </Box>
          <IconButton
            type="button"
            onClick={onClose}
            aria-label="Close modal"
            size="small"
            sx={{ color: "text.secondary", mt: -0.5, mr: -0.5 }}
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Box>

        <Box sx={{ p: 2.5, overflowY: "auto", minHeight: 0, flex: 1, display: "flex", flexDirection: "column", gap: 2 }}>
          {children}
        </Box>

        {footer && (
          <Box
            sx={{
              px: 2.5,
              py: 1.75,
              borderTop: (theme) => `1px solid ${theme.palette.divider}`,
              display: "flex",
              alignItems: "center",
              justifyContent: "flex-end",
              gap: 1.5,
              shrink: 0,
            }}
          >
            {footer}
          </Box>
        )}
      </Box>
    </Box>
  );
}
