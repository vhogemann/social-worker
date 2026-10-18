-- Add RemoteLikeId column to WorkflowCommentAudits to track likes performed by reply agents
ALTER TABLE "WorkflowCommentAudits"
ADD COLUMN IF NOT EXISTS "RemoteLikeId" character varying(500);
