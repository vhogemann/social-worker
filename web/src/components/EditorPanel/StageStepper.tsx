import { useDraftStore } from "../../store/draftStore";

const STAGES = [
  { id: "Draft", label: "Draft" },
  { id: "Ready", label: "Ready" },
  { id: "Sent", label: "Sent" },
];

const PLATFORMS = ["Bluesky", "Twitter", "LinkedIn", "Facebook", "Instagram"];

export function StageStepper() {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activePlatform = useDraftStore((s) => s.activePlatform);
  const setActivePlatform = useDraftStore((s) => s.setActivePlatform);
  const createThreadVariant = useDraftStore((s) => s.createThreadVariant);
  const updateThreadStage = useDraftStore((s) => s.updateThreadStage);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  if (!activeDraft) return null;

  const activeThread = activeDraft.threads?.find((t) => t.platform === activePlatform);
  const currentStageIndex = activeThread
    ? STAGES.findIndex((s) => s.id === activeThread.stage)
    : -1;

  const handleStageClick = async (stageId: string) => {
    if (activeDraftId && activeThread) {
      await updateThreadStage(activeDraftId, activeThread.id, stageId);
    }
  };

  const handleActivatePlatform = async () => {
    if (activeDraftId) {
      await createThreadVariant(activeDraftId, activePlatform);
    }
  };

  return (
    <div className="w-full bg-panel border-b border-border px-4 py-3 shrink-0 flex flex-col gap-3">
      {/* Platform Selector Tabs */}
      <div className="flex border-b border-border/60 pb-1.5 gap-2 overflow-x-auto scrollbar-none">
        {PLATFORMS.map((platform) => {
          const hasVariant = activeDraft.threads?.some((t) => t.platform === platform);
          const isActive = activePlatform === platform;

          return (
            <button
              key={platform}
              onClick={() => setActivePlatform(platform)}
              className={`px-3 py-1 text-xs font-medium rounded-lg transition select-none flex items-center gap-1.5 shrink-0 ${
                isActive
                  ? "bg-zinc-100 dark:bg-zinc-800 text-zinc-900 dark:text-zinc-50 font-semibold"
                  : "text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-200"
              }`}
            >
              <span>{platform}</span>
              {hasVariant && (
                <span className="w-1.5 h-1.5 rounded-full bg-indigo-500 shrink-0" />
              )}
            </button>
          );
        })}
      </div>

      {/* Stepper display or Activation block */}
      {activeThread ? (
        <div className="flex items-center justify-between relative max-w-lg mx-auto w-full py-1">
          {/* Connecting line */}
          <div className="absolute top-1/2 left-0 right-0 h-[2px] bg-border -translate-y-1/2 z-0" />
          <div
            className="absolute top-1/2 left-0 h-[2px] bg-accent -translate-y-1/2 z-0 transition-all duration-300"
            style={{
              width: `${(currentStageIndex / (STAGES.length - 1)) * 100}%`,
            }}
          />

          {STAGES.map((stage, idx) => {
            const isCompleted = idx < currentStageIndex;
            const isActive = idx === currentStageIndex;

            return (
              <button
                key={stage.id}
                onClick={() => handleStageClick(stage.id)}
                className="flex flex-col items-center relative z-10 focus:outline-none group"
              >
                <div
                  className={`w-6 h-6 rounded-full flex items-center justify-between border-2 text-[10px] font-mono font-bold transition-all ${
                    isActive
                      ? "bg-bg border-accent text-accent shadow-[0_0_8px_rgba(var(--color-accent),0.4)] scale-110"
                      : isCompleted
                      ? "bg-accent border-accent text-bg"
                      : "bg-panel border-border text-muted group-hover:border-muted group-hover:text-foreground"
                  }`}
                >
                  <span className="w-full text-center">
                    {isCompleted ? "✓" : idx + 1}
                  </span>
                </div>
                <span
                  className={`text-[9px] font-mono mt-1 transition-colors whitespace-nowrap ${
                    isActive
                      ? "text-accent font-semibold"
                      : isCompleted
                      ? "text-foreground"
                      : "text-muted group-hover:text-foreground"
                  }`}
                >
                  {stage.label}
                </span>
              </button>
            );
          })}
        </div>
      ) : (
        <div className="flex items-center justify-between gap-4 p-2 bg-zinc-50 dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 rounded-xl max-w-lg mx-auto w-full transition select-none">
          <span className="text-[11px] text-zinc-500 pl-2">
            No thread active for {activePlatform}.
          </span>
          <button
            onClick={handleActivatePlatform}
            className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 active:bg-indigo-800 text-white text-[11px] font-semibold rounded-lg shadow-sm transition"
          >
            Activate variant
          </button>
        </div>
      )}
    </div>
  );
}
