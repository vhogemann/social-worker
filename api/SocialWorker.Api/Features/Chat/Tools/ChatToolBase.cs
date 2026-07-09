using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat;

public abstract class ChatToolBase<TArgs, TResult> : IChatTool<TArgs, TResult>
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonElement Parameters { get; }
    public virtual bool RequiresVision => false;

    public abstract Task<TResult> ExecuteAsync(TArgs args, Guid? draftId, Guid userId, CancellationToken ct);

    public async Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var args = JsonSerializer.Deserialize<TArgs>(argumentsJson, options)
            ?? throw new InvalidOperationException($"Failed to deserialize arguments to {typeof(TArgs).Name}");

        var result = await ExecuteAsync(args, draftId, userId, ct);
        return BuildResult(result);
    }

    protected virtual ToolExecutionResult BuildResult(TResult result)
    {
        return new ToolExecutionResult(result!);
    }
}
