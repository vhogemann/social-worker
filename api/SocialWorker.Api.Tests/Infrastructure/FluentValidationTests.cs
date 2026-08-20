using System;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Drafts.Validators;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Feeds.Validators;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Media.Validators;
using SocialWorker.Api.Features.Providers;
using SocialWorker.Api.Features.Providers.Validators;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure;

public sealed class FluentValidationTests
{
    [Fact]
    public async Task CreateDraftRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new CreateDraftRequestValidator();
        var request = new CreateDraftRequest("Test Title", "Test Content", "Bluesky");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateDraftRequestValidator_InvalidTargetPlatform_FailsValidation()
    {
        var validator = new CreateDraftRequestValidator();
        var request = new CreateDraftRequest("Test Title", "Test Content", "InvalidPlatformName");

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TargetPlatform");
    }

    [Fact]
    public async Task DiscoverFeedRequestValidator_InvalidUrl_FailsValidation()
    {
        var validator = new DiscoverFeedRequestValidator();
        var request = new DiscoverFeedRequest("not-a-valid-url");

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Url");
    }

    [Fact]
    public async Task CreateFeedSubscriptionRequestValidator_EmptyTitleAndUrl_FailsValidation()
    {
        var validator = new CreateFeedSubscriptionRequestValidator();
        var request = new CreateFeedSubscriptionRequest("", "", null, null, null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public async Task ImportMediaFromUrlRequestValidator_ValidUrl_PassesValidation()
    {
        var validator = new ImportMediaFromUrlRequestValidator();
        var request = new ImportMediaFromUrlRequest("https://example.com/image.png", "Alt text");

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateProviderRequestValidator_ValidRequest_PassesValidation()
    {
        var validator = new CreateProviderRequestValidator();
        var request = new ProviderModels.CreateProviderRequest(
            "OpenRouter Main",
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "anthropic/claude-3.5-sonnet",
            "sk-12345",
            128000);

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task CreateProviderRequestValidator_InvalidProviderType_FailsValidation()
    {
        var validator = new CreateProviderRequestValidator();
        var request = new ProviderModels.CreateProviderRequest(
            "Unknown",
            "UnsupportedType",
            "https://example.com",
            "model",
            string.Empty,
            null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ProviderType");
    }

    [Fact]
    public async Task CreateAutomationWorkflowRequestValidator_EmptyTitle_FailsValidation()
    {
        var validator = new SocialWorker.Api.Features.Workflows.Validators.CreateAutomationWorkflowRequestValidator();
        var request = new SocialWorker.Api.Features.Workflows.CreateAutomationWorkflowRequest("", "Prompt", null, null);

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Title");
    }

    [Fact]
    public async Task CreateAutomationWorkflowRequestValidator_InvalidDestination_FailsValidation()
    {
        var validator = new SocialWorker.Api.Features.Workflows.Validators.CreateAutomationWorkflowRequestValidator();
        var request = new SocialWorker.Api.Features.Workflows.CreateAutomationWorkflowRequest(
            "Valid Title",
            "Prompt",
            null,
            new System.Collections.Generic.List<SocialWorker.Api.Features.Workflows.CreateWorkflowDestinationRequest>
            {
                new SocialWorker.Api.Features.Workflows.CreateWorkflowDestinationRequest("", true)
            });

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("Destinations"));
    }
}
