-- 1. Create AutomationWorkflows table
CREATE TABLE IF NOT EXISTS "AutomationWorkflows" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "InstructionPrompt" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AutomationWorkflows" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AutomationWorkflows_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflows_UserId" ON "AutomationWorkflows" ("UserId");

-- 2. Create AutomationWorkflowSources join table (Many-to-Many)
CREATE TABLE IF NOT EXISTS "AutomationWorkflowSources" (
    "AutomationWorkflowId" uuid NOT NULL,
    "FeedSubscriptionId" uuid NOT NULL,
    CONSTRAINT "PK_AutomationWorkflowSources" PRIMARY KEY ("AutomationWorkflowId", "FeedSubscriptionId"),
    CONSTRAINT "FK_AutomationWorkflowSources_AutomationWorkflows_AutomationWorkflowId" 
        FOREIGN KEY ("AutomationWorkflowId") REFERENCES "AutomationWorkflows" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AutomationWorkflowSources_FeedSubscriptions_FeedSubscriptionId" 
        FOREIGN KEY ("FeedSubscriptionId") REFERENCES "FeedSubscriptions" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflowSources_FeedSubscriptionId" ON "AutomationWorkflowSources" ("FeedSubscriptionId");

-- 3. Create AutomationWorkflowDestinations table
CREATE TABLE IF NOT EXISTS "AutomationWorkflowDestinations" (
    "Id" uuid NOT NULL,
    "AutomationWorkflowId" uuid NOT NULL,
    "Platform" character varying(50) NOT NULL,
    "AutoPublish" boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT "PK_AutomationWorkflowDestinations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AutomationWorkflowDestinations_AutomationWorkflows_AutomationWorkflowId" 
        FOREIGN KEY ("AutomationWorkflowId") REFERENCES "AutomationWorkflows" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflowDestinations_AutomationWorkflowId" ON "AutomationWorkflowDestinations" ("AutomationWorkflowId");

-- 4. Alter Drafts table: link provenance to workflow
ALTER TABLE "Drafts" ADD COLUMN IF NOT EXISTS "AutomationWorkflowId" uuid NULL;
ALTER TABLE "Drafts" ADD CONSTRAINT "FK_Drafts_AutomationWorkflows_AutomationWorkflowId" 
    FOREIGN KEY ("AutomationWorkflowId") REFERENCES "AutomationWorkflows" ("Id") ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS "IX_Drafts_AutomationWorkflowId" ON "Drafts" ("AutomationWorkflowId");

-- 5. Drop old queue items and recreate schema with AutomationWorkflowId
DROP TABLE IF EXISTS "FeedIngestionQueueItems";
CREATE TABLE "FeedIngestionQueueItems" (
    "Id" uuid NOT NULL,
    "FeedSubscriptionId" uuid NOT NULL,
    "AutomationWorkflowId" uuid NOT NULL,
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
    CONSTRAINT "FK_FeedIngestionQueueItems_FeedSubscriptions_FeedSubscriptionId" 
        FOREIGN KEY ("FeedSubscriptionId") REFERENCES "FeedSubscriptions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_FeedIngestionQueueItems_AutomationWorkflows_AutomationWorkflowId" 
        FOREIGN KEY ("AutomationWorkflowId") REFERENCES "AutomationWorkflows" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_FeedSubscriptionId" ON "FeedIngestionQueueItems" ("FeedSubscriptionId");
CREATE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_AutomationWorkflowId" ON "FeedIngestionQueueItems" ("AutomationWorkflowId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_Sub_Workflow_Link" 
    ON "FeedIngestionQueueItems" ("FeedSubscriptionId", "AutomationWorkflowId", "ItemLink");
CREATE INDEX IF NOT EXISTS "IX_FeedIngestionQueueItems_Status_NextAttemptAt" 
    ON "FeedIngestionQueueItems" ("Status", "NextAttemptAt");

-- 6. Simplify FeedSubscriptions by dropping decoupled fields
ALTER TABLE "FeedSubscriptions" DROP COLUMN IF EXISTS "InstructionPrompt";
ALTER TABLE "FeedSubscriptions" DROP COLUMN IF EXISTS "AutoPublish";
