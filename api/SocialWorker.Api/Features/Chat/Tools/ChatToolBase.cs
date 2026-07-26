using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat.Tools;

public abstract class ChatToolBase<TArgs, TResult> : IChatTool<TArgs, TResult>
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract JsonElement Parameters { get; }
    public virtual bool RequiresVision => false;

    public abstract Task<TResult> ExecuteAsync(TArgs args, Models.ToolExecutionContext context);

    public async Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Models.ToolExecutionContext context)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var args = JsonSerializer.Deserialize<TArgs>(argumentsJson, options)
            ?? throw new InvalidOperationException($"Failed to deserialize arguments to {typeof(TArgs).Name}");

        var result = await ExecuteAsync(args, context);
        return BuildResult(result);
    }

    protected virtual ToolExecutionResult BuildResult(TResult result)
    {
        return ToolExecutionResult.Success(result!);
    }
}
