namespace SocialWorker.Api.Features.ImageEngine;

public sealed class ImageEngineOptions
{
    public const string SectionName = "ImageEngine";

    public string BaseUrl { get; set; } = "http://localhost:8103";
    public int TimeoutSeconds { get; set; } = 30;
}
