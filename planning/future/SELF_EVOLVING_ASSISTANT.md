# SELF_EVOLVING_ASSISTANT.md -- Self-Improving Python Tool Library + Decoupled Workspace Routing

## Overview

Two tightly integrated features layered on top of the Python sandbox:

1. **Dynamic Tool Promotion** -- The LLM writes Python scripts during chat; promoted scripts become permanently importable modules in the sandbox library
2. **Semantic Tool Retrieval** -- A vector-based system that injects only the most relevant promoted tool schemas into the LLM context, keeping prompt size bounded
3. **Decoupled Workspace System** -- Virtual, DB-backed workspaces for context isolation (draft workspaces, scratchpads) that keep the main chat lightweight

---

## Architectural Gaps & Solutions

### Gap 1: Tool Confusion vs. Context Limits with Gemma 4 E4B (4.5B params)

**Problem**: Sending all tool schemas every turn (currently 14 static tools + potentially dozens of promoted scripts) overwhelms a small model's context window (~8K-32K tokens) and causes tool selection confusion.

**Solution**: Two-tier tool injection with semantic retrieval.

#### Tier 1: Static Core Tools (always sent)

These 7 tools are always injected because they are essential to every interaction:

```
replace_editor_content, propose_stage_transition, list_sources,
fetch_source, add_source, execute_python, list_promoted_tools
```

Total: ~7 JSON schemas, roughly 2000-3000 tokens.

#### Tier 2: Dynamic Promoted Tools (semantically retrieved)

The remaining 7 static tools (view_image, publish_platform, web_search, add_image_source, image_search, render_code_blocks, generate_platform_variants) are moved to "conditional" status -- sent only when the current draft stage or user message implies they are relevant. Additionally, promoted Python scripts are retrieved via vector similarity.

**Retrieval architecture**:

```
                      ┌──────────────────────┐
                      │  Ollama Embeddings    │
                      │  API                  │
                      │  (nomic-embed-text)   │
                      └──────────┬───────────┘
                                 │ POST /api/embeddings
                                 │ { model, prompt }
                                 ▼
┌──────────────┐    ┌──────────────────────┐
│  User message│───>│  ApiService           │
│  + stage     │    │ (embeds query,       │
└──────────────┘    │  cosine sim against  │
                    │  all promoted tools)  │
                    └──────────┬───────────┘
                               │ top-K (K=5)
                               ▼
                    ┌──────────────────────┐
                    │  LLM Chat Request     │
                    │  (core tools + top-K) │
                    └──────────────────────┘
```

**Embedding model**: Use Ollama's embeddings API with `nomic-embed-text` (137M params, runs easily on RTX 3060 alongside Gemma 4). Query: `POST /api/embeddings` with `{ model: "nomic-embed-text", prompt: userMessage }`.

**Vector storage**: Use `pgvector` extension in Postgres. Add a `PromotedTool` entity with a `float[] Embedding` column. The API computes cosine similarity in SQL:

```sql
SELECT id, name, description, (embedding <#> @queryEmbedding) * -1 AS distance
FROM promoted_tools
ORDER BY distance DESC
LIMIT 5;
```

**Fallback discovery tool**: A `list_promoted_tools` tool (always available in Tier 1) returns all promoted tool names/descriptions so the LLM can discover what exists and explicitly request a specific one if the retriever missed it.

**Gemma-specific tuning**:
- Cap dynamic tools at K=5 max per turn
- Tool descriptions must be concise (under 200 chars each) -- the promotion validator enforces this
- System prompt gets a one-line instruction: "You have access to specialized tools not listed here. Use `list_promoted_tools` to discover them."

---

### Gap 2: The Promotion Gatekeeper -- Prevention of Infinite Loops

**Problem**: The LLM may repeatedly try to promote broken scripts (infinite loop). The system must gracefully degrade and halt unproductive cycles.

**Solution**: 3-stage validation pipeline with retry caps and structured error feedback.

#### Stage 1: Structural Validation (Python sandbox)

