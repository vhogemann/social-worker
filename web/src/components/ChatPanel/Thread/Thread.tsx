import { useEffect, useRef } from "react";
import { ThreadPrimitive, useThread } from "@assistant-ui/react";
import { useDraftStore } from "../../../store/draftStore";
import { ThreadComposer } from "./ThreadComposer";
import { ThreadMessage } from "./ThreadMessage";

export function Thread() {
  const isRunning = useThread((state) => state.isRunning);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const loadSources = useDraftStore((s) => s.loadSources);
  const prevRunningRef = useRef(false);

  useEffect(() => {
    if (prevRunningRef.current && !isRunning) {
      loadDrafts();
      if (activeDraftId) {
        loadSources(activeDraftId);
      }
    }
    prevRunningRef.current = isRunning;
  }, [isRunning, loadDrafts, activeDraftId, loadSources]);

  return (
    <ThreadPrimitive.Root className="flex flex-col h-full bg-panel" data-testid="chat-panel">
      <div className="px-4 py-2 border-b border-border text-xs font-mono text-muted uppercase tracking-wider">
        chat
      </div>
      <ThreadPrimitive.Viewport className="flex-1 overflow-y-auto divide-y divide-border">
        <ThreadPrimitive.Messages components={{ Message: ThreadMessage }} />
      </ThreadPrimitive.Viewport>
      <ThreadComposer />
    </ThreadPrimitive.Root>
  );
}