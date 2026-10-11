CREATE TABLE IF NOT EXISTS "ReplyAgents" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "Name" character varying(255) NOT NULL,
    "Description" text NULL,
    "IsEnabled" boolean NOT NULL DEFAULT true,
    "PersonaPrompt" text NOT NULL,
    "MaxCommentAgeHours" integer NOT NULL DEFAULT 24,
    "MaxPostAgeDays" integer NOT NULL DEFAULT 7,
    "MinRankingScore" integer NOT NULL DEFAULT 7,
    "MaxRepliesPerCommentThread" integer NOT NULL DEFAULT 2,
    "MaxRepliesPerUser" integer NOT NULL DEFAULT 3,
    "MaxThreadDepth" integer NOT NULL DEFAULT 4,
    "AutoPublishReplies" boolean NOT NULL DEFAULT true,
    "CheckIntervalMinutes" integer NOT NULL DEFAULT 15,
    "LastPolledAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_ReplyAgents_Users" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ReplyAgents_UserId" ON "ReplyAgents" ("UserId");

ALTER TABLE "AutomationWorkflows"
    ADD COLUMN IF NOT EXISTS "ReplyAgentId" uuid NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_AutomationWorkflows_ReplyAgents'
    ) THEN
        ALTER TABLE "AutomationWorkflows"
            ADD CONSTRAINT "FK_AutomationWorkflows_ReplyAgents"
            FOREIGN KEY ("ReplyAgentId") REFERENCES "ReplyAgents" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflows_ReplyAgentId"
    ON "AutomationWorkflows" ("ReplyAgentId");

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'WorkflowReplyAgents') THEN
        INSERT INTO "ReplyAgents" (
            "Id",
            "UserId",
            "Name",
            "Description",
            "IsEnabled",
            "PersonaPrompt",
            "MaxCommentAgeHours",
            "MaxPostAgeDays",
            "MinRankingScore",
            "MaxRepliesPerCommentThread",
            "MaxRepliesPerUser",
            "MaxThreadDepth",
            "AutoPublishReplies",
            "CheckIntervalMinutes",
            "LastPolledAt",
            "CreatedAt",
            "UpdatedAt"
        )
        SELECT
            wra."Id",
            aw."UserId",
            aw."Title" || ' Reply Agent',
            'Migrated from workflow: ' || aw."Title",
            wra."IsEnabled",
            wra."PersonaPrompt",
            wra."MaxCommentAgeHours",
            wra."MaxPostAgeDays",
            wra."MinRankingScore",
            wra."MaxRepliesPerCommentThread",
            wra."MaxRepliesPerUser",
            wra."MaxThreadDepth",
            wra."AutoPublishReplies",
            wra."CheckIntervalMinutes",
            wra."LastPolledAt",
            wra."CreatedAt",
            wra."UpdatedAt"
        FROM "WorkflowReplyAgents" wra
        JOIN "AutomationWorkflows" aw ON wra."AutomationWorkflowId" = aw."Id"
        ON CONFLICT ("Id") DO NOTHING;

        UPDATE "AutomationWorkflows" aw
        SET "ReplyAgentId" = wra."Id"
        FROM "WorkflowReplyAgents" wra
        WHERE aw."Id" = wra."AutomationWorkflowId";
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'WorkflowCommentThreads' AND column_name = 'WorkflowReplyAgentId'
    ) THEN
        ALTER TABLE "WorkflowCommentThreads"
            DROP CONSTRAINT IF EXISTS "FK_WorkflowCommentThreads_ReplyAgents";
        ALTER TABLE "WorkflowCommentThreads"
            DROP CONSTRAINT IF EXISTS "UQ_WorkflowCommentThreads_Branch";
        
        ALTER TABLE "WorkflowCommentThreads"
            RENAME COLUMN "WorkflowReplyAgentId" TO "ReplyAgentId";

        ALTER TABLE "WorkflowCommentThreads"
            ADD CONSTRAINT "FK_WorkflowCommentThreads_ReplyAgents"
            FOREIGN KEY ("ReplyAgentId") REFERENCES "ReplyAgents" ("Id") ON DELETE CASCADE;

        ALTER TABLE "WorkflowCommentThreads"
            ADD CONSTRAINT "UQ_WorkflowCommentThreads_Branch"
            UNIQUE ("ReplyAgentId", "RemoteBranchKey");
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'WorkflowCommentAudits' AND column_name = 'WorkflowReplyAgentId'
    ) THEN
        ALTER TABLE "WorkflowCommentAudits"
            DROP CONSTRAINT IF EXISTS "FK_WorkflowCommentAudits_ReplyAgents";
        ALTER TABLE "WorkflowCommentAudits"
            DROP CONSTRAINT IF EXISTS "UQ_WorkflowCommentAudits_Comment";
        
        ALTER TABLE "WorkflowCommentAudits"
            RENAME COLUMN "WorkflowReplyAgentId" TO "ReplyAgentId";

        ALTER TABLE "WorkflowCommentAudits"
            ADD CONSTRAINT "FK_WorkflowCommentAudits_ReplyAgents"
            FOREIGN KEY ("ReplyAgentId") REFERENCES "ReplyAgents" ("Id") ON DELETE CASCADE;

        ALTER TABLE "WorkflowCommentAudits"
            ADD CONSTRAINT "UQ_WorkflowCommentAudits_Comment"
            UNIQUE ("ReplyAgentId", "RemoteCommentId");
    END IF;
END $$;

DROP TABLE IF EXISTS "WorkflowReplyAgents" CASCADE;

DROP INDEX IF EXISTS "IX_WorkflowCommentAudits_Agent_Created";
CREATE INDEX IF NOT EXISTS "IX_WorkflowCommentAudits_Agent_Created"
    ON "WorkflowCommentAudits" ("ReplyAgentId", "CreatedAt" DESC);

DROP INDEX IF EXISTS "IX_WorkflowCommentThreads_Agent_Post";
CREATE INDEX IF NOT EXISTS "IX_WorkflowCommentThreads_Agent_Post"
    ON "WorkflowCommentThreads" ("ReplyAgentId", "PostId");
