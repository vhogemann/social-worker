import React from "react";
import Chip from "@mui/material/Chip";

export type BadgeVariant = "neutral" | "success" | "warning" | "danger" | "accent";
export type BadgeSize = "xs" | "sm";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
  size?: BadgeSize;
}

const colorMap: Record<BadgeVariant, "default" | "success" | "warning" | "error" | "primary"> = {
  neutral: "default",
  success: "success",
  warning: "warning",
  danger: "error",
  accent: "primary",
};

const variantStyles: Record<BadgeVariant, string> = {
  neutral: "bg-zinc-500/10 text-muted border-border",
  success: "bg-green-500/10 text-green-400 border-green-500/20",
  warning: "bg-yellow-500/10 text-yellow-400 border-yellow-500/20",
  danger: "bg-red-500/10 text-red-400 border-red-500/20",
  accent: "bg-accent/10 text-accent border-accent/20",
};

export const Badge: React.FC<BadgeProps> = ({
  children,
  variant = "neutral",
  size = "sm",
  className = "",
  ...props
}) => {
  return (
    <Chip
      component="span"
      label={children}
      size="small"
      color={colorMap[variant]}
      variant="outlined"
      className={`${variantStyles[variant]} ${className}`}
      classes={{
        label: variantStyles[variant],
      }}
      sx={{
        height: size === "xs" ? 20 : 24,
        fontSize: size === "xs" ? "0.6875rem" : "0.75rem",
        fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
        fontWeight: 500,
        borderRadius: 1.5,
        "& .MuiChip-label": {
          px: size === "xs" ? 0.75 : 1,
        },
      }}
      {...(props as any)}
    />
  );
};
