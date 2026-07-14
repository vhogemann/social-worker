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
};

export function getToolActivityLabel(name: string): string {
  return TOOL_ACTIVITY_LABELS[name] ?? name.replaceAll("_", " ");
}

export function ThreadMessageToolActivityCall({
  name,
}: {
  name: string;
  args?: unknown;
}) {
  return (
    <div className="my-1 px-2 py-1 text-xs font-mono text-muted bg-bg rounded border border-border">
      activity: {getToolActivityLabel(name)}
    </div>
  );
}
