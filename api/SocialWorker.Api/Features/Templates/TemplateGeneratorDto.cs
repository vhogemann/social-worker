using System;
using System.Collections.Generic;

namespace SocialWorker.Api.Features.Templates;

public sealed record GenerateTemplateApiRequest(string Prompt, int? Width = null, int? Height = null);

public sealed record GeneratedTemplateResponse(
    string Name,
    string? Description,
    int Width,
    int Height,
    string Schema,
    string? PreviewBase64);

public sealed record PreviewTemplateApiRequest(
    string Schema,
    Dictionary<string, string>? Slots = null);
