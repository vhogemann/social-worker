using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddChat(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection(ChatOptions.SectionName));
        builder.Services.AddScoped<DraftTitleGenerator>();
        builder.Services.AddScoped<ChatSessionLoader>();
        builder.Services.AddScoped<SystemPromptBuilder>();
        builder.Services.AddScoped<ChatStreamWriter>();
        builder.Services.AddScoped<ChatRequestPreparationService>();
        builder.Services.AddScoped<ChatToolExecutor>();
        builder.Services.AddScoped<ChatRoundProcessor>();
        builder.Services.AddScoped<ChatService>();
        builder.Services.AddScoped<IChatTool, ReplaceEditorContentTool>();
        builder.Services.AddScoped<IChatTool, ProposeStageTransitionTool>();
        builder.Services.AddScoped<IChatTool, ListSourcesTool>();
        builder.Services.AddScoped<IChatTool, FetchSourceTool>();
        builder.Services.AddScoped<IChatTool, ViewImageTool>();
        builder.Services.AddScoped<IChatTool, PublishPlatformTool>();
        builder.Services.AddScoped<IChatTool, WebSearchTool>();
        builder.Services.AddScoped<IChatTool, AddSourceTool>();
        builder.Services.AddScoped<IChatTool, ValidateDraftTool>();
        builder.Services.AddScoped<IChatTool, AddImageSourceTool>();
        builder.Services.AddScoped<IChatTool, ImageSearchTool>();
        builder.Services.AddScoped<IChatTool, RenderCodeBlocksTool>();
        builder.Services.AddScoped<IChatTool, FormatValidatePlatformContentTool>();
        builder.Services.AddScoped<IChatTool, GeneratePlatformVariantsTool>();
        builder.Services.AddScoped<IChatTool, SearchSourcesTool>();
        builder.Services.AddScoped<IChatTool, SetBlueskyReplyTargetTool>();
    }
}
