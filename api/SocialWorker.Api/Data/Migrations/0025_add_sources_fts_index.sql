-- Add GIN index on Sources for full-text search across Title and Content
CREATE INDEX IF NOT EXISTS "IX_Sources_SearchVector" 
ON "Sources" USING GIN (to_tsvector('english', coalesce("Title", '') || ' ' || coalesce("Content", '')));
