using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Chat.Models;

public sealed record ChatSessionContext(
    LlmProvider Provider,
    LlmCredentials Credentials,
    ModelCapabilities Capabilities,
    Draft Draft,
    string EditorContent,
    List<MediaAsset> MediaAssets,
    string? DefaultBrandVoiceBody = null);
