import type { DraftDto } from "../../api/drafts";

type ReplyTargetCardProps = {
  draft: DraftDto;
};

export function ReplyTargetCard({ draft }: ReplyTargetCardProps) {
  const target = draft.blueskyReplyTarget;

  if (!target?.replyParentUrl) {
    return null;
  }

  const previewText = target.replyParentText?.trim() ?? "";
  const shortPreview = previewText.length > 220 ? `${previewText.slice(0, 220)}...` : previewText;

  return (
    <div className="mx-3 mt-3 rounded-xl border border-sky-200/80 bg-sky-50/70 p-3 text-zinc-900 shadow-sm dark:border-sky-900/70 dark:bg-sky-950/30 dark:text-zinc-100" data-testid="reply-target-card">
      <div className="flex items-start gap-3">
        <div className="h-10 w-10 shrink-0 overflow-hidden rounded-full bg-sky-200/70 dark:bg-sky-800/70">
          {target.replyParentAvatarUrl ? (
            <img
              src={target.replyParentAvatarUrl}
              alt="Reply target avatar"
              className="h-full w-full object-cover"
              referrerPolicy="no-referrer"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center text-xs font-semibold">@</div>
          )}
        </div>
        <div className="min-w-0 flex-1">
          <div className="text-[11px] font-mono uppercase tracking-[0.14em] text-sky-700 dark:text-sky-300">Reply Target</div>
          <div className="mt-1 text-sm font-semibold leading-tight">{target.replyParentAuthor || "Unknown author"}</div>
          {shortPreview ? (
            <p className="mt-1 line-clamp-3 text-sm leading-snug text-zinc-700 dark:text-zinc-200">{shortPreview}</p>
          ) : (
            <p className="mt-1 text-sm leading-snug text-zinc-500 dark:text-zinc-400">No preview text available.</p>
          )}
          <div className="mt-2">
            <a
              href={target.replyParentUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center rounded-md border border-sky-300/70 bg-white/80 px-2.5 py-1 text-xs font-semibold text-sky-800 transition hover:bg-white dark:border-sky-700 dark:bg-sky-950/40 dark:text-sky-100"
              data-testid="reply-target-open-link"
            >
              Open original post
            </a>
          </div>
        </div>
      </div>
    </div>
  );
}
