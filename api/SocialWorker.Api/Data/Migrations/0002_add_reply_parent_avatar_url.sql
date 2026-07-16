ALTER TABLE IF EXISTS "DraftBlueskyMetadata"
ADD COLUMN IF NOT EXISTS "ReplyParentAvatarUrl" character varying(2000);
