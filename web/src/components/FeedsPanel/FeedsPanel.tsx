import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faArrowLeft,
  faClock,
  faPlus,
  faRss,
  faSpinner
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchFeeds,
  fetchFeedQueue,
  createFeed,
  updateFeed,
  deleteFeed,
  triggerFeed,
  retryFeedQueueItem,
  deleteFeedQueueItem,
  type FeedQueueItemDto,
  type FeedSubscriptionDto
} from "../../api/feeds";
import { Button } from "../common/Button";
import { EmptyState } from "../common/EmptyState";
import { AlertBanner } from "../common/AlertBanner";
import { ConfirmModal } from "../common/ConfirmModal";
import { FeedSubscriptionCard } from "./FeedSubscriptionCard";
import { FeedSubscriptionModal, type FeedSubscriptionFormData } from "./FeedSubscriptionModal";
import { FeedQueueBacklog } from "./FeedQueueBacklog";

export const FeedsPanel: React.FC = () => {
  const [feeds, setFeeds] = useState<FeedSubscriptionDto[]>([]);
  const [queueItems, setQueueItems] = useState<FeedQueueItemDto[]>([]);
  const [isBacklogOpen, setIsBacklogOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [queueLoading, setQueueLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [queueError, setQueueError] = useState<string | null>(null);

  // Modal form & delete confirmation state
  const [isSubscriptionModalOpen, setIsSubscriptionModalOpen] = useState(false);
  const [editingFeed, setEditingFeed] = useState<FeedSubscriptionDto | null>(null);
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [queueActionId, setQueueActionId] = useState<string | null>(null);
  const [deletingFeedId, setDeletingFeedId] = useState<string | null>(null);
  const [deletingQueueItemId, setDeletingQueueItemId] = useState<string | null>(null);

  const loadAllFeeds = async () => {
    setLoading(true);
    setQueueLoading(true);
    setError(null);
    setQueueError(null);
    try {
      const [feedData, queueData] = await Promise.all([fetchFeeds(), fetchFeedQueue()]);
      setFeeds(feedData);
      setQueueItems(queueData);
    } catch (err: any) {
      setError(err?.message || "Failed to load feed subscriptions.");
      setQueueError(err?.message || "Failed to load processing backlog.");
    } finally {
      setLoading(false);
      setQueueLoading(false);
    }
  };

  useEffect(() => {
    void loadAllFeeds();
  }, []);

  const handleSaveSubscription = async (formData: FeedSubscriptionFormData) => {
    setError(null);
    try {
      if (editingFeed) {
        await updateFeed(editingFeed.id, formData);
      } else {
        await createFeed(formData);
      }
      setIsSubscriptionModalOpen(false);
      setEditingFeed(null);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to save feed subscription.");
      throw err;
    }
  };

  const handleEdit = (feed: FeedSubscriptionDto) => {
    setEditingFeed(feed);
    setIsSubscriptionModalOpen(true);
  };

  const handleConfirmDeleteFeed = async () => {
    if (!deletingFeedId) return;
    try {
      await deleteFeed(deletingFeedId);
      setDeletingFeedId(null);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to delete subscription.");
    }
  };

  const handleTrigger = async (id: string) => {
    setTriggeringId(id);
    setError(null);
    try {
      await triggerFeed(id);
      await loadAllFeeds();
    } catch (err: any) {
      setError(err?.message || "Failed to trigger feed polling.");
    } finally {
      setTriggeringId(null);
    }
  };

  const handleRetryQueueItem = async (id: string) => {
    setQueueActionId(id);
    setQueueError(null);
    try {
      await retryFeedQueueItem(id);
      await loadAllFeeds();
    } catch (err: any) {
      setQueueError(err?.message || "Failed to retry queue item.");
    } finally {
      setQueueActionId(null);
    }
  };

  const handleConfirmDeleteQueueItem = async () => {
    if (!deletingQueueItemId) return;
    setQueueActionId(deletingQueueItemId);
    setQueueError(null);
    try {
      await deleteFeedQueueItem(deletingQueueItemId);
      setDeletingQueueItemId(null);
      await loadAllFeeds();
    } catch (err: any) {
      setQueueError(err?.message || "Failed to delete queue item.");
    } finally {
      setQueueActionId(null);
    }
  };

  return (
    <div className="flex flex-col h-full bg-bg overflow-y-auto">
      <div className="max-w-6xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* Top Header */}
        <div className="flex justify-between items-center border-b border-border pb-4">
          <div className="flex items-center gap-4">
            <Link to="/" className="text-muted hover:text-foreground text-xs flex items-center gap-1.5 transition">
              <FontAwesomeIcon icon={faArrowLeft} />
              <span>Back</span>
            </Link>
            <h1 className="text-lg font-bold text-foreground">Feed Automation</h1>
          </div>
          <div className="flex gap-2">
            <Button
              variant={isBacklogOpen ? "secondary" : "outline"}
              size="sm"
              icon={faClock}
              onClick={() => setIsBacklogOpen(!isBacklogOpen)}
              className={isBacklogOpen ? "bg-panel border-accent text-accent" : ""}
            >
              Processing Backlog ({queueItems.length})
            </Button>
            <Button
              variant="primary"
              size="sm"
              icon={faPlus}
              onClick={() => {
                setEditingFeed(null);
                setIsSubscriptionModalOpen(true);
              }}
            >
              New Feed
            </Button>
          </div>
        </div>

        {error && <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} />}

        {/* Backlog View */}
        {isBacklogOpen && (
          <FeedQueueBacklog
            items={queueItems}
            isLoading={queueLoading}
            error={queueError}
            actionId={queueActionId}
            onRefresh={loadAllFeeds}
            onRetry={handleRetryQueueItem}
            onDelete={(id) => setDeletingQueueItemId(id)}
          />
        )}

        {/* Feed Subscriptions Grid */}
        {loading && feeds.length === 0 ? (
          <div className="flex justify-center items-center py-20 text-muted">
            <FontAwesomeIcon icon={faSpinner} spin className="text-xl" />
          </div>
        ) : feeds.length === 0 ? (
          <EmptyState
            icon={faRss}
            title="No Feed Subscriptions"
            description="Add your first RSS, Atom, or YouTube feed subscription to start automating content drafting."
            action={
              <Button
                variant="primary"
                size="sm"
                icon={faPlus}
                onClick={() => {
                  setEditingFeed(null);
                  setIsSubscriptionModalOpen(true);
                }}
              >
                Add Subscription
              </Button>
            }
          />
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {feeds.map((feed) => (
              <FeedSubscriptionCard
                key={feed.id}
                feed={feed}
                isTriggering={triggeringId === feed.id}
                onTrigger={handleTrigger}
                onEdit={handleEdit}
                onDelete={(id) => setDeletingFeedId(id)}
              />
            ))}
          </div>
        )}

        {/* Subscription Create / Edit Modal */}
        <FeedSubscriptionModal
          isOpen={isSubscriptionModalOpen}
          editingFeed={editingFeed}
          onClose={() => {
            setIsSubscriptionModalOpen(false);
            setEditingFeed(null);
          }}
          onSave={handleSaveSubscription}
        />

        {/* Deletion Confirmations */}
        <ConfirmModal
          isOpen={Boolean(deletingFeedId)}
          title="Delete Subscription"
          message="Are you sure you want to delete this feed subscription? This will stop automated polling."
          confirmLabel="Delete"
          onConfirm={handleConfirmDeleteFeed}
          onCancel={() => setDeletingFeedId(null)}
        />

        <ConfirmModal
          isOpen={Boolean(deletingQueueItemId)}
          title="Delete Queue Item"
          message="Are you sure you want to remove this item from the processing queue?"
          confirmLabel="Delete"
          onConfirm={handleConfirmDeleteQueueItem}
          onCancel={() => setDeletingQueueItemId(null)}
        />
      </div>
    </div>
  );
};
