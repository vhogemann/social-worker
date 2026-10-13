# Plan: Workflow Pausing & Archiving (State Lifecycle Management)

## 1. Problem & Goals
Currently, workflows are always active (`Active`). When a user no longer wants to post to a channel or wants to temporarily suspend automated generation without losing history, sources, prompts, and destinations, their only option is to delete the workflow.
Deleting destroys configuration, unlinks generated drafts from their parent workflow, and severs reply agents.

We need:
1. **Workflow Status Lifecycle**:
   - `Active`: Normal operation (feed polling ingests new items, manual triggers work, reply agents poll).
   - `Paused`: Workflow is temporarily stopped (feed polling skips ingesting items for this workflow; manual trigger is prevented or warned; scheduled items remain unaffected or frozen; reply agent skips if only assigned to paused workflows).
   - `Archived`: Workflow is retired without deletion (hidden from default active list by default or switchable via tabs; ingests nothing; retains all historical references, drafts, metrics).
2. **Web UI State Controls**:
   - Tab bar / Filter on `/workflows`: **All Active** (default), **Paused**, **Archived**, and **All**.
   - Direct Pause/Resume toggle button on `WorkflowCard` and `WorkflowDetailHeader`.
   - Direct Archive/Unarchive action on `WorkflowCard` and `WorkflowDetailHeader`.
   - Clear visual status badges on workflow cards (`Active` green, `Paused` amber, `Archived` zinc/gray).
   - Confirmation modal when archiving or resuming.
3. **API & Background Ingestion Guarantees**:
   - `AutomationWorkflow.Status` (`Active`, `Paused`, `Archived`).
   - `FeedPollingService` skips workflows where `Status != WorkflowStatus.Active`.
   - `FeedIngestionTaskHandler` skips processing if workflow is `Paused` or `Archived`.
   - `ReplyAgentPollingHostedService` skips posts belonging to workflows that are `Archived` (optional: paused workflows can continue replying to existing posts or skip; standard is to allow replying to already-published posts, but skip generating new drafts).
   - MCP tools (`list_workflows`, `update_workflow`, `pause_workflow`, `resume_workflow`, `archive_workflow`) support workflow statuses.

---

## 2. Technical Design

### A. Data Model & Migration
1. **Enum**:
   ```csharp
   [JsonConverter(typeof(JsonStringEnumConverter))]
   public enum WorkflowStatus
   {
       Active,
       Paused,
       Archived
   }
   ```
2. **Entity `AutomationWorkflow`**:
   - Add property `public WorkflowStatus Status { get; set; } = WorkflowStatus.Active;`
   - Add domain methods:
     - `public void Pause()`
     - `public void Resume()`
     - `public void Archive()`
     - `public void Unarchive()`
3. **Migration `0022_add_workflow_status.sql`**:
   ```sql
   ALTER TABLE "AutomationWorkflows" ADD COLUMN IF NOT EXISTS "Status" character varying(32) NOT NULL DEFAULT 'Active';
   CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflows_Status" ON "AutomationWorkflows" ("Status");
   ```
4. **EF Core Configuration (`AppDbContext.cs`)**:
   - Map `Status` as string with max length 32 and default value `Active`.

---

### B. Ingestion & Execution Pipeline
1. **`FeedPollingService.cs`**:
   - In `PollSubscriptionAsync`:
     ```csharp
     foreach (var wf in sub.WorkflowSources.Where(ws => ws.AutomationWorkflow?.Status == WorkflowStatus.Active))
     ```
   - Include `AutomationWorkflow` in `sub.WorkflowSources` or check in `FeedIngestionTaskHandler`.
2. **`FeedIngestionTaskHandler.cs`**:
   - Check if `workflow.Status != WorkflowStatus.Active`:
     ```csharp
     if (workflow.Status != WorkflowStatus.Active)
     {
         _logger.LogInformation("Skipping feed ingestion for workflow {WorkflowId} because status is {Status}", workflow.Id, workflow.Status);
         return TaskResult.Success();
     }
     ```
