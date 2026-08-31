import { useEffect } from "react";
import { useComposerDraft } from "../../Composer/ComposerDraftContext";

export function ThreadMessageReplaceEditorToolCall({
  args,
}: {
  args: { markdown?: string };
}) {
  const markdown = args?.markdown ?? "";
  const { applyExternal } = useComposerDraft();

  useEffect(() => {
    if (markdown) {
      applyExternal(markdown);
    }
  }, [markdown, applyExternal]);

  return (
    <div className="my-1 px-2 py-1 text-xs font-mono text-accent bg-bg rounded border border-border">
      editor updated ({markdown.length} chars)
    </div>
  );
}