```
LLM calls propose_promote_tool(name, code)
  → C# saves to transient /tmp/{sessionId}/{name}.py
  → C# calls POST /sandbox/validate with the code
  → Sandbox checks:
      a) Syntax parse (ast.parse)
      b) Module allowlist (no os, socket, subprocess imports)
      c) Metadata manifest extraction (JSON block in docstring)
         Required fields: name (str), description (str, <200 chars),
         parameters (JSON Schema object), returns (str)
      d) Manifest name matches the LLM-supplied name
  → Returns { valid: bool, errors: string[], manifest: object|null }
```

#### Stage 2: Execution Validation (Python sandbox)

```
If Stage 1 passes:
  → C# calls POST /sandbox/execute with a test harness:
      code = manifest.test_code or "print(module.__doc__)"
      timeout = 3s (shorter than normal execution)
  → Checks:
      a) Process exits cleanly (exit_code == 0)
      b) No stderr output
      c) Execution time < 3s
  → Returns { valid: bool, errors: string[], stdout: str, stderr: str }
```

#### Stage 3: Registration (C# backend)

```
If Stage 2 passes:
  a) Compute SHA-256 of code for dedup
  b) Check promoted_tools table -- if hash exists, return "already promoted"
  c) Write code to ./sandbox/libs/{name}.py (host path)
  d) Insert promoted_tools row with { name, description, parameters_schema,
     code_hash, embedding (computed via Ollama), user_id, created_at }
  e) Return success with the tool name and description
```

#### Retry governance

| Condition | Action |
|---|---|
| First failure | Return structured error: which stage, which check, line numbers |
| 2 failures same session | Return error + warning: "2 failed attempts remaining" |
| 3 failures same session | Return error: "Promotion rate limit reached for this session. Try a different approach or use execute_python inline." |
| Block subsequent `propose_promote_tool` calls for rest of session | C# tracks `_promotionFailCount` per ChatService session -- refuses execution at tool level after 3 |

The LLM **cannot bypass** validation. There is no admin `force_promote` tool. The C# backend is the sole gatekeeper.

**Error feedback format** (returned to LLM as tool result):

```
PROMOTION FAILED (Stage 1 - Structural Validation)
- Syntax error at line 12: unexpected indent
- Import of 'requests' is not allowed in the sandbox
- Missing required manifest field: 'parameters'

Your script was not saved. Fix the issues and call propose_promote_tool again.
(2 promotion attempts remaining this session)
```

---

### Gap 3: Context Isolation with Decoupled Workspaces

**Problem**: The main chat accumulates all messages across all topics, bloating context and confusing a 4.5B model. Draft-specific data pollutes the executive chat.

**Solution**: Virtual DB-backed workspaces that the LLM explicitly switches between via tool calls.

#### Workspace model

New entities:

```csharp
public class Workspace {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }          // "main_chat" | "draft" | "scratchpad"
    public Guid? DraftId { get; set; }
    public Draft? Draft { get; set; }
    public string? SystemPrompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkspaceMessage {
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; }
    public string Role { get; set; }          // "user" | "assistant" | "system"
    public string Content { get; set; }
    public string? ToolCallsJson { get; set; }
    public int TokenCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Tool interface** (new chat tools):

```
list_workspaces()            → [{ id, name, type, message_count }]
read_workspace(workspace_id) → recent messages + system prompt
append_to_workspace(workspace_id, content, role)
create_workspace(name, type, draft_id?)
archive_workspace(workspace_id)
```

**Main Chat** is `Workspace.Type == "main_chat"` -- holds the executive orchestrator with global system prompts, always present in context.

**Draft Workspaces** are `Workspace.Type == "draft"` -- linked to a specific `Draft` entity.

**Context isolation**: When processing `read_workspace(id)`, the C# backend loads ONLY that workspace's last 20 messages. The main chat's payload does not contain workspace history -- only the tool result text.

**Scratchpad** (`Type == "scratchpad"`) -- a lightweight text buffer for intermediate results, data matrices, or logs without creating a full draft.

**Explicit routing**: The LLM must explicitly call `create_workspace`, `append_to_workspace`, and `read_workspace` to move between workspaces -- no implicit switching.

---

### Gap 4: Telemetry & Visibility in an Air-Gapped Sandbox

**Problem**: The sandbox has no network. How do we surface execution bugs, compilation errors, and runtime telemetry to the user?

**Solution**: Three-layer telemetry pipeline.

#### Layer 1: Sandbox-side structured responses

Every sandbox endpoint returns a `telemetry` envelope alongside the user-visible result:

```json
// POST /execute response
{
  "stdout": "42\n",
  "stderr": "",
  "exit_code": 0,
  "execution_time_ms": 2.3,
  "telemetry": {
    "peak_memory_kb": 8192,
    "imports_used": ["math", "json"],
    "execution_phase": "runtime",
    "truncated": false,
    "pid": 42,
    "signal": null
  }
}

