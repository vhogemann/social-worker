import React from "react";
import Alert from "@mui/material/Alert";
import IconButton from "@mui/material/IconButton";
import CloseIcon from "@mui/icons-material/Close";

export type AlertBannerVariant = "error" | "warning" | "info" | "success";

export interface AlertBannerProps {
  variant?: AlertBannerVariant;
  message?: string;
  children?: React.ReactNode;
  onDismiss?: () => void;
  className?: string;
}

export const AlertBanner: React.FC<AlertBannerProps> = ({
  variant = "error",
  message,
  children,
  onDismiss,
  className = "",
}) => {
  return (
    <Alert
      severity={variant}
      variant="outlined"
      action={
        onDismiss ? (
          <IconButton
            aria-label="Dismiss alert"
            color="inherit"
            size="small"
            onClick={onDismiss}
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        ) : undefined
      }
      className={className}
      sx={{
        borderRadius: 2,
        fontSize: "0.8125rem",
        alignItems: "center",
        py: 0.5,
      }}
    >
      {message || children}
    </Alert>
  );
};
