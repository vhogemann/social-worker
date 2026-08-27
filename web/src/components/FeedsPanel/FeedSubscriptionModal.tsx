import React, { useState, useEffect } from "react";
import { Modal } from "../common/Modal";
import { Input } from "../common/Input";
import { Button } from "../common/Button";
import { FeedDiscoveryForm } from "./FeedDiscoveryForm";
import { discoverFeed, type FeedSubscriptionDto } from "../../api/feeds";

export interface FeedSubscriptionFormData {
  title: string;
  feedUrl: string;
  websiteUrl?: string;
  includeFilters?: string;
  excludeFilters?: string;
}

export interface FeedSubscriptionModalProps {
  isOpen: boolean;
  editingFeed: FeedSubscriptionDto | null;
  onClose: () => void;
  onSave: (data: FeedSubscriptionFormData) => Promise<void>;
}

export const FeedSubscriptionModal: React.FC<FeedSubscriptionModalProps> = ({
  isOpen,
  editingFeed,
  onClose,
  onSave
}) => {
  const [inputUrl, setInputUrl] = useState("");
  const [title, setTitle] = useState("");
  const [feedUrl, setFeedUrl] = useState("");
  const [websiteUrl, setWebsiteUrl] = useState("");
  const [includeFilters, setIncludeFilters] = useState("");
  const [excludeFilters, setExcludeFilters] = useState("");
  const [discovering, setDiscovering] = useState(false);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (editingFeed) {
      setTitle(editingFeed.title);
      setFeedUrl(editingFeed.feedUrl);
      setWebsiteUrl(editingFeed.websiteUrl || "");
      setIncludeFilters(editingFeed.includeFilters || "");
      setExcludeFilters(editingFeed.excludeFilters || "");
      setInputUrl(editingFeed.websiteUrl || editingFeed.feedUrl);
    } else {
      setTitle("");
      setFeedUrl("");
      setWebsiteUrl("");
      setIncludeFilters("");
      setExcludeFilters("");
      setInputUrl("");
    }
    setDiscoveryError(null);
  }, [editingFeed, isOpen]);

  const handleDiscover = async () => {
    if (!inputUrl.trim()) return;
    setDiscovering(true);
    setDiscoveryError(null);
    try {
      const result = await discoverFeed(inputUrl);
      if (result.success) {
        setFeedUrl(result.feedUrl);
        setTitle(result.title || "Discovered Feed");
        setWebsiteUrl(result.websiteUrl || inputUrl);
      } else {
        setDiscoveryError(result.error || "Could not discover any feed at this URL.");
      }
    } catch (err: any) {
      setDiscoveryError(err?.message || "Feed discovery failed.");
    } finally {
      setDiscovering(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !feedUrl.trim()) return;

    setSaving(true);
    try {
      await onSave({
        title: title.trim(),
        feedUrl: feedUrl.trim(),
        websiteUrl: websiteUrl.trim() || undefined,
        includeFilters: includeFilters.trim() || undefined,
        excludeFilters: excludeFilters.trim() || undefined
      });
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingFeed ? "Edit Feed Subscription" : "Add Feed Subscription"}
      maxWidth="xl"
    >
      <div className="flex flex-col gap-4">
        {!editingFeed && (
          <FeedDiscoveryForm
            inputUrl={inputUrl}
            onChangeInputUrl={setInputUrl}
            onDiscover={handleDiscover}
            discovering={discovering}
            error={discoveryError}
          />
        )}

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <Input
            required
            label="Feed Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="e.g. .NET Blog RSS"
          />

          <Input
            required
            type="url"
            mono
            label="Feed URL"
            value={feedUrl}
            onChange={(e) => setFeedUrl(e.target.value)}
            placeholder="https://example.com/rss.xml"
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="Include Filters (comma-separated)"
              value={includeFilters}
              onChange={(e) => setIncludeFilters(e.target.value)}
              placeholder="e.g. dotnet, csharp, ai"
            />
            <Input
              label="Exclude Filters (comma-separated)"
              value={excludeFilters}
              onChange={(e) => setExcludeFilters(e.target.value)}
              placeholder="e.g. crypto, jobs, sponsored"
            />
          </div>

          <div className="flex justify-end gap-2 border-t border-border pt-4 mt-2">
            <Button variant="secondary" size="sm" onClick={onClose} disabled={saving}>
              Cancel
            </Button>
            <Button type="submit" variant="primary" size="sm" loading={saving}>
              {editingFeed ? "Save Changes" : "Create Subscription"}
            </Button>
          </div>
        </form>
      </div>
    </Modal>
  );
};
