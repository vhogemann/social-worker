import { useEffect, useRef, useState } from "react";
import {
  ThreadPrimitive,
  ComposerPrimitive,
  MessagePrimitive,
  useMessage,
  useThread,
} from "@assistant-ui/react";
import ReactMarkdown from "react-markdown";
import { useEditorStore } from "../../store/editorStore";
import { useDraftStore } from "../../store/draftStore";

function ReplaceEditorToolCall({
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

function ProposeStageTransitionToolCall({
  args,
}: {
  args: { platform?: string; stage?: string; reasoning?: string };
}) {
  const platform = args?.platform ?? "";
  const stage = args?.stage ?? "";
  const reasoning = args?.reasoning ?? "";
  const updateThreadStage = useDraftStore((s) => s.updateThreadStage);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const activeThread = activeDraft?.threads?.find((t) => t.platform === platform);
  const [approved, setApproved] = useState(false);

  const handleApprove = async () => {
    if (activeDraftId && activeThread && stage) {
      await updateThreadStage(activeDraftId, activeThread.id, stage);
      setApproved(true);
    }
  };

  const isCurrentStage = activeThread?.stage === stage;

  return (
    <div className="my-2 p-3 text-xs bg-bg rounded border border-border flex flex-col gap-2 max-w-sm">
      <div className="flex items-center justify-between">
        <span className="font-mono text-muted uppercase tracking-wider">Stage Proposal ({platform})</span>
        {approved || isCurrentStage ? (
          <span className="text-green-400 font-mono font-medium">✓ Approved</span>
        ) : (
          <span className="text-accent font-mono font-medium">Pending Approval</span>
        )}
      </div>
      <div>
        Proposing move to <strong className="text-accent font-mono">{stage}</strong>.
      </div>
      {reasoning && (
        <div className="text-muted italic font-sans pl-2 border-l border-border mt-1">
          "{reasoning}"
        </div>
      )}
      {!approved && !isCurrentStage && (
        <div className="flex items-center gap-2 mt-1.5 justify-end">
          <button
            onClick={handleApprove}
            className="bg-accent text-bg px-2.5 py-1 rounded font-mono font-medium hover:opacity-95 transition-opacity"
          >
            Approve
          </button>
        </div>
      )}
    </div>
  );
}

function TextPart({ text }: { text: string }) {
  return (
    <div className="prose prose-invert prose-sm max-w-none">
      <ReactMarkdown>{text}</ReactMarkdown>
    </div>
  );
}

function Message() {
  const role = useMessage((m) => m.role);
  return (
    <MessagePrimitive.Root className="px-4 py-3">
      <div className="mb-1 text-xs font-mono uppercase tracking-wider text-muted">
        {role}
      </div>
      <MessagePrimitive.Parts
        components={{
          Text: TextPart,
          tools: {
            by_name: {
              replace_editor_content: ReplaceEditorToolCall as never,
              propose_stage_transition: ProposeStageTransitionToolCall as never,
            },
          },
        }}
      />
    </MessagePrimitive.Root>
  );
}

function Composer() {
  const isRunning = useThread((state) => state.isRunning);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const isLocked = activeDraft?.status === "Sourcing" || activeDraft?.status === "Formatting";

  return (
    <ComposerPrimitive.Root className="relative flex items-end gap-2 px-4 py-3 border-t border-border">
      {isRunning && (
        <div className="absolute bottom-20 left-4 flex items-center gap-2 text-xs font-mono text-muted">
          <span className="inline-block w-3 h-3 border-2 border-muted border-t-accent rounded-full animate-spin-slow" />
          assistant is thinking...
        </div>
      )}
      <ComposerPrimitive.Input
        placeholder={isLocked ? "Draft is locked..." : "Ask the assistant..."}
        disabled={isLocked}
        className="flex-1 resize-none bg-bg border border-border rounded px-3 py-2 text-sm focus:outline-none focus:border-accent disabled:opacity-50"
        rows={2}
      />
      <ComposerPrimitive.Send
        disabled={isLocked}
        className="bg-accent text-bg px-4 py-2 rounded text-sm font-medium hover:opacity-90 disabled:opacity-40"
      >
        Send
      </ComposerPrimitive.Send>
    </ComposerPrimitive.Root>
  );
}

export function Thread() {
  const isRunning = useThread((state) => state.isRunning);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const prevRunningRef = useRef(false);

  useEffect(() => {
    if (prevRunningRef.current && !isRunning) {
      loadDrafts();
    }
    prevRunningRef.current = isRunning;
  }, [isRunning, loadDrafts]);

  return (
    <ThreadPrimitive.Root className="flex flex-col h-full bg-panel">
      <div className="px-4 py-2 border-b border-border text-xs font-mono text-muted uppercase tracking-wider">
        chat
      </div>
      <ThreadPrimitive.Viewport className="flex-1 overflow-y-auto divide-y divide-border">
        <ThreadPrimitive.Messages components={{ Message }} />
      </ThreadPrimitive.Viewport>
      <Composer />
    </ThreadPrimitive.Root>
  );
}