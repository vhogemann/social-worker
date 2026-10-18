using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using SocialWorker.Api.Features.Chat.Models;

namespace SocialWorker.Api.Features.Chat.Services;

public static class TextToolCallParser
{
    private static readonly Regex ToolCallBlockRegex = new(
        @"<tool_call>(.*?)</tool_call>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex ArgKeyValueRegex = new(
        @"<arg_key>(.*?)</arg_key>\s*<arg_value>(.*?)</arg_value>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex ToolNamePrefixRegex = new(
        @"^([a-zA-Z0-9_\-\.]+)",
        RegexOptions.Compiled);

    private static readonly Regex DsmlCallsBlockRegex = new(
        @"<[\uFF5C|]DSML[\uFF5C|]\s*calls>(.*?)</[\uFF5C|]DSML[\uFF5C|]\s*calls>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex DsmlInvokeRegex = new(
        @"<[\uFF5C|]DSML[\uFF5C|]\s*invoke\s+name=""([^""]+)""[^>]*>(.*?)</[\uFF5C|]DSML[\uFF5C|]\s*invoke>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex DsmlParamRegex = new(
        @"<[\uFF5C|]DSML[\uFF5C|]\s*parameter\s+name=""([^""]+)""[^>]*>(.*?)</[\uFF5C|]DSML[\uFF5C|]\s*parameter>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public static bool ContainsToolCallSyntax(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("<tool_call", StringComparison.OrdinalIgnoreCase)
            || text.Contains("</tool_call>", StringComparison.OrdinalIgnoreCase)
            || text.Contains("<arg_key>", StringComparison.OrdinalIgnoreCase)
            || text.Contains("<arg_value>", StringComparison.OrdinalIgnoreCase)
            || text.Contains("DSML", StringComparison.OrdinalIgnoreCase)
            || text.Contains("<｜tool", StringComparison.OrdinalIgnoreCase)
            || text.Contains("<|tool", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tool_calls", StringComparison.OrdinalIgnoreCase);
    }

    public static List<OpenAiModels.OpenAiToolCall> ParseToolCalls(string? text)
    {
        var result = new List<OpenAiModels.OpenAiToolCall>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var matches = ToolCallBlockRegex.Matches(text);
        foreach (Match match in matches)
        {
            var inner = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(inner)) continue;

            if (inner.StartsWith('{') && inner.EndsWith('}'))
            {
                try
                {
                    using var doc = JsonDocument.Parse(inner);
                    var root = doc.RootElement;
                    string? name = null;
                    if (root.TryGetProperty("name", out var nameProp))
                    {
                        name = nameProp.GetString();
                    }
                    else if (root.TryGetProperty("function", out var fnProp) && fnProp.TryGetProperty("name", out var fnNameProp))
                    {
                        name = fnNameProp.GetString();
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string args = "{}";
                        if (root.TryGetProperty("arguments", out var argsProp))
                        {
                            args = argsProp.ValueKind == JsonValueKind.String
                                ? argsProp.GetString() ?? "{}"
                                : argsProp.GetRawText();
                        }
                        else if (root.TryGetProperty("parameters", out var paramsProp))
                        {
                            args = paramsProp.ValueKind == JsonValueKind.String
                                ? paramsProp.GetString() ?? "{}"
                                : paramsProp.GetRawText();
                        }

                        result.Add(new OpenAiModels.OpenAiToolCall
                        {
                            Id = $"call_{Guid.NewGuid():N}",
                            Type = "function",
                            Function = new OpenAiModels.OpenAiToolCallFunction
                            {
                                Name = name,
                                Arguments = args
                            }
                        });
                        continue;
                    }
                }
                catch (JsonException)
                {
                }
            }

            var nameMatch = ToolNamePrefixRegex.Match(inner);
            if (nameMatch.Success)
            {
                var toolName = nameMatch.Groups[1].Value.Trim();
                var remaining = inner.Substring(nameMatch.Length).Trim();

                var argMatches = ArgKeyValueRegex.Matches(remaining);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match am in argMatches)
                {
                    var key = am.Groups[1].Value.Trim();
                    var val = am.Groups[2].Value.Trim();
                    dict[key] = val;
                }

                var argsJson = JsonSerializer.Serialize(dict);
                result.Add(new OpenAiModels.OpenAiToolCall
                {
                    Id = $"call_{Guid.NewGuid():N}",
                    Type = "function",
                    Function = new OpenAiModels.OpenAiToolCallFunction
                    {
                        Name = toolName,
                        Arguments = argsJson
                    }
                });
            }
        }

        var dsmlMatches = DsmlInvokeRegex.Matches(text);
        foreach (Match dm in dsmlMatches)
        {
            var toolName = dm.Groups[1].Value.Trim();
            var inner = dm.Groups[2].Value.Trim();

            var paramMatches = DsmlParamRegex.Matches(inner);
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (Match pm in paramMatches)
            {
                var paramName = pm.Groups[1].Value.Trim();
                var paramValue = pm.Groups[2].Value.Trim();
                if (int.TryParse(paramValue, out var intVal))
                {
                    dict[paramName] = intVal;
                }
                else if (bool.TryParse(paramValue, out var boolVal))
                {
                    dict[paramName] = boolVal;
                }
                else
                {
                    dict[paramName] = paramValue;
                }
            }

            result.Add(new OpenAiModels.OpenAiToolCall
            {
                Id = $"call_{Guid.NewGuid():N}",
                Type = "function",
                Function = new OpenAiModels.OpenAiToolCallFunction
                {
                    Name = toolName,
                    Arguments = JsonSerializer.Serialize(dict)
                }
            });
        }

        return result;
    }

    public static string StripToolCalls(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var stripped = ToolCallBlockRegex.Replace(text, string.Empty);
        stripped = DsmlCallsBlockRegex.Replace(stripped, string.Empty);
        stripped = Regex.Replace(stripped, @"<[\uFF5C|]DSML[\uFF5C|].*?</[\uFF5C|]DSML[\uFF5C|]\s*[^>]*>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        stripped = Regex.Replace(stripped, @"<[\uFF5C|][^>]+[\uFF5C|]>", string.Empty, RegexOptions.IgnoreCase);
        return stripped.Trim();
    }
}
