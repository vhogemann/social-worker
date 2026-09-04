import { MessagePrimitive, useMessage } from "@assistant-ui/react";
import { ThreadMessageProposeStageTransitionToolCall } from "./ThreadMessageProposeStageTransitionToolCall";
import { ThreadMessageReplaceEditorToolCall } from "./ThreadMessageReplaceEditorToolCall";
import { ThreadMessageTextPart } from "./ThreadMessageTextPart";
import { ThreadMessageToolActivityCall } from "./ThreadMessageToolActivityCall";

const WebSearchToolCall = (props: any) => <ThreadMessageToolActivityCall name="web_search" {...props} />;
const ImageSearchToolCall = (props: any) => <ThreadMessageToolActivityCall name="image_search" {...props} />;
const ListSourcesToolCall = (props: any) => <ThreadMessageToolActivityCall name="list_sources" {...props} />;
const FetchSourceToolCall = (props: any) => <ThreadMessageToolActivityCall name="fetch_source" {...props} />;
const AddSourceToolCall = (props: any) => <ThreadMessageToolActivityCall name="add_source" {...props} />;
const AddImageSourceToolCall = (props: any) => <ThreadMessageToolActivityCall name="add_image_source" {...props} />;
const ViewImageToolCall = (props: any) => <ThreadMessageToolActivityCall name="view_image" {...props} />;
const ValidateDraftToolCall = (props: any) => <ThreadMessageToolActivityCall name="validate_draft" {...props} />;
const RenderCodeBlocksToolCall = (props: any) => <ThreadMessageToolActivityCall name="render_code_blocks" {...props} />;
const GeneratePlatformVariantsToolCall = (props: any) => <ThreadMessageToolActivityCall name="generate_platform_variants" {...props} />;
const FormatValidatePlatformContentToolCall = (props: any) => <ThreadMessageToolActivityCall name="format_validate_platform_content" {...props} />;
const PublishToolCall = (props: any) => <ThreadMessageToolActivityCall name="publish" {...props} />;
const SetBlueskyReplyTargetToolCall = (props: any) => <ThreadMessageToolActivityCall name="set_bluesky_reply_target" {...props} />;
const FallbackToolCall = (props: any) => <ThreadMessageToolActivityCall name={props.toolName ?? "tool"} {...props} />;

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
              set_bluesky_reply_target: SetBlueskyReplyTargetToolCall as never,
            },
            Fallback: FallbackToolCall as never,
          },
        }}
      />
    </MessagePrimitive.Root>
  );
}
