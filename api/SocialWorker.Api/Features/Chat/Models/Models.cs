using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Chat;

public static class ChatModels
{
    public sealed class ChatRequest
    {
        public List<UiMessage> Messages { get; set; } = new();
        public string? System { get; set; }
        public string? Editor { get; set; }
        public Guid? DraftId { get; set; }
    }

    public sealed class UiMessage
    {
        public string Role { get; set; } = "user";
        public List<UiPart> Content { get; set; } = new();
    }

    public sealed class UiPart
    {
        public string Type { get; set; } = "text";
        public string? Text { get; set; }
    }
}

public static class OpenAiModels
{
    public sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("messages")]
        public List<OpenAiMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = true;

        [JsonPropertyName("tools")]
        public List<OpenAiTool>? Tools { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;
    }

    public sealed class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public object? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAiToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
    }

    public sealed class OpenAiTool
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAiFunction Function { get; set; } = new();
    }

    public sealed class OpenAiFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("parameters")]
        public JsonElement Parameters { get; set; }
    }

    public sealed class OpenAiToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAiToolCallFunction Function { get; set; } = new();
    }

    public sealed class OpenAiToolCallFunction
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "";
    }

    public sealed class StreamChunk
    {
        [JsonPropertyName("choices")]
        public List<StreamChoice> Choices { get; set; } = new();
    }

    public sealed class StreamChoice
    {
        [JsonPropertyName("delta")]
        public StreamDelta Delta { get; set; } = new();

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    public sealed class StreamDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<StreamToolCall>? ToolCalls { get; set; }
    }

    public sealed class StreamToolCall
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("function")]
        public StreamToolCallFunction? Function { get; set; }
    }

    public sealed class StreamToolCallFunction
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public string? Arguments { get; set; }
    }

    public sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatCompletionChoice> Choices { get; set; } = new();
    }

    public sealed class ChatCompletionChoice
    {
        [JsonPropertyName("message")]
        public ChatCompletionMessage Message { get; set; } = new();
    }

    public sealed class ChatCompletionMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}