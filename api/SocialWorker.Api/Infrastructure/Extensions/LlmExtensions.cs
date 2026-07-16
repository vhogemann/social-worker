using SocialWorker.Api.Features.Providers.Services;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddLlm(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));
        builder.Services.AddHttpClient<OpenAiProviderAdapter>();
        builder.Services.AddScoped<ILlmProviderAdapter>(sp =>
        {
            var demoProfile = Environment.GetEnvironmentVariable("DEMO_LLM_PROFILE");
            if (!string.IsNullOrWhiteSpace(demoProfile))
            {
                return new DemoLlmAdapter();
            }
            return sp.GetRequiredService<OpenAiProviderAdapter>();
        });
        builder.Services.AddHttpClient<ModelCapabilityProbe>();
        builder.Services.AddScoped<LlmProviderService>();
    }
}
