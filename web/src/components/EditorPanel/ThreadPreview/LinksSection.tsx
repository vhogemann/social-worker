import React from "react";
import { useDraftStore } from "../../../store/draftStore";

interface LinksSectionProps {
  links: { text: string; url: string; raw: string }[];
}

export const LinksSection: React.FC<LinksSectionProps> = ({ links }) => {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);

  if (links.length !== 1) return null;

  const link = links[0];
  let domain = "";
  try {
    domain = new URL(link.url).hostname;
  } catch {
    domain = "link";
  }

  const source = activeDraft?.sources?.find(
    (s) => s.reference.toLowerCase() === link.url.toLowerCase()
  );

  const displayTitle = source?.title || link.text || "View Link";

  return (
    <a
      href={link.url}
      target="_blank"
      rel="noopener noreferrer"
      className="mt-3 block overflow-hidden rounded-xl border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900 hover:opacity-95 transition relative group/link"
    >
      <div className="p-3">
        <div className="text-xs text-zinc-500 font-mono">{domain}</div>
        <div className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 truncate mt-0.5">
          {displayTitle}
        </div>
      </div>
    </a>
  );
};
