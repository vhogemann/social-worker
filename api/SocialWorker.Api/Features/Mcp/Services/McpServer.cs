using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Mcp.Models;

namespace SocialWorker.Api.Features.Mcp.Services;

public sealed class McpServer
{
    private readonly McpToolRegistry _toolRegistry;
    private readonly McpResourceRegistry _resourceRegistry;

    public McpServer(McpToolRegistry toolRegistry, McpResourceRegistry resourceRegistry)
    {
        _toolRegistry = toolRegistry;
        _resourceRegistry = resourceRegistry;
    }

    public async Task<McpResponse?> HandleRequestAsync(McpRequest request, Guid userId, CancellationToken ct)
    {
        try
        {
            switch (request.Method)
            {
                case "initialize":
                    var initResult = new McpInitializeResult(
                        "2024-11-05",
                        new McpServerCapabilities(
                            new McpCapabilityTools(true),
                            new McpCapabilityResources(false, true),
                            new McpCapabilityPrompts(false)),
                        new McpServerInfo("SocialWorker", "1.0.0"));
                    return McpResponse.Success(request.Id, initResult);

                case "notifications/initialized":
                    return null; // Notifications do not send a response

                case "ping":
                    return McpResponse.Success(request.Id, new Dictionary<string, object>());

                case "tools/list":
                    var tools = _toolRegistry.ListTools();
                    return McpResponse.Success(request.Id, new { tools });

                case "tools/call":
                    if (!request.Params.HasValue)
                    {
                        return McpResponse.Fail(request.Id, -32602, "Invalid params: tools/call requires params object.");
                    }

                    var paramsEl = request.Params.Value;
                    if (!paramsEl.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
                    {
                        return McpResponse.Fail(request.Id, -32602, "Missing 'name' in tools/call params.");
                    }

                    var toolName = nameProp.GetString()!;
                    JsonElement? args = paramsEl.TryGetProperty("arguments", out var a) ? a : null;

                    var callResult = await _toolRegistry.ExecuteToolAsync(toolName, args, userId, ct);
                    return McpResponse.Success(request.Id, callResult);

                case "resources/list":
                    var resources = await _resourceRegistry.ListResourcesAsync(userId, ct);
                    return McpResponse.Success(request.Id, new { resources });

                case "resources/read":
                    if (!request.Params.HasValue || !request.Params.Value.TryGetProperty("uri", out var uriProp) || string.IsNullOrWhiteSpace(uriProp.GetString()))
                    {
                        return McpResponse.Fail(request.Id, -32602, "Missing 'uri' in resources/read params.");
                    }

                    var uri = uriProp.GetString()!;
                    var readResult = await _resourceRegistry.ReadResourceAsync(uri, userId, ct);
                    if (readResult == null)
                    {
                        return McpResponse.Fail(request.Id, -32002, $"Resource not found: {uri}");
                    }

                    return McpResponse.Success(request.Id, readResult);

                case "prompts/list":
                    var prompts = new List<McpPromptDefinition>
                    {
                        new("compose_thread", "Draft an engaging multi-post social media thread from research topic or sources.", new List<McpPromptArgument>
                        {
                            new("topic", "Topic or summary to compose", true),
                            new("platform", "Target platform (e.g. Bluesky)", false)
                        })
                    };
                    return McpResponse.Success(request.Id, new { prompts });

                default:
                    return McpResponse.Fail(request.Id, -32601, $"Method '{request.Method}' not found.");
            }
        }
        catch (Exception ex)
        {
            return McpResponse.Fail(request.Id, -32603, $"Internal server error: {ex.Message}");
        }
    }
}
