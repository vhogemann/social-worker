import React from "react";
import { EditorModeSwitcher } from "./EditorModeSwitcher";
import { Button } from "../common/Button";

export interface EditorPanelHeaderProps {
  targetPlatform?: string | null;
  mode: "edit" | "preview";
  onChangeMode: (mode: "edit" | "preview") => void;
  isCanonical: boolean;
  isSent: boolean;
  canPublish: boolean;
  isPublishing: boolean;
  onOpenAdaptModal: () => void;
  onPublish: () => void;
}

export const EditorPanelHeader: React.FC<EditorPanelHeaderProps> = ({
  targetPlatform,
  mode,
  onChangeMode,
  isCanonical,
  isSent,
  canPublish,
  isPublishing,
  onOpenAdaptModal,
  onPublish,
}) => {
  return (
    <div className="px-3 py-2 border-b border-border flex items-center justify-between text-xs font-mono text-muted uppercase tracking-wider select-none">
      <span className="flex items-center gap-2">
        <span>editor</span>
        {targetPlatform && (
          <span className="text-[10px] font-sans font-medium normal-case text-muted bg-border/60 px-1.5 py-0.5 rounded">
            {targetPlatform}
          </span>
        )}
      </span>

      <EditorModeSwitcher mode={mode} onChangeMode={onChangeMode} />

      <div className="flex items-center gap-2 ml-auto">
        {isCanonical && !isSent && (
          <Button
            variant="secondary"
            size="xs"
            onClick={onOpenAdaptModal}
            className="uppercase tracking-wider font-semibold"
          >
            Adapt
          </Button>
        )}
        {canPublish && !isSent && (
          <Button
            variant="primary"
            size="xs"
            onClick={onPublish}
            loading={isPublishing}
            className="uppercase tracking-wider font-semibold"
          >
            {isPublishing ? "Publishing..." : "Publish"}
          </Button>
        )}
      </div>
    </div>
  );
};
