import { MessagePrimitive, useMessage } from "@assistant-ui/react";
import { ThreadMessageProposeStageTransitionToolCall } from "./ThreadMessageProposeStageTransitionToolCall";
import { ThreadMessageReplaceEditorToolCall } from "./ThreadMessageReplaceEditorToolCall";
import { ThreadMessageTextPart } from "./ThreadMessageTextPart";
import { ThreadMessageToolActivityCall } from "./ThreadMessageToolActivityCall";

const WebSearchToolCall = () => <ThreadMessageToolActivityCall name="web_search" />;
const ImageSearchToolCall = () => <ThreadMessageToolActivityCall name="image_search" />;
const ListSourcesToolCall = () => <ThreadMessageToolActivityCall name="list_sources" />;
const FetchSourceToolCall = () => <ThreadMessageToolActivityCall name="fetch_source" />;
const AddSourceToolCall = () => <ThreadMessageToolActivityCall name="add_source" />;
const AddImageSourceToolCall = () => <ThreadMessageToolActivityCall name="add_image_source" />;
const ViewImageToolCall = () => <ThreadMessageToolActivityCall name="view_image" />;
const ValidateDraftToolCall = () => <ThreadMessageToolActivityCall name="validate_draft" />;
const RenderCodeBlocksToolCall = () => <ThreadMessageToolActivityCall name="render_code_blocks" />;
const GeneratePlatformVariantsToolCall = () => <ThreadMessageToolActivityCall name="generate_platform_variants" />;
const FormatValidatePlatformContentToolCall = () => <ThreadMessageToolActivityCall name="format_validate_platform_content" />;
const PublishToolCall = () => <ThreadMessageToolActivityCall name="publish" />;

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
              web_search: WebSearchToolCall as never,
              image_search: ImageSearchToolCall as never,
              list_sources: ListSourcesToolCall as never,
              fetch_source: FetchSourceToolCall as never,
              add_source: AddSourceToolCall as never,
              add_image_source: AddImageSourceToolCall as never,
              view_image: ViewImageToolCall as never,
              validate_draft: ValidateDraftToolCall as never,
              render_code_blocks: RenderCodeBlocksToolCall as never,
              generate_platform_variants: GeneratePlatformVariantsToolCall as never,
              format_validate_platform_content: FormatValidatePlatformContentToolCall as never,
              publish: PublishToolCall as never,
            },
          },
        }}
      />
    </MessagePrimitive.Root>
  );
}
