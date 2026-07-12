# CHAT_SERVICE_REFACTORING.md — Refactoring ChatService into Tool-specific Services

## Goal
The goal of this refactoring is to split the monolithic `ChatService` class, extracting the definitions and execution logic of each LLM function-calling tool into its own service class. We will also define a common `IChatTool` interface to standardize tool implementations, making it simple to add or modify tools in the future.

## Proposed Design

### 1. The `IChatTool` Interface
We will define `IChatTool` in `api/SocialWorker.Api/Features/Chat/IChatTool.cs`:
```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat;

public interface IChatTool
{
    string Name { get; }
    string Description { get; }
    JsonElement Parameters { get; }
    bool RequiresVision => false;
    Task<object> ExecuteAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct);
}
```

### 2. Move Tool Implementations
We will create a new folder `api/SocialWorker.Api/Features/Chat/Tools/` and define the following classes, each implementing `IChatTool`:

1. **`ReplaceEditorContentTool.cs`**
   - Implements `replace_editor_content`
   - Injects `IServiceScopeFactory` to manage DB operations.
2. **`ProposeStageTransitionTool.cs`**
   - Implements `propose_stage_transition`
   - Simple synchronous/logical response.
3. **`ListSourcesTool.cs`**
   - Implements `list_sources`
   - Injects `IServiceScopeFactory` to retrieve attached source metadata.
4. **`FetchSourceTool.cs`**
   - Implements `fetch_source`
   - Injects `IServiceScopeFactory` to retrieve source content.
5. **`ViewImageTool.cs`**
   - Implements `view_image`
   - Injects `IServiceScopeFactory` to read/process images from disk.
   - Sets `RequiresVision = true`.

### 3. Register Tools in Dependency Injection
We will register all `IChatTool` instances in `Program.cs`. We can register them as `Transient` or `Scoped` so they can be injected into `ChatService`.
```csharp
builder.Services.AddScoped<IChatTool, ReplaceEditorContentTool>();
builder.Services.AddScoped<IChatTool, ProposeStageTransitionTool>();
builder.Services.AddScoped<IChatTool, ListSourcesTool>();
builder.Services.AddScoped<IChatTool, FetchSourceTool>();
builder.Services.AddScoped<IChatTool, ViewImageTool>();
```

### 4. Refactor `ChatService`
We will update `ChatService` to:
- Inject `IEnumerable<IChatTool> tools`.
- Build the `payload.Tools` list dynamically by querying `tool.Name`, `tool.Description`, `tool.Parameters`, and matching `tool.RequiresVision` against `supportsVision`.
- Simplify `ExecuteToolAsync` to dynamically look up the matching tool in the injected list and invoke its `ExecuteAsync` method.

## Verification
- Run `docker compose build api` to verify compilation.
- Ensure the chat flows and tool executions continue to work as expected.
