import { useEffect, useRef } from "react";
import { ThreadPrimitive, useThread } from "@assistant-ui/react";
import { useComposerDraft } from "../../Composer/ComposerDraftContext";
import { ThreadComposer } from "./ThreadComposer";
import { ThreadActivityCards } from "./ThreadActivityCards";
import { ThreadMessage } from "./ThreadMessage";

export function Thread() {
  const isRunning = useThread((state) => state.isRunning);
  const { activityCards, loadSources, refetchDraft } = useComposerDraft();
  const prevRunningRef = useRef(false);

  useEffect(() => {
    if (prevRunningRef.current && !isRunning) {
      void refetchDraft();
      void loadSources();
    }
    prevRunningRef.current = isRunning;
  }, [isRunning, refetchDraft, loadSources]);

  return (
    <ThreadPrimitive.Root className="flex flex-col h-full bg-panel" data-testid="chat-panel">
      <div className="px-4 py-2 border-b border-border text-xs font-mono text-muted uppercase tracking-wider">
        chat
      </div>
      <ThreadPrimitive.Viewport className="flex-1 overflow-y-auto divide-y divide-border">
        <ThreadActivityCards cards={activityCards} />
        <ThreadPrimitive.Messages components={{ Message: ThreadMessage }} />
      </ThreadPrimitive.Viewport>
      <ThreadComposer />
    </ThreadPrimitive.Root>
  );
}