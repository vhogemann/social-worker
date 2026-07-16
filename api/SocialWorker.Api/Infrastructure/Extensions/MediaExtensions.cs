using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddMedia(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ImageResizer>();
        builder.Services.AddSingleton<FileStorageProvider>();
        builder.Services.AddScoped<MediaService>();
        builder.Services.AddSingleton<CodeImageRenderer>();
        builder.Services.AddScoped<CodeImageService>();
    }
}
