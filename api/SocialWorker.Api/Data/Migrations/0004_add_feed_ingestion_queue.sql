CREATE TABLE IF NOT EXISTS "FeedIngestionQueueItems" (
    "Id" uuid NOT NULL,
    "FeedSubscriptionId" uuid NOT NULL,
    "ItemTitle" character varying(500) NOT NULL,
    "ItemLink" character varying(1000) NOT NULL,
    "ItemDescription" text NULL,
    "ItemPublishedAt" timestamp with time zone NULL,
    "Status" character varying(50) NOT NULL,
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "MaxAttempts" integer NOT NULL DEFAULT 3,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LastAttemptAt" timestamp with time zone NULL,
    "LastError" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_FeedIngestionQueueItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FeedIngestionQueueItems_FeedSubscriptions_FeedSubscriptionId" FOREIGN KEY ("FeedSubscriptionId") REFERENCES "FeedSubscriptions" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_FeedSubscriptionId" ON "FeedIngestionQueueItems" ("FeedSubscriptionId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_FeedSubscriptionId_ItemLink" ON "FeedIngestionQueueItems" ("FeedSubscriptionId", "ItemLink");
CREATE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_Status_NextAttemptAt" ON "FeedIngestionQueueItems" ("Status", "NextAttemptAt");
