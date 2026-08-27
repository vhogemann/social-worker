import React, { forwardRef } from "react";

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string | null;
  hint?: string;
  mono?: boolean;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ label, error, hint, mono = false, className = "", id, required, ...props }, ref) => {
    const inputId = id || (label ? label.toLowerCase().replace(/\s+/g, "-") : undefined);

    return (
      <div className="w-full flex flex-col gap-1.5">
        {label && (
          <label htmlFor={inputId} className="block text-[10px] font-mono uppercase tracking-wider text-muted">
            {label}
            {required && <span className="text-accent ml-1">*</span>}
          </label>
        )}
        <input
          ref={ref}
          id={inputId}
          required={required}
          className={`w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent disabled:opacity-50 disabled:cursor-not-allowed ${
            mono ? "font-mono" : ""
          } ${error ? "border-red-500/50 focus:border-red-500" : ""} ${className}`}
          {...props}
        />
        {hint && !error && <span className="text-[10px] text-muted font-mono">{hint}</span>}
        {error && <span className="text-[10px] text-red-400 font-mono">{error}</span>}
      </div>
    );
  }
);

Input.displayName = "Input";
