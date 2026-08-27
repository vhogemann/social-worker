import React from "react";
import { Button } from "../common/Button";

export interface SourcesFilterBarProps {
  query: string;
  kind: string;
  addedAfter: string;
  addedBefore: string;
  onChangeQuery: (val: string) => void;
  onChangeKind: (val: string) => void;
  onChangeAddedAfter: (val: string) => void;
  onChangeAddedBefore: (val: string) => void;
  onSubmit: (e: React.FormEvent) => void;
  onReset: () => void;
}

export const SourcesFilterBar: React.FC<SourcesFilterBarProps> = ({
  query,
  kind,
  addedAfter,
  addedBefore,
  onChangeQuery,
  onChangeKind,
  onChangeAddedAfter,
  onChangeAddedBefore,
  onSubmit,
  onReset
}) => {
  return (
    <form
      onSubmit={onSubmit}
      className="bg-panel border border-border rounded-xl p-4 mb-6 shrink-0 grid grid-cols-1 md:grid-cols-4 gap-4 items-end"
    >
      <div className="md:col-span-2">
        <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
          Keyword Search
        </label>
        <input
          type="text"
          value={query}
          onChange={(e) => onChangeQuery(e.target.value)}
          placeholder="Search content, titles, or URLs..."
          className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent"
        />
      </div>

      <div>
        <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
          Source Type
        </label>
        <select
          value={kind}
          onChange={(e) => onChangeKind(e.target.value)}
          className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent"
        >
          <option value="all">All Types</option>
          <option value="Url">Web URL</option>
          <option value="YouTube">YouTube Video</option>
          <option value="File">Document / File</option>
        </select>
      </div>

      <div className="flex gap-2">
        <Button type="submit" variant="primary" size="sm" className="flex-1">
          Search
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={onReset}>
          Reset
        </Button>
      </div>

      <div>
        <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
          Added After
        </label>
        <input
          type="date"
          value={addedAfter}
          onChange={(e) => onChangeAddedAfter(e.target.value)}
          className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent"
        />
      </div>

      <div>
        <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">
          Added Before
        </label>
        <input
          type="date"
          value={addedBefore}
          onChange={(e) => onChangeAddedBefore(e.target.value)}
          className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none transition focus:border-accent"
        />
      </div>
    </form>
  );
};
