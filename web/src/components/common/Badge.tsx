import React from "react";

export type BadgeVariant = "neutral" | "success" | "warning" | "danger" | "accent";
export type BadgeSize = "xs" | "sm";

export interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  variant?: BadgeVariant;
  size?: BadgeSize;
}

const variantStyles: Record<BadgeVariant, string> = {
  neutral: "bg-zinc-500/10 text-muted border-border",
  success: "bg-green-500/10 text-green-400 border-green-500/20",
  warning: "bg-yellow-500/10 text-yellow-400 border-yellow-500/20",
  danger: "bg-red-500/10 text-red-400 border-red-500/20",
  accent: "bg-accent/10 text-accent border-accent/20"
};

const sizeStyles: Record<BadgeSize, string> = {
  xs: "px-1.5 py-0.5 text-[9px]",
  sm: "px-2 py-0.5 text-[10px]"
};

export const Badge: React.FC<BadgeProps> = ({
  children,
  variant = "neutral",
  size = "sm",
  className = "",
  ...props
}) => {
  return (
    <span
      className={`inline-flex items-center rounded-full font-mono font-medium border ${variantStyles[variant]} ${sizeStyles[size]} ${className}`}
      {...props}
    >
      {children}
    </span>
  );
};
