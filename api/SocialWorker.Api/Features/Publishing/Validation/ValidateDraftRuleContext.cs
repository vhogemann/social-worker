using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing.Validation;

public sealed record ValidateDraftRuleContext<TSegment>(
    int PostNumber,
    TSegment Segment,
    IReadOnlyList<MediaAsset> MediaAssets);