import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner, type IconDefinition } from "@fortawesome/free-solid-svg-icons";

export type ButtonVariant = "primary" | "secondary" | "danger" | "ghost" | "outline";
export type ButtonSize = "xs" | "sm" | "md";

export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  icon?: IconDefinition;
  iconPosition?: "left" | "right";
}

const variantStyles: Record<ButtonVariant, string> = {
  primary: "bg-accent text-accent-foreground font-semibold hover:opacity-90",
  secondary: "bg-panel border border-border text-foreground hover:bg-bg",
  danger: "border border-border text-muted hover:border-red-500/40 hover:bg-red-500/10 hover:text-red-400",
  ghost: "text-muted hover:text-foreground hover:bg-panel",
  outline: "border border-border text-muted hover:text-foreground hover:bg-panel hover:border-zinc-700"
};

const sizeStyles: Record<ButtonSize, string> = {
  xs: "px-2.5 py-1 text-xs rounded-lg gap-1.5",
  sm: "px-3 py-1.5 text-xs rounded-lg gap-1.5",
  md: "px-4 py-2 text-sm rounded-lg gap-2"
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

  return (
    <button
      type={type}
      disabled={isDisabled}
      className={`inline-flex items-center justify-center font-medium transition disabled:opacity-50 disabled:cursor-not-allowed ${variantStyles[variant]} ${sizeStyles[size]} ${className}`}
      {...props}
    >
      {loading ? (
        <FontAwesomeIcon icon={faSpinner} spin className="text-xs" />
      ) : icon && iconPosition === "left" ? (
        <FontAwesomeIcon icon={icon} className="text-xs" />
      ) : null}
      {children && <span>{children}</span>}
      {!loading && icon && iconPosition === "right" && (
        <FontAwesomeIcon icon={icon} className="text-xs" />
      )}
    </button>
  );
};