// POST /validate response
{
  "valid": false,
  "errors": ["Import 'requests' is not allowed."],
  "telemetry": {
    "execution_phase": "import_check",
    "checked_imports": ["json", "math", "requests"],
    "blocked_imports": ["requests"]
  }
}
```

The sandbox logs all requests to stdout (Docker captures this) with structured JSON:

```
{"event": "execute", "duration_ms": 2.3, "code_size": 45, "exit_code": 0, "pid": 42}
{"event": "validate", "valid": false, "reason": "blocked_import", "import": "requests"}
```

#### Layer 2: API-side interception and logging

`SandboxClient` wraps every call with structured logging:

```csharp
var sw = Stopwatch.StartNew();
var response = await _httpClient.PostAsync(...);
sw.Stop();

_log.LogInformation(
    "Sandbox {Endpoint} | {StatusCode} | {Duration:F1}ms | CodeSize: {Size}",
    endpoint, response.StatusCode, sw.Elapsed.TotalMilliseconds, code.Length);

// Slow execution warning
if (sw.Elapsed.TotalSeconds > 3)
    _log.LogWarning("Sandbox execution took {Duration:F1}s", sw.Elapsed.TotalSeconds);
```

#### Layer 3: User-facing debug tools

Two new debug tools available to the LLM:

```
get_sandbox_logs(count=5)     → Recent sandbox execution telemetry (JSON)
get_promotion_history()       → All promotion attempts this session (pass/fail/reason)
```

Telemetry is rendered in the chat as collapsible details blocks (web UI).

**Async health monitoring**: A background `SandboxHealthService` polls `GET /health` every 30 seconds. If the sandbox is unreachable for 3 consecutive polls, it sets a `SandboxStatus` cache key to "degraded" -- the `execute_python` tool returns a friendly error instead of timing out.

**Promotion audit trail**: A new `PromotionAttempt` DB table stores every promotion attempt:

```csharp
public class PromotionAttempt {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string ScriptName { get; set; }
    public string CodeHash { get; set; }
    public bool Valid { get; set; }
    public string? FailureReason { get; set; }
    public string TelemetryJson { get; set; }    // Full sandbox telemetry
    public DateTime CreatedAt { get; set; }
}
```

This enables an admin dashboard (future) for reviewing what the LLM has attempted to promote.

---

## Phased Implementation Plan

### Phase 1: Sandbox Foundation + ExecutePythonTool

**Prerequisites**: Docker, existing .NET API, existing compose file.

**Deliverables**:
- `sandbox/` directory with Dockerfile, server.py, runner.py
- `SandboxClient.cs` + `SandboxOptions.cs` + DI registration
- `ExecutePythonTool.cs` chat tool
- `sandbox-net` network with `internal: true` in compose
- Unit tests for client + tool
- `GET /health` and `POST /execute` endpoints

**Validation**: `docker compose up --build` starts sandbox. API can call `/execute` with `print("hello")` and get back `{"stdout": "hello\n"}`. Web UI chat can invoke `execute_python` via LLM.

**Already defined in**: `planning/future/PYTHON_SANDBOX.md`

---

### Phase 2: Promotion Pipeline

**Prerequisites**: Phase 1 complete (sandbox running with POST /execute).

**New files**:

```
sandbox/
  validate.py          # Validation endpoint logic
  manifest.py          # Metadata manifest parser

