import { useEffect } from "react";
import { useEditorStore } from "../../../store/editorStore";

export function ThreadMessageReplaceEditorToolCall({
  args,
}: {
  args: { markdown?: string };
}) {
  const markdown = args?.markdown ?? "";

  useEffect(() => {
    if (markdown) {
      useEditorStore.getState().applyExternal(markdown);
    }
  }, [markdown]);

  return (
    <div className="my-1 px-2 py-1 text-xs font-mono text-accent bg-bg rounded border border-border">
      editor updated ({markdown.length} chars)
    </div>
  );
}
