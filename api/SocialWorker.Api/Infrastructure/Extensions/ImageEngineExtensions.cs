using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Templates.Services;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddImageEngine(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<ImageEngineOptions>(builder.Configuration.GetSection(ImageEngineOptions.SectionName));

        builder.Services.AddHttpClient<IImageEngineClient, ImageEngineClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ImageEngineOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        builder.Services.AddScoped<TemplateGeneratorService>();
    }
}