api/SocialWorker.Api/
  Features/Promotion/
    Services/
      PromotionService.cs      # Orchestrates 3-stage validation
      ToolLibraryService.cs     # Reads/writes sandbox/libs/*.py
    Entities/
      PromotedTool.cs          # DB entity
      PromotionAttempt.cs      # Audit trail entity
    Tools/
      ProposePromoteTool.cs    # Chat tool: propose_promote_tool
      ListPromotedToolsTool.cs # Chat tool: list_promoted_tools
  Infrastructure/Embedding/
    EmbeddingService.cs        # Ollama embeddings client
    IEmbeddingService.cs

docker-compose.yml
  # sandbox gets POST /validate endpoint
  # api gets Embedding__BaseUrl env var pointing to ollama
```

**New sandbox endpoint**:

```
POST /validate
  Request:  { "code": str }
  Response: {
    "valid": bool,
    "errors": string[],
    "manifest": { "name": str, "description": str, "parameters": { ... }, "returns": str } | null,
    "telemetry": { ... }
  }
  Status: 200
```

**New DB entities**:

```csharp
public class PromotedTool {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }                 // Also the filename (no .py)
    public string Description { get; set; }            // < 200 chars
    public string ParametersSchema { get; set; }       // JSON Schema string
    public string Returns { get; set; }                // "string" | "number" | "object" etc.
    public string CodeHash { get; set; }               // SHA-256 of code file
    public float[]? Embedding { get; set; }            // pgvector float[]
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Computed at registration time by calling Ollama embeddings
    // Embedding dimension: 768 (nomic-embed-text)
}
```

**Metadata manifest format** (embedded in Python docstring, required for promotion):

```python
"""
{"name": "calculate_bmi", "description": "Compute BMI from weight and height", "parameters": {"type": "object", "properties": {"weight_kg": {"type": "number"}, "height_m": {"type": "number"}}, "required": ["weight_kg", "height_m"]}, "returns": "number"}
"""
def calculate_bmi(weight_kg, height_m):
    return weight_kg / (height_m ** 2)
```

**Chat tool: `propose_promote_tool`**:

```csharp
public sealed record ProposePromoteArgs(string Name, string Code);

// Executes:
// 1. Write code to temp file
// 2. Call POST /sandbox/validate
// 3. If pass, call POST /sandbox/execute with test harness
// 4. If pass, compute SHA-256, check dedup
// 5. Compute embedding via Ollama
// 6. Save to ./sandbox/libs/{name}.py
// 7. Insert PromotedTool row
// 8. Return success message
```

**Docker compose additions**:

```yaml
api:
  environment:
    Embedding__BaseUrl: http://ollama:11434
    Embedding__Model: nomic-embed-text
    Embedding__Enabled: true
```

**Validation**:
- Manual: Chat "write a BMI calculator and promote it" -- LLM calls `propose_promote_tool`, tool file appears in `./sandbox/libs/`, subsequent `execute_python` calls can `import calculate_bmi`
- Test: `PromotionServiceTests` covering: valid promotion, blocked import (os), syntax error, missing manifest, duplicate hash, 3-failure rate limit

---

### Phase 3: Semantic Tool Retrieval

**Prerequisites**: Phase 2 complete (promoted tools exist in DB with embeddings).

**Changes to existing files**:

**`ChatService.cs`** -- replace the unconditional `_tools.Where(...)` with a selection strategy:

```csharp
// Static core tools (always sent)
var coreToolNames = new HashSet<string> {
    "replace_editor_content", "propose_stage_transition",
    "list_sources", "fetch_source", "add_source",
    "execute_python", "list_promoted_tools"
};

// Conditionally included static tools based on draft stage
var conditionalNames = GetConditionalTools(session.Draft.Status);

