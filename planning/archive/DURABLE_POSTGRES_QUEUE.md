# Durable PostgreSQL `SKIP LOCKED` Task Queue Plan

## Overview
This plan establishes a **durable, transactional, distributed task queue** backed by PostgreSQL 16 `FOR UPDATE SKIP LOCKED`, replacing the in-memory `System.Threading.Channels` queue.

---

## Architectural Highlights

```mermaid
sequenceDiagram
    autonumber
    participant App as API Controller / Service
    participant Queue as PostgresJobQueue
    participant DB as PostgreSQL ("BackgroundTasks")
    participant W1 as Worker 1 (Node A)
    participant W2 as Worker 2 (Node B)
    participant Handler as IBackgroundTaskHandler

    Note over App,DB: 1. Enqueue (Transactional Outbox)
    App->>Queue: EnqueueAsync("youtube-transcript", payload)
    Queue->>DB: INSERT INTO "BackgroundTasks" (Status='Pending')

    Note over DB,W2: 2. Distributed Atomic Dequeue (SKIP LOCKED)
    par Concurrent Workers Polling
        W1->>DB: UPDATE ... FOR UPDATE SKIP LOCKED (Worker 1)
        DB-->>W1: Returns Task 1 (Locked for 5m)
        W2->>DB: UPDATE ... FOR UPDATE SKIP LOCKED (Worker 2)
        DB-->>W2: Returns Task 2 (Locked for 5m)
    end

    Note over W1,Handler: 3. Strongly-Typed Execution & Recovery
    W1->>Handler: ExecuteAsync(payload, ct)
    alt Task Success
        W1->>DB: UPDATE "BackgroundTasks" SET Status='Completed', CompletedAt=NOW()
    else Transient Failure (Attempt < MaxAttempts)
        W1->>DB: UPDATE "BackgroundTasks" SET Status='Pending', NextAttemptAt=NOW() + Backoff
    else Permanent Failure (Attempt >= MaxAttempts)
        W1->>DB: UPDATE "BackgroundTasks" SET Status='Failed', LastError=ex
    end
```

---

## Core Components

1. **Entity**: `BackgroundTask.cs` stored in table `"BackgroundTasks"`.
2. **Queue Interface**: `IBackgroundTaskQueue` supporting typed enqueueing and atomic `FOR UPDATE SKIP LOCKED` dequeueing.
3. **Worker Pool**: `BackgroundJobHostedService` running `N` parallel workers (default 4) with graceful backoff and crash lease recovery.
4. **Typed Handlers**:
   - `YouTubeTranscriptTaskHandler`
   - `FeedIngestionTaskHandler`
   - `ChatSummaryTaskHandler`
   - `TemplateRenderTaskHandler`

---

## Verification Plan

1. **Unit & Integration Tests**:
   - `PostgresBackgroundTaskQueueTests.cs` testing concurrent dequeueing without race conditions.
   - Retry with exponential backoff and dead-lettering tests.
   - Lease expiration and automatic crash recovery tests.
2. **Execution**:
   ```bash
   docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
   ```
