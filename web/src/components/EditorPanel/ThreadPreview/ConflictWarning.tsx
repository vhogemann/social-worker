import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faTriangleExclamation } from "@fortawesome/free-solid-svg-icons";

interface ConflictWarningProps {
  hasConflict: boolean;
}

export const ConflictWarning: React.FC<ConflictWarningProps> = ({ hasConflict }) => {
  if (!hasConflict) return null;

  return (
    <div className="mt-3 px-3 py-2 bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400 text-xs rounded-xl flex items-center gap-1.5 font-medium select-none">
      <FontAwesomeIcon icon={faTriangleExclamation} className="w-4 h-4 shrink-0" />
      <span>Bluesky: only images OR one YouTube embed is allowed per post.</span>
    </div>
  );
};
