import { MessagePrimitive, useMessage } from "@assistant-ui/react";
import { ThreadMessageProposeStageTransitionToolCall } from "./ThreadMessageProposeStageTransitionToolCall";
import { ThreadMessageReplaceEditorToolCall } from "./ThreadMessageReplaceEditorToolCall";
import { ThreadMessageTextPart } from "./ThreadMessageTextPart";

export function ThreadMessage() {
  const role = useMessage((m) => m.role);
  return (
    <MessagePrimitive.Root className="px-4 py-3">
      <div className="mb-1 text-xs font-mono uppercase tracking-wider text-muted">
        {role}
      </div>
      <MessagePrimitive.Parts
        components={{
          Text: ThreadMessageTextPart,
          tools: {
            by_name: {
              replace_editor_content: ThreadMessageReplaceEditorToolCall as never,
              propose_stage_transition: ThreadMessageProposeStageTransitionToolCall as never,
            },
          },
        }}
      />
    </MessagePrimitive.Root>
  );
}
