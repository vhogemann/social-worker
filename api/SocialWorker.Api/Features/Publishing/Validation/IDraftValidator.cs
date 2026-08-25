using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Features.Publishing.Validation;

public interface IDraftValidator
{
    ValidateDraftResult Validate(string content, IReadOnlyList<MediaAsset> mediaAssets);
}
