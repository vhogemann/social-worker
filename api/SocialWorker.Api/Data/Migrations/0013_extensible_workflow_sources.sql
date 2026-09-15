-- 1. Add ScheduleId to AutomationWorkflows
ALTER TABLE "AutomationWorkflows" ADD COLUMN IF NOT EXISTS "ScheduleId" uuid NULL;
ALTER TABLE "AutomationWorkflows" ADD CONSTRAINT "FK_AutomationWorkflows_Schedules_ScheduleId" 
    FOREIGN KEY ("ScheduleId") REFERENCES "Schedules" ("Id") ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflows_ScheduleId" ON "AutomationWorkflows" ("ScheduleId");

-- 2. Make AutomationWorkflowSources polymorphic with its own Id primary key
ALTER TABLE "AutomationWorkflowSources" DROP CONSTRAINT IF EXISTS "PK_AutomationWorkflowSources";
ALTER TABLE "AutomationWorkflowSources" ADD COLUMN IF NOT EXISTS "Id" uuid NOT NULL DEFAULT gen_random_uuid();
ALTER TABLE "AutomationWorkflowSources" ADD COLUMN IF NOT EXISTS "Kind" character varying(50) NOT NULL DEFAULT 'Feed';
ALTER TABLE "AutomationWorkflowSources" ADD COLUMN IF NOT EXISTS "SourceWorkflowId" uuid NULL;
ALTER TABLE "AutomationWorkflowSources" ADD COLUMN IF NOT EXISTS "ConfigurationJson" text NULL;
ALTER TABLE "AutomationWorkflowSources" ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW();
ALTER TABLE "AutomationWorkflowSources" ALTER COLUMN "FeedSubscriptionId" DROP NOT NULL;
ALTER TABLE "AutomationWorkflowSources" ADD CONSTRAINT "PK_AutomationWorkflowSources" PRIMARY KEY ("Id");
ALTER TABLE "AutomationWorkflowSources" ADD CONSTRAINT "FK_AutomationWorkflowSources_AutomationWorkflows_SourceWorkflowId" 
    FOREIGN KEY ("SourceWorkflowId") REFERENCES "AutomationWorkflows" ("Id") ON DELETE CASCADE;
CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflowSources_SourceWorkflowId" ON "AutomationWorkflowSources" ("SourceWorkflowId");
