CREATE TABLE IF NOT EXISTS "BackgroundTasks" (
    "Id" uuid NOT NULL,
    "TaskType" character varying(100) NOT NULL,
    "PayloadJson" text NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "Priority" integer NOT NULL DEFAULT 0,
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "MaxAttempts" integer NOT NULL DEFAULT 3,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LockedUntil" timestamp with time zone NULL,
    "LockedByWorker" character varying(150) NULL,
    "LastError" text NULL,
    "DeduplicationKey" character varying(300) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "CompletedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_BackgroundTasks" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_BackgroundTasks_Status_NextAttemptAt" ON "BackgroundTasks" ("Status", "NextAttemptAt");
CREATE INDEX IF NOT EXISTS "IX_BackgroundTasks_LockedUntil" ON "BackgroundTasks" ("LockedUntil");
CREATE INDEX IF NOT EXISTS "IX_BackgroundTasks_TaskType_CreatedAt" ON "BackgroundTasks" ("TaskType", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_BackgroundTasks_DeduplicationKey" ON "BackgroundTasks" ("DeduplicationKey");
