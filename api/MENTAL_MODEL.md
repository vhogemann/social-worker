# MENTAL_MODEL.md — api (SocialWorker.Api & Tests)

## Purpose

This document provides the technical architecture, directory ownership map, and verification guidelines for the backend Web API service (`SocialWorker.Api`) and its test suite (`SocialWorker.Api.Tests`).

## Tech Stack

- **Runtime & SDK**: .NET 10.0 (C# 13 / C# 14 preview features, Nullable Reference Types enabled, Global Usings).
- **Framework**: ASP.NET Core Minimal APIs.
- **ORM & Database**: Entity Framework Core 10 with `Npgsql.EntityFrameworkCore.PostgreSQL`, PostgreSQL 16.
- **Markdown AST**: `Markdig` CommonMark parser for segment splitting (`---`), link extraction, and facet detection.
- **AI & LLM**: `OpenAI` .NET SDK with configurable `BaseUrl` supporting OpenAI, OpenRouter, and Ollama.
- **Scheduling**: `Cronos` for cron-based recurrence pattern parsing.
- **Graphics & Formatting**: `SkiaSharp` (native Linux assets) and `ColorCode.Core` for code image generation.
- **Validation**: `FluentValidation` for request contract enforcement.
- **Testing**: xUnit with `Microsoft.AspNetCore.Mvc.Testing` and in-memory/isolated SQLite or PostgreSQL test harness.

## System Topology & Entrypoints

```
api/
├── Dockerfile                         # Production multi-stage build (SDK -> runtime)
├── SocialWorker.Api/
│   ├── Program.cs                     # WebApplication builder, DI container configuration, middleware pipeline
│   ├── Data/
│   │   ├── AppDbContext.cs            # EF Core DbContext with model definitions & query filters
│   │   ├── Entities/                  # Core domain entities (Draft, Source, Post, Account, Schedule, etc.)
│   │   └── Migrations/                # Version-controlled EF Core SQL migrations
│   ├── Features/                      # Vertical slice feature folders
│   └── Infrastructure/                # Cross-cutting concerns (Auth, Llm, Markdown, Platforms, Security)
└── SocialWorker.Api.Tests/            # Comprehensive unit and integration test suite (825 tests)
```

### Entry & Startup Sequence

1. **`Program.cs`**:
   - Loads configuration: `appsettings.json`, environment variables, and optional mounted overrides (`/app/config/appsettings.json:ro`).
   - Configures Data Protection keys persisted to disk (`/root/.aspnet/DataProtection-Keys`).
   - Registers core services: Postgres DB Context (`Npgsql`), Background Task queue, Publishers, MCP Registry, and LLM adapters.
2. **`Infrastructure/Extensions/AppStartupExtensions.cs`**:
   - Maps Minimal API route groups: `/api/auth`, `/api/accounts`, `/api/drafts`, `/api/sources`, `/api/media`, `/api/publishing`, `/api/feeds`, `/api/workflows`, `/api/schedules`, `/api/outbox`, `/api/templates`, `/api/mcp`, and `/api/background-tasks`.
   - Automatic database migration and baseline data seeding during container bootstrap.

## Core Architectural Patterns

### 1. Vertical Feature Slices

Endpoints and domain logic are structured into vertical feature folders under `Features/`:
- **`Drafts/`**: Draft lifecycle management, thread stage transitions (`RoughDraft -> Sourcing -> Refining -> Formatting -> Ready -> Sent`), segment ordering, and AST content updates.
- **`Publishing/`**: Multi-platform publishing engine implementing `IPublisher`:
  - `BlueskyPublisher`: Chained post threads, facets, single/multiple image blobs.
  - `LinkedInPublisher`: Personal profile and organization company page authoring.
  - `MetaThreadsPublisher`: Multi-post threads, carousel containers, media container status polling, and token refreshing.
  - `TwitterPublisher`: OAuth 2.0 PKCE, tweet thread replies, 280 weighted-char limits, and media upload.
  - `Resilience/PublishResumePlanner`: Checkpoints publishing progress per segment; allows resuming failed multi-post threads without duplicating already-posted segments.
- **`Sources/` & `Sources/Instagram/`**: Source ingestion for URLs, uploaded files, and social profiles.
  - `InstagramBusinessDiscoveryClient`: Queries Meta Graph API for public business/creator profiles, reels, and media.
  - `InstagramOAuthService`: Handles user access token retrieval and caller Instagram Business Account ID discovery.
  - `SourceTranscriptionService`: Submits media to the Python `transcriber` service and stores Whisper transcript results.
- **`Feeds/`**: RSS/Atom, YouTube channel, and multi-platform social feeds (Instagram, X, Telegram, Reddit, TikTok, Bluesky) resolved deterministically to RSSHub routes or Graph API fallback via `RssHubRouteResolver` and `RssHubFeedDiscoveryService`. Manages subscription, polling, entry deduplication, media extraction, and workflow trigger dispatch.
- **`Workflows/`**: Automated source-to-draft generation pipeline. Implements `IWorkflowSourceHandler` (e.g. `FeedWorkflowSourceHandler`, `DigestWorkflowSourceHandler`) to process feed items into formatted thread drafts using prompt templates.
- **`ReplyAgents/`**: Autonomous reply agent library (`ReplyAgent`, `WorkflowCommentThread`, `WorkflowCommentAudit`).
  - Many-to-one workflow assignment: Workflows point to `ReplyAgentId` (nullable FK).
  - `IReplyPlatformAdapter`: Multi-platform abstraction for fetching post reply trees and publishing comments (implements `BlueskyReplyAdapter`, `MetaThreadsReplyAdapter`, Twitter/LinkedIn stubs).
  - `CommentEnrichmentService`: Pre-fetches context for links (via `WebScraperService`) and images (via vision LLM).
  - `CommentRankingService`: Two-phase evaluation, scoring 1-10, intent classification, and terminal closure detection.
  - `ReplyGenerationService`: Persona-prompted reply synthesis.
  - `CommentThreadSummarizer`: Rolling thread summary cache preventing redundant comment retrieval.
  - Hard stop criteria: Branch depth limits, per-user limits, bot self-reply prevention, terminal sentiment closure detection.
  - Background tasks: `"workflow-reply-check-post"` and `"reply-thread-summary"` queued by `ReplyAgentPollingHostedService`.
  - Minimal API endpoints at `/api/reply-agents` (CRUD) and `/api/workflows/{workflowId}/reply-agent` (assignment & workflow audits).
- **`Scheduling/`**: Content calendar engine.
  - `ScheduleEvaluationHostedService`: Recurring background hosted service (running every 15 min) that invokes `ScheduleSlotEvaluatorService` to re-score decay and continuously fill upcoming slots.
  - `ScheduleSlotEvaluatorService`: Core engine matching queued drafts with available schedule slot windows calculated via `SlotWindowCalculator` and `Cronos`, handling level-2 time decay and cascading preemption.
  - `ScheduleQueueService`: Priority-decayed draft queue management with idempotent enqueueing and active item unique constraint.
  - Review Outbox: Human-in-the-loop triage before post dispatch.
- **`BackgroundTasks/`**: Robust asynchronous work orchestration.
  - `PostgresBackgroundTaskQueue`: Uses PostgreSQL 16 `FOR UPDATE SKIP LOCKED` for concurrency-safe, durable queueing across container restarts.
  - `BackgroundTaskHostedService`: Worker pool executing transcriptions, web scraping, feed syncs, AI summarization, reply checks, and thread compaction with lease heartbeat tracking and failure retries.
  - `BackgroundTaskRetentionService`: Prunes task logs, dead runs, and succeeded/expired schedule queue items older than 7 days.
- **`Mcp/`**: Model Context Protocol integration.
  - Stateless JSON-RPC 2.0 at `POST /api/mcp` and streaming Server-Sent Events at `GET /api/mcp/sse`.
  - Exposes 35 tools covering drafts, sources, workflows, feeds, schedules, calendar events, accounts, and reply agents (`list_reply_agents`, `create_reply_agent`, `update_reply_agent`, `assign_reply_agent`, `get_reply_agent`, `configure_reply_agent`, `trigger_reply_check`, `list_comment_audits`, `reply_to_comment`).

### 2. Static Entity Factories & Invariant Protection

Domain models enforce valid states through static factory methods (e.g., `Draft.Create`, `Source.CreateUrl`, `Source.CreateFile`, `LlmProvider.Create`, `FeedSubscription.Create`, `ReplyAgent.Create`). Direct invalid instantiation is disallowed.

### 3. Markdown AST & CommonMark Thread Segmentation

Threads are authored as CommonMark documents where posts are separated by horizontal rules (`---`). The `ThreadDocumentParser` in `Infrastructure/Markdown/` decomposes text into structured segments, extracts inline hyperlinks and media, and computes character budgeting per target platform.

## Database & Migrations

- Migrations are version-controlled SQL scripts residing in `Data/Migrations/`.
- `DbUp` automatically discovers and applies unapplied SQL migrations on application startup in `Program.cs` / `AppStartupExtensions.cs`.

## Verification & Testing

All commands must be executed within Docker containers via the `tooling` profile:

```bash
# Build API project
docker compose --profile tooling run --rm dotnet build

# Run full test suite (697 tests)
docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj

# Run single test class or filter
docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj --filter "FullyQualifiedName~ReplyAgents"
```
