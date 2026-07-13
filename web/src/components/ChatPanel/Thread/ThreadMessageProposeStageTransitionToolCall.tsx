import { useState } from "react";
import { useDraftStore } from "../../../store/draftStore";

export function ThreadMessageProposeStageTransitionToolCall({
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
