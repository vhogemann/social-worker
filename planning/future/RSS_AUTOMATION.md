# Future Idea: RSS/Atom Feed Ingestion & Automation

Implement first-level automation by subscribing to external RSS/Atom feeds, periodically polling them, and converting new items into Drafts with feed-specific agent instructions.

## Proposed Concept

### 1. Feed Subscription Model
We will introduce a `FeedSubscription` entity:
- `Id` (Guid)
- `Title` (string)
- `FeedUrl` (string)
- `InstructionPrompt` (string) — Feed-specific instructions passed to the agent to draft the thread (e.g., "Summarize this tech blog post, highlight the key metrics, and keep a professional tone").
- `AutoPublish` (bool) — If true, publish immediately when ready. If false, leave the draft in the inbox for manual review.
- `LastPolledAt` (DateTime)

### 2. Polling Engine (Hosted Service)
A background hosted service (`FeedPollingHostedService`) will:
- Run periodically (e.g., every 30 minutes).
- Fetch feed items since `LastPolledAt`.
- For each new item:
  - Create a new `Draft` with the feed item content as a `Source`.
  - Trigger the agent thread-generation process using the feed's `InstructionPrompt`.
  - If `AutoPublish` is enabled and generation succeeds, queue the draft for automatic publishing to Bluesky.

### 3. Database Schema
```sql
CREATE TABLE FeedSubscriptions (
    Id UUID PRIMARY KEY,
    Title VARCHAR(200) NOT NULL,
    FeedUrl VARCHAR(500) NOT NULL,
    InstructionPrompt TEXT NOT NULL,
    AutoPublish BOOLEAN NOT NULL DEFAULT FALSE,
    LastPolledAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);
```
