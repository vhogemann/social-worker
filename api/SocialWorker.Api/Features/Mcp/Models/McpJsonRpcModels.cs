using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Mcp.Models;

public sealed record McpRequest(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] object? Id,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("params")] JsonElement? Params);

public sealed record McpResponse(
    [property: JsonPropertyName("jsonrpc")] string JsonRpc,
    [property: JsonPropertyName("id")] object? Id,
    [property: JsonPropertyName("result"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Result,
    [property: JsonPropertyName("error"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] McpError? Error)
{
    public static McpResponse Success(object? id, object? result) =>
        new("2.0", id, result, null);

    public static McpResponse Fail(object? id, int code, string message, object? data = null) =>
        new("2.0", id, null, new McpError(code, message, data));
}

public sealed record McpError(
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data = null);

public sealed record McpInitializeResult(
    [property: JsonPropertyName("protocolVersion")] string ProtocolVersion,
    [property: JsonPropertyName("capabilities")] McpServerCapabilities Capabilities,
    [property: JsonPropertyName("serverInfo")] McpServerInfo ServerInfo);

public sealed record McpServerCapabilities(
    [property: JsonPropertyName("tools"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] McpCapabilityTools? Tools = null,
    [property: JsonPropertyName("resources"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] McpCapabilityResources? Resources = null,
    [property: JsonPropertyName("prompts"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] McpCapabilityPrompts? Prompts = null);

public sealed record McpCapabilityTools([property: JsonPropertyName("listChanged")] bool ListChanged = false);
public sealed record McpCapabilityResources([property: JsonPropertyName("subscribe")] bool Subscribe = false, [property: JsonPropertyName("listChanged")] bool ListChanged = false);
public sealed record McpCapabilityPrompts([property: JsonPropertyName("listChanged")] bool ListChanged = false);

public sealed record McpServerInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version);

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);

public sealed record McpToolCallResult(
    [property: JsonPropertyName("content")] List<McpContentPart> Content,
    [property: JsonPropertyName("isError")] bool IsError = false);

public sealed record McpContentPart(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonPropertyName("data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Data = null,
    [property: JsonPropertyName("mimeType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MimeType = null)
{
    public static McpContentPart AsText(string text) => new("text", Text: text);
    public static McpContentPart AsImage(string base64Data, string mimeType) => new("image", Data: base64Data, MimeType: mimeType);
}

public sealed record McpResourceDefinition(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonPropertyName("mimeType")] string? MimeType = "text/plain");

public sealed record McpResourceReadResult(
    [property: JsonPropertyName("contents")] List<McpResourceContent> Contents);

public sealed record McpResourceContent(
    [property: JsonPropertyName("uri")] string Uri,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null,
    [property: JsonPropertyName("blob"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Blob = null)
{
    public static McpResourceContent AsText(string uri, string mimeType, string text) => new(uri, mimeType, Text: text);
    public static McpResourceContent AsBlob(string uri, string mimeType, string blob) => new(uri, mimeType, Blob: blob);
}

public sealed record McpPromptDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonPropertyName("arguments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<McpPromptArgument>? Arguments = null);

public sealed record McpPromptArgument(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonPropertyName("required")] bool Required = false);
