import React from "react";
import { Button } from "../common/Button";

export interface FeedDiscoveryFormProps {
  inputUrl: string;
  onChangeInputUrl: (value: string) => void;
  onDiscover: () => void;
  discovering: boolean;
  error?: string | null;
}

export const FeedDiscoveryForm: React.FC<FeedDiscoveryFormProps> = ({
  inputUrl,
  onChangeInputUrl,
  onDiscover,
  discovering,
  error
}) => {
  return (
    <div className="p-3 bg-bg border border-border rounded-lg flex flex-col gap-2">
      <label className="text-[10px] font-mono uppercase tracking-wider text-muted">
        Auto-Discover Feed URL (RSS / Atom / YouTube)
      </label>
      <div className="flex gap-2">
        <input
          type="url"
          placeholder="e.g. https://youtube.com/@channel or blog URL"
          value={inputUrl}
          onChange={(e) => onChangeInputUrl(e.target.value)}
          className="flex-1 rounded-lg border border-border bg-panel px-3 py-1.5 text-xs text-foreground outline-none focus:border-accent"
        />
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={onDiscover}
          loading={discovering}
          disabled={!inputUrl.trim()}
          className="font-mono text-xs"
        >
          Discover
        </Button>
      </div>
      {error && <p className="text-[10px] text-red-400 font-mono">{error}</p>}
    </div>
  );
};
