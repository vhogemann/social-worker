import React from "react";

interface TextPreviewProps {
  content: string;
}

export const TextPreview: React.FC<TextPreviewProps> = ({ content }) => {
  return (
    <pre className="p-4 rounded-xl bg-bg/40 border border-border/40 text-xs text-zinc-300 whitespace-pre-wrap break-words font-mono leading-relaxed h-full overflow-y-auto">
      {content}
    </pre>
  );
};
