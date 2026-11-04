import React from "react";
import MuiButton, { type ButtonProps as MuiButtonProps } from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import type { IconDefinition } from "@fortawesome/free-solid-svg-icons";

export type ButtonVariant = "primary" | "secondary" | "danger" | "ghost" | "outline";
export type ButtonSize = "xs" | "sm" | "md";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  icon?: IconDefinition | React.ReactNode;
  iconPosition?: "left" | "right";
}

const variantStyles: Record<ButtonVariant, { muiVariant: MuiButtonProps["variant"]; muiColor: MuiButtonProps["color"] }> = {
  primary: { muiVariant: "contained", muiColor: "primary" },
  secondary: { muiVariant: "outlined", muiColor: "inherit" },
  danger: { muiVariant: "outlined", muiColor: "error" },
  ghost: { muiVariant: "text", muiColor: "inherit" },
  outline: { muiVariant: "outlined", muiColor: "inherit" },
};

export const Button: React.FC<ButtonProps> = ({
  children,
  variant = "secondary",
  size = "sm",
  loading = false,
  disabled = false,
  icon,
  iconPosition = "left",
  className = "",
  type = "button",
  ...props
}) => {
  const isDisabled = disabled || loading;
  const config = variantStyles[variant] || variantStyles.secondary;

  const renderIcon = () => {
    if (loading) {
      return <CircularProgress size={13} color="inherit" />;
    }
    if (!icon) return null;
    if (typeof icon === "object" && "icon" in (icon as object)) {
      return <FontAwesomeIcon icon={icon as IconDefinition} className="text-xs" />;
    }
    return icon;
  };

  const startIcon = iconPosition === "left" ? renderIcon() : loading ? renderIcon() : null;
  const endIcon = !loading && iconPosition === "right" ? renderIcon() : null;

  const sizeSx = {
    xs: { py: 0.25, px: 1, fontSize: "0.75rem", minHeight: 28 },
    sm: { py: 0.5, px: 1.5, fontSize: "0.8125rem", minHeight: 32 },
    md: { py: 0.75, px: 2, fontSize: "0.875rem", minHeight: 36 },
  }[size];

  return (
    <MuiButton
      type={type}
      disabled={isDisabled}
      variant={config.muiVariant}
      color={config.muiColor}
      startIcon={startIcon}
      endIcon={endIcon}
      className={className}
      sx={{
        borderRadius: 2,
        fontWeight: 600,
        textTransform: "none",
        ...sizeSx,
      }}
      {...(props as any)}
    >
      {children}
    </MuiButton>
  );
};