3. **`WorkflowsEndpoint.cs`**:
   - Update `GET /api/workflows`:
     - Accept optional query param `WorkflowStatus? status` and `bool? includeArchived`.
     - When no param is specified, default to returning `Active` and `Paused` (or provide status counts).
   - Add `PATCH /api/workflows/{id}/status`:
     - Payload: `{ status: "Active" | "Paused" | "Archived" }`
     - Validates transition and saves `workflow.UpdatedAt`.
   - Update `AutomationWorkflowDto` to include `string Status` or `WorkflowStatus Status`.
   - Update `CreateAutomationWorkflowRequest` to accept optional `WorkflowStatus? Status`.
   - In manual `trigger`: return Bad Request if workflow is `Archived` (allow if `Active`, allow or warn if `Paused`).

---

### C. MCP Server Updates
1. **`list_workflows`**: Include `status` property in DTO output, and support optional `status` filter argument (`active`, `paused`, `archived`, `all`).
2. **New MCP tool or enhanced `update_workflow`**:
   - Support `status` in `update_workflow`.
   - Add `pause_workflow` (`workflowId`), `resume_workflow` (`workflowId`), `archive_workflow` (`workflowId`).

---

### D. Frontend / Web UI
1. **API Contracts (`web/src/api/workflows.ts`)**:
   - Update `AutomationWorkflowDto` with `status: "Active" | "Paused" | "Archived"`.
   - Add `updateWorkflowStatus(id: string, status: "Active" | "Paused" | "Archived"): Promise<AutomationWorkflowDto>`.
   - Support `status` filter in `fetchWorkflows(status?: string, includeArchived?: boolean)`.
2. **Workflows Dashboard (`web/src/components/WorkflowsPanel/WorkflowsPage.tsx`)**:
   - Filter Tabs:
     - `Active` (badge count)
     - `Paused` (badge count)
     - `Archived` (badge count)
     - `All` (badge count)
   - Dynamic search filtering by title & status.
3. **Workflow Card (`WorkflowCard.tsx`)**:
   - Status badge pill:
     - `Active`: green outline / dot (`Active`)
     - `Paused`: amber outline / pause icon (`Paused`)
     - `Archived`: zinc outline / archive box icon (`Archived`)
   - Quick action buttons:
     - If `Active`: "Pause" button (with `faPause` icon).
     - If `Paused`: "Resume" button (with `faPlay` icon).
     - More menu or Archive button (`faBoxArchive`).
4. **Workflow Detail Header (`WorkflowDetailHeader.tsx`)**:
   - Displays status badge prominently next to Title and Filter threshold.
   - Pause / Resume toggle button in header next to "Trigger Now".
   - Archive / Unarchive button in header actions.
5. **Workflow Detail Page (`WorkflowDetailPage.tsx`)**:
   - Banner notification if workflow is Paused or Archived explaining that automatic ingestion is currently paused.

---

## 3. TDD & Verification Steps
1. **TDD Unit Tests First**:
   - `AutomationWorkflowTests.cs`: test `Create`, `Pause`, `Resume`, `Archive`, and invalid transitions.
   - `WorkflowsEndpointTests.cs`: test status update endpoint, filtering by status, querying archived vs active.
   - `FeedOrchestrationServiceTests.cs` / `FeedIngestionTaskHandlerTests.cs`: verify paused and archived workflows discard or skip feed ingestion.
   - `WorkflowCard.test.tsx` & `WorkflowDetailHeader.test.tsx`: test rendering of Paused / Archived badges and action clicks.
   - `WorkflowsPage.test.tsx`: test tab filtering.
2. **Docker Tooling Verification**:
   - Run backend test suite: `docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj`
   - Run frontend test suite: `docker compose --profile tooling run --rm web-test`
3. **Container Rebuild & Health Check**:
   - Rebuild and start container stack: `docker compose up -d --build api web migrator`
   - Verify migrations apply cleanly without breaking existing workflows (`vhogemann` Twitter and Threads master workflows preserve their state as `Active`).
