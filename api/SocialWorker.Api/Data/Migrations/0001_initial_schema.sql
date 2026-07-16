CREATE TABLE IF NOT EXISTS "LlmProviders" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "ProviderType" character varying(50) NOT NULL,
    "BaseUrl" character varying(500) NOT NULL,
    "ApiKey" character varying(500) NOT NULL,
    "Model" character varying(200) NOT NULL,
    "ContextWindowTokens" integer NULL,
    "IsDefault" boolean NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_LlmProviders" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_LlmProviders_Name" ON "LlmProviders" ("Name");

CREATE TABLE IF NOT EXISTS "Users" (
    "Id" uuid NOT NULL,
    "Username" character varying(100) NOT NULL,
    "Email" character varying(150) NOT NULL,
    "PasswordHash" text NOT NULL,
    "Role" character varying(50) NOT NULL,
    "IsActive" boolean NOT NULL,
    "PreferredProviderId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Users_LlmProviders_PreferredProviderId" FOREIGN KEY ("PreferredProviderId") REFERENCES "LlmProviders" ("Id") ON DELETE SET NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");
CREATE INDEX IF NOT EXISTS "IX_Users_PreferredProviderId" ON "Users" ("PreferredProviderId");

CREATE TABLE IF NOT EXISTS "Drafts" (
    "Id" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Status" character varying(50) NOT NULL,
    "Content" text NULL,
    "UserId" uuid NOT NULL,
    "TargetPlatform" character varying(50) NULL,
    "CanonicalDraftId" uuid NULL,
    "ChatHistory" text NULL,
    "ChatSummary" text NULL,
    "LastSummarizedMessageCount" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Drafts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Drafts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Drafts_Drafts_CanonicalDraftId" FOREIGN KEY ("CanonicalDraftId") REFERENCES "Drafts" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_Drafts_UserId" ON "Drafts" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Drafts_CanonicalDraftId" ON "Drafts" ("CanonicalDraftId");

CREATE TABLE IF NOT EXISTS "ThreadSegments" (
    "Id" uuid NOT NULL,
    "DraftId" uuid NOT NULL,
    "Position" integer NOT NULL,
    "Content" text NOT NULL,
    CONSTRAINT "PK_ThreadSegments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ThreadSegments_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ThreadSegments_DraftId_Position" ON "ThreadSegments" ("DraftId", "Position");

CREATE TABLE IF NOT EXISTS "PlatformThreads" (
    "Id" uuid NOT NULL,
    "DraftId" uuid NOT NULL,
    "Platform" character varying(100) NOT NULL,
    "Stage" character varying(50) NOT NULL,
    "Content" text NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_PlatformThreads" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PlatformThreads_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PlatformThreads_DraftId_Platform" ON "PlatformThreads" ("DraftId", "Platform");

CREATE TABLE IF NOT EXISTS "MediaAssets" (
    "Id" uuid NOT NULL,
    "DraftId" uuid NOT NULL,
    "FileName" character varying(255) NOT NULL,
    "MimeType" character varying(100) NOT NULL,
    "AltText" character varying(1000) NULL,
    "FilePath" character varying(500) NOT NULL,
    "Sha256" character varying(64) NOT NULL,
    "SizeBytes" bigint NOT NULL,
    "Width" integer NOT NULL,
    "Height" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_MediaAssets" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MediaAssets_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_MediaAssets_DraftId" ON "MediaAssets" ("DraftId");

CREATE TABLE IF NOT EXISTS "Sources" (
    "Id" uuid NOT NULL,
    "Kind" character varying(50) NOT NULL,
    "Reference" character varying(500) NOT NULL,
    "Content" text NULL,
    "Title" character varying(200) NULL,
    "Summary" text NULL,
    "TranscriptStatus" character varying(50) NOT NULL,
    "TranscriptPath" character varying(500) NULL,
    "YoutubeVideoId" character varying(11) NULL,
    "Sha256" character varying(64) NULL,
    "AddedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Sources" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS "DraftSources" (
    "DraftId" uuid NOT NULL,
    "SourceId" uuid NOT NULL,
    "LinkedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_DraftSources" PRIMARY KEY ("DraftId", "SourceId"),
    CONSTRAINT "FK_DraftSources_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DraftSources_Sources_SourceId" FOREIGN KEY ("SourceId") REFERENCES "Sources" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_DraftSources_DraftId_SourceId" ON "DraftSources" ("DraftId", "SourceId");
CREATE INDEX IF NOT EXISTS "IX_DraftSources_SourceId" ON "DraftSources" ("SourceId");

CREATE TABLE IF NOT EXISTS "Accounts" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Platform" character varying(100) NOT NULL,
    "Handle" character varying(255) NOT NULL,
    "CredentialsEncrypted" text NOT NULL,
    "Status" character varying(50) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Accounts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_UserId_Platform" ON "Accounts" ("UserId", "Platform");

CREATE TABLE IF NOT EXISTS "Posts" (
    "Id" uuid NOT NULL,
    "DraftId" uuid NOT NULL,
    "PlatformThreadId" uuid NOT NULL,
    "SegmentIndex" integer NOT NULL,
    "Platform" character varying(100) NOT NULL,
    "RemoteId" character varying(255) NULL,
    "Url" character varying(1000) NULL,
    "Error" text NULL,
    "PostedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Posts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Posts_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Posts_PlatformThreads_PlatformThreadId" FOREIGN KEY ("PlatformThreadId") REFERENCES "PlatformThreads" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_Posts_DraftId" ON "Posts" ("DraftId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Posts_PlatformThreadId_SegmentIndex" ON "Posts" ("PlatformThreadId", "SegmentIndex");

CREATE TABLE IF NOT EXISTS "BrandVoicePrompts" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(150) NOT NULL,
    "Body" text NOT NULL,
    "IsDefault" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_BrandVoicePrompts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_BrandVoicePrompts_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_BrandVoicePrompts_UserId" ON "BrandVoicePrompts" ("UserId");

CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Token" character varying(200) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastUsedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefreshTokens_Token" ON "RefreshTokens" ("Token");
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");

CREATE TABLE IF NOT EXISTS "DraftBlueskyMetadata" (
    "DraftId" uuid NOT NULL,
    "ReplyRootUri" character varying(1000) NULL,
    "ReplyRootCid" character varying(255) NULL,
    "ReplyParentUri" character varying(1000) NULL,
    "ReplyParentCid" character varying(255) NULL,
    "ReplyParentUrl" character varying(1000) NULL,
    "ReplyParentAuthor" character varying(255) NULL,
    "ReplyParentText" text NULL,
    CONSTRAINT "PK_DraftBlueskyMetadata" PRIMARY KEY ("DraftId"),
    CONSTRAINT "FK_DraftBlueskyMetadata_Drafts_DraftId" FOREIGN KEY ("DraftId") REFERENCES "Drafts" ("Id") ON DELETE CASCADE
);
