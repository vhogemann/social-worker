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
    Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct);
}

public interface IChatTool<TArgs, TResult> : IChatTool
{
    Task<TResult> ExecuteAsync(TArgs args, Guid? draftId, Guid userId, CancellationToken ct);
}
