using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class OpenAiProviderAdapterTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };
        }
    }

    [Fact]
    public async Task SerializedRequest_DoesNotContainNullProperties()
    {
        var mockHandler = new MockHttpMessageHandler();
        using var httpClient = new HttpClient(mockHandler);
        var logger = NullLogger<OpenAiProviderAdapter>.Instance;
        var adapter = new OpenAiProviderAdapter(httpClient, logger);

        var request = new OpenAiModels.ChatCompletionRequest
        {
            Model = "test-model",
            Messages = new List<OpenAiModels.OpenAiMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new object[]
                    {
                        new { type = "text", text = "hello", image_url = (object?)null },
                        new { type = "image_url", image_url = new { url = "data:image/jpeg;base64,abc" }, text = (string?)null }
                    },
                    ToolCalls = null,
                    ToolCallId = null
                }
            },
            Tools = null
        };

        var credentials = new LlmCredentials(
            "https://api.openai.com/v1",
            "sk-test",
            "test-model"
        );

        await adapter.CompleteAsync(request, credentials, CancellationToken.None);

        Assert.NotNull(mockHandler.LastRequestBody);
        var requestJson = mockHandler.LastRequestBody;

        Assert.DoesNotContain("\"image_url\":null", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"text\":null", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"tool_calls\"", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"tool_call_id\"", requestJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"tools\"", requestJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serializes_ViewImageResultItem_Correctly()
    {
        var items = new List<ViewImageResultItem>
        {
            new ViewImageResultItem("text", "hello", null),
            new ViewImageResultItem("image_url", null, new ViewImageResultImageUrl("data:image/jpeg;base64,abc"))
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(items, options);

        Assert.DoesNotContain("\"image_url\":null", json);
        Assert.DoesNotContain("\"text\":null", json);
    }
}
