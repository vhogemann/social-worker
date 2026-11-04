import React, { forwardRef } from "react";
import TextField from "@mui/material/TextField";

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
      <TextField
        inputRef={ref}
        id={inputId}
        label={label}
        required={required}
        error={Boolean(error)}
        helperText={error || hint}
        size="small"
        fullWidth
        variant="outlined"
        className={className}
        inputProps={{
          ...props,
          style: mono ? { fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace", fontSize: "0.8125rem" } : undefined,
        }}
        slotProps={{
          inputLabel: {
            shrink: true,
            sx: {
              fontSize: "0.8125rem",
              fontWeight: 500,
            },
          },
        }}
        sx={{
          "& .MuiOutlinedInput-root": {
            borderRadius: 2,
            fontSize: "0.875rem",
          },
          "& .MuiFormHelperText-root": {
            fontFamily: "ui-monospace, SFMono-Regular, Menlo, monospace",
            fontSize: "0.6875rem",
            mx: 0.5,
          },
        }}
      />
    );
  }
);

Input.displayName = "Input";
