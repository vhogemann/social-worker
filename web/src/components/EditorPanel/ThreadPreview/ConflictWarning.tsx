import React from "react";

interface ConflictWarningProps {
  hasConflict: boolean;
}

export const ConflictWarning: React.FC<ConflictWarningProps> = ({ hasConflict }) => {
  if (!hasConflict) return null;

  return (
    <div className="mt-3 px-3 py-2 bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400 text-xs rounded-xl flex items-center gap-1.5 font-medium select-none">
      <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
      </svg>
      <span>Bluesky: only images OR one YouTube embed is allowed per post.</span>
    </div>
  );
};
