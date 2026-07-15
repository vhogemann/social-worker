using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat.Adapters;

public interface ILlmProviderAdapter
{
    IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct);

    Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct);
}
