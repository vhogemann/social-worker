import React from "react";

export interface EditorModeSwitcherProps {
  mode: "edit" | "preview";
  onChangeMode: (mode: "edit" | "preview") => void;
}

export const EditorModeSwitcher: React.FC<EditorModeSwitcherProps> = ({
  mode,
  onChangeMode,
}) => {
  return (
    <div className="flex items-center bg-bg border border-border rounded-lg p-0.5 font-sans lowercase">
      <button
        onClick={() => onChangeMode("edit")}
        className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
          mode === "edit"
            ? "bg-border text-foreground shadow-sm font-semibold"
            : "text-muted hover:text-foreground"
        }`}
      >
        edit
      </button>
      <button
        onClick={() => onChangeMode("preview")}
        className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
          mode === "preview"
            ? "bg-border text-foreground shadow-sm font-semibold"
            : "text-muted hover:text-foreground"
        }`}
      >
        preview
      </button>
    </div>
  );
};
