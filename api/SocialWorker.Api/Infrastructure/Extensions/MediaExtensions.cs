using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Media.Services;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddMedia(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<FileStorageProvider>();
        builder.Services.AddScoped<MediaService>();
        builder.Services.AddSingleton<CodeImageRenderer>();
        builder.Services.AddScoped<CodeImageService>();

        builder.Services.AddHttpClient<IImageGenerationClient, OpenRouterImageClient>(client =>
        {
            client.BaseAddress = new Uri("https://openrouter.ai");
            client.Timeout = TimeSpan.FromSeconds(120);
        });

        builder.Services.AddScoped<ImageGenerationService>();
    }
}
