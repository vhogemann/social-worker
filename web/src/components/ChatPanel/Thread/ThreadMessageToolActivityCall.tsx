import { useEffect } from "react";
import { useComposerDraft } from "../../Composer/ComposerDraftContext";

const TOOL_ACTIVITY_LABELS: Record<string, string> = {
  web_search: "web search",
  image_search: "image search",
  list_sources: "sources listed",
  fetch_source: "source fetched",
  add_source: "source added",
  add_image_source: "image added",
  view_image: "image inspected",
  validate_draft: "draft validated",
  render_code_blocks: "code rendered",
  generate_platform_variants: "variants generated",
  format_validate_platform_content: "content formatted",
  publish: "publish executed",
  set_bluesky_reply_target: "reply target set",
};

export function getToolActivityLabel(name: string): string {
  return TOOL_ACTIVITY_LABELS[name] ?? name.replaceAll("_", " ");
}

function extractToolDetails(name: string, args: any): string | null {
  if (!args) return null;
  if (typeof args === "string") return args.slice(0, 60);

  if (name === "web_search" || name === "image_search") {
    return args.query ? `"${args.query}"` : null;
  }
  if (name === "fetch_source" || name === "add_source") {
    return args.title || args.url || null;
  }
  if (name === "add_image_source") {
    return args.url || null;
  }
  if (name === "format_validate_platform_content") {
    return args.platform ? `platform: ${args.platform}` : null;
  }
  if (name === "generate_platform_variants") {
    return Array.isArray(args.platforms) ? args.platforms.join(", ") : null;
  }
  if (name === "set_bluesky_reply_target") {
    return args.replyParentUrl || null;
  }

  return null;
}

export function ThreadMessageToolActivityCall({
  name,
  args,
  result,
  status,
}: {
  name: string;
  args?: any;
  result?: any;
  status?: { type?: string; error?: string };
}) {
  const { setPanelMode } = useComposerDraft();
  const label = getToolActivityLabel(name);
  const isRunning = status?.type === "running";
  const isError = Boolean(status?.type === "error" || (result && (result.error || result.Error)));
  const details = extractToolDetails(name, args);

  useEffect(() => {
    if (name === "publish") {
      setPanelMode("preview");
    }
  }, [name, setPanelMode]);

  return (
    <div
      className="my-1.5 inline-flex items-center gap-2 px-2.5 py-1 text-xs font-mono rounded-md border border-border bg-bg/80 text-foreground shadow-sm"
      data-testid="thread-tool-activity"
    >
      <span
        className={`w-1.5 h-1.5 rounded-full ${
          isError ? "bg-red-400" : isRunning ? "bg-accent animate-pulse" : "bg-emerald-400"
        }`}
      />
      <span className="text-accent font-medium">{label}</span>
      {details && <span className="text-muted text-[11px] truncate max-w-xs">{details}</span>}
      {isRunning && <span className="text-muted text-[10px] animate-pulse">running...</span>}
      {isError && <span className="text-red-400 text-[10px]">failed</span>}
    </div>
  );
}