// Dynamic promoted tools (top-K by embedding similarity)
var latestMessage = req.Messages.LastOrDefault()?.Content.FirstOrDefault()?.Text ?? "";
var promotedToolSchemas = await _retrievalService.GetRelevantToolsAsync(
    latestMessage, topK: 5, userId, ct);

// Merge all tool schemas
var allToolSchemas = coreToolNames
    .Union(conditionalNames)
    .Select(name => _toolSchemaCache.GetSchema(name))
    .Concat(promotedToolSchemas)
    .ToList();
```

**New files**:

```
api/SocialWorker.Api/
  Features/Chat/Services/
    ToolRetrievalService.cs     # Embedding + SQL similarity search
    ToolSchemaCache.cs          # Static tool schemas by name
```

**`ToolRetrievalService`**:

```csharp
public sealed class ToolRetrievalService {
    private readonly EmbeddingService _embeddings;
    private readonly AppDbContext _db;

    public async Task<List<OpenAiTool>> GetRelevantToolsAsync(
        string query, int topK, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || !_embeddings.Enabled)
            return new();

        var queryEmbedding = await _embeddings.GenerateEmbeddingAsync(query, ct);

        // pgvector cosine distance (inner product on normalized vectors)
        var tools = await _db.PromotedTools
            .Where(t => t.UserId == userId && t.Embedding != null)
            .OrderBy(t => t.Embedding!.InnerProduct(queryEmbedding))
            .Take(topK)
            .Select(t => new { t.Name, t.Description, t.ParametersSchema })
            .ToListAsync(ct);

        return tools.Select(t => new OpenAiTool {
            Function = new() {
                Name = t.Name,
                Description = t.Description,
                Parameters = JsonSerializer.Deserialize<JsonElement>(t.ParametersSchema)
            }
        }).ToList();
    }
}
```

**pgvector setup**:
- Add `pgvector` NuGet package to API project
- EF migration: `CREATE EXTENSION IF NOT EXISTS vector`
- Column type: `e.Property(x => x.Embedding).HasColumnType("vector(768)");`
- Index: `e.HasIndex(x => x.Embedding).HasMethod("ivfflat").HasOperators("vector_cosine_ops");`

**Validation**:
- With 3 promoted tools (bmi_calculator, text_analyzer, date_formatter), send "calculate my BMI" -- only bmi_calculator schema appears in the tool list sent to the LLM
- `list_promoted_tools` still returns all 3
- Test: `ToolRetrievalServiceTests` with known embeddings, verify cosine similarity ranking

---

### Phase 4: Decoupled Workspace System

**Prerequisites**: Phase 1 sandbox running, core chat tools stable.

**New files**:

```
api/SocialWorker.Api/
  Data/Entities/
    Workspace.cs
    WorkspaceMessage.cs
  Features/Workspaces/
    Endpoints/
      WorkspaceEndpoints.cs     # REST CRUD for workspaces
    Services/
      WorkspaceService.cs       # Business logic
    Tools/
      ListWorkspacesTool.cs     # Chat tool: list_workspaces
      ReadWorkspaceTool.cs      # Chat tool: read_workspace
      AppendToWorkspaceTool.cs  # Chat tool: append_to_workspace
      CreateWorkspaceTool.cs    # Chat tool: create_workspace
