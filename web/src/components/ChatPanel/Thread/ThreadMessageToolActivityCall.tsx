import { useEffect } from "react";
import { useComposerDraft } from "../../Composer/ComposerDraftContext";
import { useMessage } from "@assistant-ui/react";

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

export function ThreadMessageToolActivityCall({
  name,
}: {
  name: string;
  args?: unknown;
}) {
  const { activeDraftId, addActivityCard, setPanelMode } = useComposerDraft();
  const messageId = useMessage((m) => m.id as string | undefined);

  useEffect(() => {
    if (name === "publish") {
      setPanelMode("preview");
    }
  }, [name, setPanelMode]);

  useEffect(() => {
    if (!activeDraftId || !messageId) {
      return;
    }

    if (name === "validate_draft") {
      return;
    }

    const label = getToolActivityLabel(name);
    const message = `${label} executed.`;

    addActivityCard({
      sourceKey: `tool:${messageId}:${name}`,
      title: label,
      message,
      kind: "info",
    });
  }, [activeDraftId, addActivityCard, messageId, name]);

  return null;
}
