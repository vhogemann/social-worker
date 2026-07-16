import { useEffect } from "react";
import { useEditorStore } from "../../../store/editorStore";
import { useChatStore } from "../../../store/chatStore";
import { useDraftStore } from "../../../store/draftStore";
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
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const addActivityCard = useChatStore((s) => s.addActivityCard);
  const messageId = useMessage((m) => m.id as string | undefined);

  useEffect(() => {
    if (name === "publish") {
      useEditorStore.getState().setPanelMode("preview");
    }
  }, [name]);

  useEffect(() => {
    if (!activeDraftId || !messageId) {
      return;
    }

    // validate_draft already renders rich, inline validation chips in the message stream.
    // Skip separate activity card emission to avoid duplicate feedback channels.
    if (name === "validate_draft") {
      return;
    }

    const label = getToolActivityLabel(name);
    const message = `${label} executed.`;

    addActivityCard(activeDraftId, {
      sourceKey: `tool:${messageId}:${name}`,
      title: label,
      message,
      kind: "info",
    });
  }, [activeDraftId, addActivityCard, messageId, name]);

  return null;
}