```

**Docker**: No new containers. Everything runs in existing API + Postgres.

**Workspace creation** (triggered by LLM via `create_workspace` tool, or by user via web UI):

```csharp
// Called when LLM says "I'll write this in a scratchpad"
// or when user clicks "New Draft Workspace" in web UI
public async Task<Workspace> CreateWorkspaceAsync(
    string name, string type, Guid? draftId, Guid userId, CancellationToken ct)
{
    var workspace = new Workspace {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = name,
        Type = type,
        DraftId = draftId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    _db.Workspaces.Add(workspace);
    await _db.SaveChangesAsync(ct);
    return workspace;
}
```

**Context isolation mechanism**:

When the LLM calls `read_workspace(id)`, the backend:
1. Loads the workspace's last 20 messages
2. Loads the workspace's system prompt (if set) or the default
3. Loads the linked Draft's current content + sources (if `DraftId` is set)
4. Returns all of this as a structured text block in the tool result

The main chat's conversation payload (`convo` in ChatService) does NOT contain the workspace messages. Only the tool result text is in context.

```csharp
// ReadWorkspaceTool.ExecuteAsync:
var messages = await _db.WorkspaceMessages
    .Where(m => m.WorkspaceId == args.WorkspaceId)
    .OrderByDescending(m => m.CreatedAt)
    .Take(20)
    .ToListAsync(ct);

var sb = new StringBuilder();
sb.AppendLine($"Workspace: {workspace.Name} ({workspace.Type})");
sb.AppendLine();

if (workspace.Draft != null)
{
    sb.AppendLine("--- Linked Draft ---");
    sb.AppendLine(workspace.Draft.Content);
    sb.AppendLine();
}

sb.AppendLine("--- Recent Messages ---");
foreach (var msg in messages.AsEnumerable().Reverse())
{
    sb.AppendLine($"[{msg.Role}] {msg.Content[..Math.Min(msg.Content.Length, 500)]}");
}
// ... optionally append ...
```

**Tool-dependent workspace switching decision**:

The LLM does NOT automatically switch workspaces. It must explicitly:
1. Decide context is getting crowded → call `create_workspace("Data Analysis Scratchpad", "scratchpad")`
2. Copy relevant data → call `append_to_workspace(id, "raw data: ...", "user")`
3. Continue work there → call `read_workspace(id)` to see what's there

This explicit routing is important for a 4.5B model -- implicit workspace switching would confuse it.

**Validation**:
- Chat "create a scratchpad for these numbers" → LLM calls `create_workspace`, then `append_to_workspace`
- Chat "what's in my scratchpad?" → LLM calls `read_workspace`, returns the stored data
- Test: `WorkspaceServiceTests` for CRUD, `ReadWorkspaceToolTests` for context assembly

## Threat Model & Safety Guardrails

### Layer 1: Container Isolation (primary defense)

| Threat | Mitigation |
|---|---|
| Code escapes container | Non-root user (`sandbox`), no `sudo`, no `cap_add` in compose, read-only root filesystem |
| Code modifies library files | `/app/libs` mounted `:ro` inside container -- writes are impossible |
| Code persists data | Stateless execution -- temp files deleted in `finally`, no writable volumes, ephemeral container |
| Code consumes all RAM | Docker `deploy.resources.limits.memory: 128M` -- OOM kill enforced by kernel |
| Code consumes all CPU | Docker `deploy.resources.limits.cpus: 0.5` -- kernel cgroups throttle |
| Code runs forever | `subprocess.run(timeout=5)` + `SIGALRM` secondary guard -- double timeout enforcement |
| Code forks bombs | Module allowlist blocks `os.fork`; cgroup `pids_limit: 64` (Docker default) |
| Code scans internal network | `networks: sandbox-net { internal: true }` -- zero network connectivity |
| Code exfiltrates data via DNS | Air-gapped -- no DNS resolution possible |
| Code writes to `/tmp` | Allowed but ephemeral (container restart wipes); monitored via telemetry |

### Layer 2: Python Runtime Restrictions (secondary defense)

| Threat | Mitigation |
|---|---|
| `__import__` to bypass allowlist | Prologue removes `__import__` from builtins before user code runs |
| `eval()` / `exec()` to execute arbitrary code | Both removed from builtins in prologue |
| `open()` to read host files | Builtin removed; even `/proc` and `/etc` reads blocked |
| `os.system()` / `subprocess.run()` | Module allowlist blocks `os` and `subprocess` completely |
| `ctypes` to call C libraries | Module allowlist excludes `ctypes` |
| Reflection to access forbidden modules | Custom `sys.meta_path` import hook blocks any module not on allowlist |
| `sys.modules` manipulation to re-enable | `sys` is restricted -- `sys.modules` is made read-only via prologue |
| Platform-specific exploits | `platform` module on allowlist but returns sandboxed Python info only |

### Layer 3: API-Level Enforcement (tertiary defense)

| Threat | Mitigation |
|---|---|
| LLM tries to promote malicious code | 3-stage validation pipeline blocks dangerous imports at structural check |
| LLM tries to bypass validation | No `force_promote` tool exists; validation is server-enforced, not LLM-enforced |
| LLM sends huge code payload | `SandboxClient` enforces max request body size (100KB via `HttpClient.MaxResponseContentBufferSize`) |
| LLM causes infinite promotion loop | 3-failure-per-session cap blocks further promotion attempts |
| Token leaked via sandbox output | Output truncated at 10KB; API logs scrub potential secrets (optional, Phase 2+) |

### Layer 4: Host System Protection

| Threat | Mitigation |
|---|---|
| Container escape to host | No `--privileged`, no `--pid=host`, no `--network=host`, no volume mounts from host (except read-only libs dir), no `security_opt` relaxed |
| Library volume symlink attack | `./sandbox/libs` on host should be owned by non-root (or root-owned, `:ro` in container) -- running `chown -R root:root ./sandbox/libs` on bootstrap prevents symlink attacks from the sandbox |
| Compose misconfiguration | Documented in deploy checklist: never remove `internal: true`, never expose sandbox ports, never run as root |
| Host Docker socket exposure | Sandbox container MUST NOT mount `/var/run/docker.sock` -- compose file explicitly avoids this |
| Resource starvation of host | `deploy.resources.limits` ensures sandbox can't consume host resources |

### Deployment Security Checklist

- [ ] `sandbox-net` has `internal: true` -- verified with `docker network inspect sandbox-net`
- [ ] Sandbox container runs as non-root -- verified with `docker exec sandbox whoami`
- [ ] No ports exposed for sandbox in compose -- verified with `docker compose ps`
- [ ] `/app/libs` mounted `:ro` (read-only) -- verified with `docker inspect sandbox`
- [ ] API connects to `sandbox-net` but NOT the reverse -- verified with `docker network inspect sandbox-net`
- [ ] `deploy.resources.limits.memory` set to 128M -- verified with `docker stats sandbox`
- [ ] `deploy.resources.limits.cpus` set to 0.5 -- verified with `docker stats sandbox`
- [ ] No dangerous capabilities (`SYS_PTRACE`, `SYS_ADMIN`, `NET_ADMIN`) in compose
- [ ] Sandbox `Dockerfile` uses `USER sandbox` before `CMD`
- [ ] No `environment:` variables containing secrets in sandbox compose section
- [ ] `./sandbox/libs` directory on host is not world-writable
- [ ] API enforces `MaxRequestBodySize` for sandbox client (100KB)
- [ ] API enforces promotion retry cap (3 failures/session)

---

## What's In/Out of Scope Per Phase

| Feature | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|---|---|---|---|---|
| Execute arbitrary Python | Yes | Yes | Yes | Yes |
| Module allowlist | Yes | Yes | Yes | Yes |
| Air-gapped container | Yes | Yes | Yes | Yes |
| Resource limits | Yes | Yes | Yes | Yes |
| Hybrid lib volume (manual) | Yes | Yes | Yes | Yes |
| POST /validate endpoint | No | Yes | Yes | Yes |
| `propose_promote_tool` | No | Yes | Yes | Yes |
| `list_promoted_tools` | No | Yes | Yes | Yes |
| Promotion audit trail | No | Yes | Yes | Yes |
| 3-failure retry cap | No | Yes | Yes | Yes |
| pgvector setup | No | No | Yes | Yes |
| Embedding generation | No | No | Yes | Yes |
| Semantic tool retrieval | No | No | Yes | Yes |
| Conditional static tools | No | No | Yes | Yes |
| Workspace entities | No | No | No | Yes |
| Workspace chat tools | No | No | No | Yes |
| Context isolation | No | No | No | Yes |

---

## Links

- Sandbox foundation: `planning/future/PYTHON_SANDBOX.md`
- Telegram bridge: `planning/future/TELEGRAM_INTEGRATION.md`
- Main plan tracker: `planning/PLAN.md`