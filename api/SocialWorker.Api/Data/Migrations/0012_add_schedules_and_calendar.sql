-- 1. Create Schedules table
CREATE TABLE IF NOT EXISTS "Schedules" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Timezone" character varying(100) NOT NULL DEFAULT 'UTC',
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Schedules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Schedules_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Schedules_UserId" ON "Schedules" ("UserId");

-- 2. Create ScheduleSlotRules table
CREATE TABLE IF NOT EXISTS "ScheduleSlotRules" (
    "Id" uuid NOT NULL,
    "ScheduleId" uuid NOT NULL,
    "RecurrenceType" character varying(50) NOT NULL,
    "CronExpression" character varying(100) NOT NULL,
    "StartTime" time without time zone NOT NULL,
    "EndTime" time without time zone NOT NULL,
    "PostCapacity" integer NOT NULL DEFAULT 1,
    "DaysOfWeek" character varying(50) NULL,
    "DayOfMonth" integer NULL,
    "MonthOfYear" integer NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ScheduleSlotRules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleSlotRules_Schedules_ScheduleId" FOREIGN KEY ("ScheduleId") REFERENCES "Schedules" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ScheduleSlotRules_ScheduleId" ON "ScheduleSlotRules" ("ScheduleId");

-- 3. Create ScheduleQueueItems table
CREATE TABLE IF NOT EXISTS "ScheduleQueueItems" (
    "Id" uuid NOT NULL,
    "ScheduleId" uuid NOT NULL,
    "DraftId" uuid NOT NULL,
    "PlatformThreadId" uuid NOT NULL,
    "Position" integer NOT NULL DEFAULT 0,
    "Status" character varying(50) NOT NULL DEFAULT 'Queued',
    "AssignedSlotUtc" timestamp with time zone NULL,
    "ScheduledTaskId" uuid NULL,
    "LastError" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ScheduleQueueItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ScheduleQueueItems_Schedules_ScheduleId" FOREIGN KEY ("ScheduleId") REFERENCES "Schedules" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ScheduleQueueItems_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ScheduleQueueItems_PlatformThreads_PlatformThreadId" FOREIGN KEY ("PlatformThreadId") REFERENCES "PlatformThreads" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_ScheduleId_Position" ON "ScheduleQueueItems" ("ScheduleId", "Position");
CREATE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_Status_AssignedSlotUtc" ON "ScheduleQueueItems" ("Status", "AssignedSlotUtc");
CREATE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_DraftId" ON "ScheduleQueueItems" ("DraftId");
CREATE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_PlatformThreadId" ON "ScheduleQueueItems" ("PlatformThreadId");

-- 4. Alter AutomationWorkflowDestinations table
ALTER TABLE "AutomationWorkflowDestinations" ADD COLUMN IF NOT EXISTS "PublishingMode" integer NOT NULL DEFAULT 0;
ALTER TABLE "AutomationWorkflowDestinations" ADD COLUMN IF NOT EXISTS "ScheduleId" uuid NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_AutomationWorkflowDestinations_Schedules_ScheduleId'
    ) THEN
        ALTER TABLE "AutomationWorkflowDestinations"
            ADD CONSTRAINT "FK_AutomationWorkflowDestinations_Schedules_ScheduleId"
            FOREIGN KEY ("ScheduleId") REFERENCES "Schedules" ("Id") ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflowDestinations_ScheduleId" ON "AutomationWorkflowDestinations" ("ScheduleId");

-- 5. Data migration: populate PublishingMode from existing AutoPublish boolean
UPDATE "AutomationWorkflowDestinations" SET "PublishingMode" = 1 WHERE "AutoPublish" = TRUE;
