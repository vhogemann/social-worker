CREATE TABLE IF NOT EXISTS "WorkflowReplyAgents" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "AutomationWorkflowId" uuid NOT NULL UNIQUE,
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
    CONSTRAINT "FK_WorkflowReplyAgents_AutomationWorkflows" FOREIGN KEY ("AutomationWorkflowId")
        REFERENCES "AutomationWorkflows" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "WorkflowCommentThreads" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "WorkflowReplyAgentId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "RemoteThreadKey" character varying(500) NOT NULL,
    "RemoteBranchKey" character varying(500) NOT NULL,
    "Summary" text NULL,
    "SummarizedUpToRemoteId" character varying(500) NULL,
    "SummarizedTurnCount" integer NOT NULL DEFAULT 0,
    "AgentReplyCount" integer NOT NULL DEFAULT 0,
    "IsTerminal" boolean NOT NULL DEFAULT false,
    "TerminalReason" character varying(100) NULL,
    "LastActivityAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_WorkflowCommentThreads_ReplyAgents" FOREIGN KEY ("WorkflowReplyAgentId")
        REFERENCES "WorkflowReplyAgents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_WorkflowCommentThreads_Posts" FOREIGN KEY ("PostId")
        REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "UQ_WorkflowCommentThreads_Branch" UNIQUE ("WorkflowReplyAgentId", "RemoteBranchKey")
);

CREATE TABLE IF NOT EXISTS "WorkflowCommentAudits" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "WorkflowReplyAgentId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "Platform" character varying(50) NOT NULL DEFAULT 'Bluesky',
    "RemoteCommentId" character varying(500) NOT NULL,
    "RemoteParentId" character varying(500) NULL,
    "RemoteRootId" character varying(500) NULL,
    "RemoteAuthorHandle" character varying(255) NOT NULL,
    "RemoteAuthorId" character varying(255) NOT NULL,
    "CommentText" text NOT NULL,
    "ParsedLinksJson" text NULL,
    "ParsedImagesJson" text NULL,
    "CommentCreatedAt" timestamp with time zone NOT NULL,
    "RankingScore" integer NOT NULL DEFAULT 0,
    "RankingCategory" character varying(50) NOT NULL DEFAULT 'Unknown',
    "RankingDecision" character varying(50) NOT NULL,
    "RankingReasoning" text NULL,
    "GeneratedReplyText" text NULL,
    "RemoteReplyId" character varying(500) NULL,
    "PublishedPostId" uuid NULL,
    "ErrorMessage" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "FK_WorkflowCommentAudits_ReplyAgents" FOREIGN KEY ("WorkflowReplyAgentId")
        REFERENCES "WorkflowReplyAgents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_WorkflowCommentAudits_Posts" FOREIGN KEY ("PostId")
        REFERENCES "Posts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_WorkflowCommentAudits_PublishedPost" FOREIGN KEY ("PublishedPostId")
        REFERENCES "Posts" ("Id") ON DELETE SET NULL,
    CONSTRAINT "UQ_WorkflowCommentAudits_Comment" UNIQUE ("WorkflowReplyAgentId", "RemoteCommentId")
);

CREATE INDEX IF NOT EXISTS "IX_WorkflowCommentAudits_Agent_Created"
    ON "WorkflowCommentAudits" ("WorkflowReplyAgentId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_WorkflowCommentThreads_Agent_Post"
    ON "WorkflowCommentThreads" ("WorkflowReplyAgentId", "PostId");
