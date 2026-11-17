import React, { useState, useEffect } from "react";
import { Modal } from "./Modal";
import { Button } from "./Button";
import { TagInput } from "./TagInput";
import { AlertBanner } from "./AlertBanner";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faTags, faCheck } from "@fortawesome/free-solid-svg-icons";

export interface TagEditorModalProps {
  isOpen: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  initialTags?: string[];
  onSave: (tags: string[]) => Promise<void>;
  showDefaultToggle?: boolean;
}

export const TagEditorModal: React.FC<TagEditorModalProps> = ({
  isOpen,
  onClose,
  title,
  description = "Add or remove tags to categorize this item. The 'default' tag identifies primary or preferential items for ad-hoc posting.",
  initialTags = [],
  onSave,
  showDefaultToggle = true
}) => {
  const [tags, setTags] = useState<string[]>(initialTags);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setTags(initialTags);
      setError(null);
      setIsSaving(false);
    }
  }, [isOpen, initialTags]);

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setIsSaving(true);
      setError(null);
      await onSave(tags);
      onClose();
    } catch (err: any) {
      setError(err?.message || "Failed to update tags.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} title={title}>
      <form onSubmit={handleSave} className="space-y-4">
        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {description && (
          <p className="text-xs text-muted font-sans leading-relaxed">
            {description}
          </p>
        )}

        <TagInput
          tags={tags}
          onChange={setTags}
          label="Tags"
          hint="Type a tag and press Enter or comma. Tags are converted to lowercase automatically."
          showDefaultToggle={showDefaultToggle}
          disabled={isSaving}
        />

        <div className="flex justify-end gap-2 pt-3 border-t border-border">
          <Button
            type="button"
            variant="ghost"
            onClick={onClose}
            disabled={isSaving}
          >
            Cancel
          </Button>
          <Button
            type="submit"
            variant="primary"
            loading={isSaving}
            icon={faCheck}
            data-testid="save-tags-submit-btn"
          >
            Save Tags
          </Button>
        </div>
      </form>
    </Modal>
  );
};
