import { useEffect, useRef } from 'react';

interface TextEditOverlayProps {
  initialText: string;
  x: number;
  y: number;
  width: number;
  height: number;
  fontSize: number;
  fontFamily: string;
  color: string;
  align: 'left' | 'center' | 'right';
  lineHeight: number;
  onSave: (text: string) => void;
  onCancel: () => void;
}

export function TextEditOverlay({
  initialText,
  x,
  y,
  width,
  height,
  fontSize,
  fontFamily,
  color,
  align,
  lineHeight,
  onSave,
  onCancel,
}: TextEditOverlayProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.focus();
      textareaRef.current.select();
    }
  }, []);

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Escape') {
      onCancel();
    } else if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
      onSave(textareaRef.current?.value || '');
    }
  };

  return (
    <div
      className="absolute z-50 pointer-events-auto"
      style={{
        left: `${x}px`,
        top: `${y}px`,
        width: `${Math.max(120, width)}px`,
        minHeight: `${Math.max(40, height)}px`,
      }}
    >
      <textarea
        ref={textareaRef}
        defaultValue={initialText}
        onBlur={(e) => onSave(e.target.value)}
        onKeyDown={handleKeyDown}
        className="w-full h-full bg-black/80 border border-accent rounded p-1.5 resize-none focus:outline-none shadow-xl"
        style={{
          fontSize: `${fontSize}px`,
          fontFamily,
          color,
          textAlign: align,
          lineHeight: `${lineHeight}`,
        }}
      />
      <div className="text-[10px] font-mono text-muted mt-1 bg-black/80 px-2 py-0.5 rounded border border-border inline-block">
        Press Esc to cancel, Ctrl+Enter or click outside to save
      </div>
    </div>
  );
}
