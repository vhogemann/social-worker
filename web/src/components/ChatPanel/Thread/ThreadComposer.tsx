import { useEffect, useMemo, useRef, useState } from "react";
import {
  ComposerPrimitive,
  useComposer,
  useComposerRuntime,
  useThread,
} from "@assistant-ui/react";
import { useDraftStore } from "../../../store/draftStore";
import { slashAdapter } from "./ThreadSlashCommands";
import { navigatePromptHistory } from "./ThreadComposerHistory";

type SlashItem = {
  id: string;
  label: string;
  description?: string;
  metadata?: { command?: string };
};

type ThreadMessageView = {
  role?: string;
  content?: ReadonlyArray<{
    type?: string;
    text?: string;
  }>;
};

function getSlashQuery(text: string): string | null {
  if (!text.startsWith("/")) {
    return null;
  }

  return text.slice(1).trim();
}

export function ThreadComposer() {
  const isRunning = useThread((state) => state.isRunning);
  const messages = useThread((state) => state.messages as ReadonlyArray<ThreadMessageView>);
  const composerText = useComposer((state) => state.text);
  const composerRuntime = useComposerRuntime();
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const isLocked = activeDraft?.status === "Sourcing" || activeDraft?.status === "Formatting";
  const draftHistoryKey = activeDraftId ?? "__no_draft__";

  const [activeIndex, setActiveIndex] = useState(0);
  const [dismissedForText, setDismissedForText] = useState<string | null>(null);
  const [promptHistoryByDraft, setPromptHistoryByDraft] = useState<Record<string, string[]>>({});
  const [historyIndexByDraft, setHistoryIndexByDraft] = useState<Record<string, number | null>>({});
  const [draftBufferByDraft, setDraftBufferByDraft] = useState<Record<string, string>>({});
  const slashItemRefs = useRef<Array<HTMLButtonElement | null>>([]);
  const lastCapturedUserTextRef = useRef<Record<string, string>>({});

  const promptHistory = promptHistoryByDraft[draftHistoryKey] ?? [];
  const historyIndex = historyIndexByDraft[draftHistoryKey] ?? null;
  const draftBuffer = draftBufferByDraft[draftHistoryKey] ?? "";

  const slashQuery = useMemo(() => getSlashQuery(composerText), [composerText]);
  const slashItems = useMemo<SlashItem[]>(() => {
    if (slashQuery === null) {
      return [];
    }

    return slashAdapter.search(slashQuery) as SlashItem[];
  }, [slashQuery]);

  const showSlashPopover =
    !isLocked &&
    slashQuery !== null &&
    slashItems.length > 0 &&
    dismissedForText !== composerText;

  useEffect(() => {
    setActiveIndex(0);
  }, [slashQuery]);

  useEffect(() => {
    if (!showSlashPopover) {
      return;
    }

    slashItemRefs.current[activeIndex]?.scrollIntoView({ block: "nearest" });
  }, [activeIndex, showSlashPopover]);

  useEffect(() => {
    for (let i = messages.length - 1; i >= 0; i--) {
      const message = messages[i];
      if ((message.role ?? "").toLowerCase() !== "user") {
        continue;
      }

      const text = (message.content ?? [])
        .filter((part) => part.type === "text")
        .map((part) => part.text ?? "")
        .join("\n")
        .trim();

      if (!text || text === lastCapturedUserTextRef.current[draftHistoryKey]) {
        return;
      }

      lastCapturedUserTextRef.current[draftHistoryKey] = text;
      setPromptHistoryByDraft((current) => {
        const draftHistory = current[draftHistoryKey] ?? [];
        if (draftHistory[draftHistory.length - 1] === text) {
          return current;
        }

        return {
          ...current,
          [draftHistoryKey]: [...draftHistory, text],
        };
      });
      setHistoryIndexByDraft((current) => ({ ...current, [draftHistoryKey]: null }));
      return;
    }
  }, [messages, draftHistoryKey]);

  const selectSlashItem = (item: SlashItem) => {
    const command = typeof item.metadata?.command === "string" ? item.metadata.command : item.label;
    composerRuntime.setText(command);
    setDismissedForText(command);
  };

  return (
    <ComposerPrimitive.Root className="relative flex items-end gap-2 px-4 py-3 border-t border-border">
      {isRunning && (
        <div className="absolute bottom-20 left-4 flex items-center gap-2 text-xs font-mono text-muted">
          <span className="inline-block w-3 h-3 border-2 border-muted border-t-accent rounded-full animate-spin-slow" />
          assistant is thinking...
        </div>
      )}

      {showSlashPopover && (
        <div className="absolute bottom-24 left-4 z-20 w-[26rem] max-w-[calc(100%-2rem)] rounded-lg border border-border bg-panel shadow-xl overflow-hidden">
          <div className="px-3 py-2 text-[11px] font-mono uppercase tracking-wider text-muted border-b border-border">
            slash commands
          </div>
          <div className="p-2 max-h-72 overflow-y-auto">
            {slashItems.map((item, index) => (
              <button
                key={item.id}
                type="button"
                onClick={() => selectSlashItem(item)}
                ref={(el) => {
                  slashItemRefs.current[index] = el;
                }}
                className={`w-full text-left px-2 py-1.5 rounded ${
                  index === activeIndex ? "bg-bg" : "hover:bg-bg"
                }`}
              >
                <div className="text-sm text-foreground">{item.label}</div>
                {item.description ? (
                  <div className="text-xs text-muted">{item.description}</div>
                ) : null}
              </button>
            ))}
          </div>
        </div>
      )}

      <ComposerPrimitive.Input
        placeholder={isLocked ? "Draft is locked..." : "Ask the assistant... (type / for commands)"}
        disabled={isLocked}
        className="flex-1 resize-none bg-bg border border-border rounded px-3 py-2 text-sm focus:outline-none focus:border-accent disabled:opacity-50"
        rows={2}
        onKeyDown={(e) => {
          const target = e.currentTarget;

          if (!showSlashPopover) {
            const hasSelection = target.selectionStart !== target.selectionEnd;
            const selectionStart = target.selectionStart ?? 0;
            const selectionEnd = target.selectionEnd ?? 0;
            const isAtFirstLine = !target.value.slice(0, selectionStart).includes("\n");
            const isAtLastLine = !target.value.slice(selectionEnd).includes("\n");

            if (!hasSelection && (e.key === "ArrowUp" || e.key === "ArrowDown") && !e.altKey && !e.metaKey && !e.ctrlKey && !e.shiftKey) {
              if (e.key === "ArrowUp" && isAtFirstLine) {
                const result = navigatePromptHistory("up", {
                  history: promptHistory,
                  historyIndex,
                  draftBuffer,
                  composerText,
                });

                if (result.consumed) {
                  e.preventDefault();
                  composerRuntime.setText(result.nextText);
                  setHistoryIndexByDraft((current) => ({ ...current, [draftHistoryKey]: result.nextHistoryIndex }));
                  setDraftBufferByDraft((current) => ({ ...current, [draftHistoryKey]: result.nextDraftBuffer }));
                }
              }

              if (e.key === "ArrowDown" && isAtLastLine) {
                const result = navigatePromptHistory("down", {
                  history: promptHistory,
                  historyIndex,
                  draftBuffer,
                  composerText,
                });

                if (result.consumed) {
                  e.preventDefault();
                  composerRuntime.setText(result.nextText);
                  setHistoryIndexByDraft((current) => ({ ...current, [draftHistoryKey]: result.nextHistoryIndex }));
                  setDraftBufferByDraft((current) => ({ ...current, [draftHistoryKey]: result.nextDraftBuffer }));
                }
              }
            }

            return;
          }

          if (e.key === "Escape") {
            e.preventDefault();
            setDismissedForText(composerText);
            return;
          }

          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActiveIndex((prev) => (prev + 1) % slashItems.length);
            return;
          }

          if (e.key === "ArrowUp") {
            e.preventDefault();
            setActiveIndex((prev) => (prev - 1 + slashItems.length) % slashItems.length);
            return;
          }

          if ((e.key === "Enter" || e.key === "Tab") && slashItems[activeIndex]) {
            e.preventDefault();
            selectSlashItem(slashItems[activeIndex]);
          }
        }}
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
