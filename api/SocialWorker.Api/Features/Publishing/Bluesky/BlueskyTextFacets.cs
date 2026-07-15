using System.Collections.Generic;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed record BlueskyTextFacets(string PlainText, List<BlueskyFacet> Facets);
