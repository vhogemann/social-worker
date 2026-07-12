using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure.Llm;

public sealed class ModelCapabilityProbeTests
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly NullLogger<ModelCapabilityProbe> _log = NullLogger<ModelCapabilityProbe>.Instance;

    private ModelCapabilityProbe CreateProbe() => new(null!, _cache, _log);

    [Fact]
    public async Task OpenAI_Gpt4o_SupportsVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenAI", Model = "gpt-4o" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        Assert.True(caps.SupportsVision);
        Assert.True(caps.SupportsTools);
    }

    [Fact]
    public async Task OpenAI_Gpt4oMini_SupportsVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenAI", Model = "gpt-4o-mini" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        Assert.True(caps.SupportsVision);
    }

    [Fact]
    public async Task OpenAI_Gpt4_NoVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenAI", Model = "gpt-4" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        Assert.False(caps.SupportsVision);
    }

    [Fact]
    public async Task O1_SupportsVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenAI", Model = "o1-preview" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        Assert.True(caps.SupportsVision);
    }

    [Fact]
    public async Task CachesResult()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenAI", Model = "gpt-4o" };

        var first = await probe.GetCapabilitiesAsync(provider);
        var second = await probe.GetCapabilitiesAsync(provider);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task UnknownProvider_ReturnsNoCapabilities()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "Unknown", Model = "some-model" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        Assert.False(caps.SupportsVision);
        Assert.False(caps.SupportsTools);
    }

    [Fact]
    public async Task OpenRouter_Claude3_DetectedAsVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "OpenRouter", Model = "anthropic/claude-3-opus" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        // Falls back to heuristics since HTTP won't respond
        Assert.True(caps.SupportsVision);
    }

    [Fact]
    public async Task Ollama_Llama3_NoVision()
    {
        var probe = CreateProbe();
        var provider = new LlmProvider { ProviderType = "Ollama", Model = "llama3.1" };
        var caps = await probe.GetCapabilitiesAsync(provider);
        // Falls back to heuristics, llama is excluded
        Assert.False(caps.SupportsVision);
    }
}