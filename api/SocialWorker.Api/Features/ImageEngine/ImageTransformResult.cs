namespace SocialWorker.Api.Features.ImageEngine;

public sealed record ImageTransformResult(
    byte[] Data,
    int Width,
    int Height,
    string MimeType);
