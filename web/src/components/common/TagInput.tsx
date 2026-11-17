import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faXmark, faStar } from "@fortawesome/free-solid-svg-icons";

export interface TagInputProps {
  tags: string[];
  onChange: (tags: string[]) => void;
  label?: string;
  placeholder?: string;
  hint?: string;
  disabled?: boolean;
  showDefaultToggle?: boolean;
  className?: string;
}

export const TagInput: React.FC<TagInputProps> = ({
  tags,
  onChange,
  label,
  placeholder = "Type a tag and press Enter or comma...",
  hint,
  disabled = false,
  showDefaultToggle = true,
  className = ""
}) => {
  const [inputValue, setInputValue] = useState("");

  const addTag = (rawTag: string) => {
    const trimmed = rawTag.trim().toLowerCase();
    if (!trimmed) return;
    if (!tags.includes(trimmed)) {
      onChange([...tags, trimmed]);
    }
    setInputValue("");
  };

  const removeTag = (tagToRemove: string) => {
    onChange(tags.filter((t) => t !== tagToRemove));
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" || e.key === ",") {
      e.preventDefault();
      addTag(inputValue);
    } else if (e.key === "Backspace" && inputValue === "" && tags.length > 0) {
      removeTag(tags[tags.length - 1]);
    }
  };

  const toggleDefaultTag = () => {
    if (tags.includes("default")) {
      removeTag("default");
    } else {
      onChange(["default", ...tags.filter((t) => t !== "default")]);
    }
  };

  return (
    <div className={`space-y-2 ${className}`}>
      {label && (
        <div className="flex items-center justify-between">
          <label className="block text-xs font-medium text-foreground font-sans">
            {label}
          </label>
          {showDefaultToggle && (
            <button
              type="button"
              disabled={disabled}
              onClick={toggleDefaultTag}
              data-testid="toggle-default-tag-btn"
              className={`inline-flex items-center gap-1 text-[11px] px-2 py-0.5 rounded border transition-colors font-mono ${
                tags.includes("default")
                  ? "border-amber-500/40 bg-amber-500/10 text-amber-400 font-semibold"
                  : "border-border bg-bg/40 text-muted hover:text-foreground hover:border-muted"
              }`}
            >
              <FontAwesomeIcon icon={faStar} className="w-2.5 h-2.5" />
              <span>{tags.includes("default") ? "Default Set" : "+ Set as Default"}</span>
            </button>
          )}
        </div>
      )}

      <div className="p-2 border border-border bg-bg rounded-lg focus-within:border-accent transition-colors">
        <div className="flex flex-wrap items-center gap-1.5 min-h-[28px]">
          {tags.map((tag) => {
            const isDefault = tag === "default";
            return (
              <span
                key={tag}
                data-testid={`tag-chip-${tag}`}
                className={`inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-md border font-mono ${
                  isDefault
                    ? "border-amber-500/40 bg-amber-500/15 text-amber-300 font-semibold"
                    : "border-border bg-bg/80 text-foreground"
                }`}
              >
                {isDefault && <FontAwesomeIcon icon={faStar} className="w-2.5 h-2.5 text-amber-400" />}
                <span>{tag}</span>
                {!disabled && (
                  <button
                    type="button"
                    onClick={() => removeTag(tag)}
                    data-testid={`remove-tag-${tag}`}
                    className="text-muted hover:text-foreground transition-colors ml-0.5 p-0.5"
                    aria-label={`Remove tag ${tag}`}
                  >
                    <FontAwesomeIcon icon={faXmark} className="w-2.5 h-2.5" />
                  </button>
                )}
              </span>
            );
          })}

          <div className="flex-1 flex items-center min-w-[120px]">
            <input
              type="text"
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              onBlur={() => {
                if (inputValue.trim()) {
                  addTag(inputValue);
                }
              }}
              disabled={disabled}
              placeholder={tags.length === 0 ? placeholder : "Add tag..."}
              className="w-full bg-transparent text-xs text-foreground placeholder:text-muted/60 focus:outline-none font-mono py-0.5 px-1"
              data-testid="tag-text-input"
            />
            {inputValue.trim() && (
              <button
                type="button"
                onClick={() => addTag(inputValue)}
                data-testid="add-tag-submit-btn"
                className="text-[10px] bg-accent text-bg font-mono font-medium px-1.5 py-0.5 rounded hover:opacity-90 ml-1 shrink-0"
              >
                <FontAwesomeIcon icon={faPlus} className="w-2 h-2 mr-0.5" />
                Add
              </button>
            )}
          </div>
        </div>
      </div>

      {hint && <p className="text-[11px] text-muted font-sans">{hint}</p>}
    </div>
  );
};
