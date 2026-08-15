## Plan: LinkedIn Contract And Markdown Conversion [COMPLETE]

Define a stable LinkedIn publishing contract by separating concerns into three layers: internal Markdown thread model, normalized LinkedIn post model, and LinkedIn API payload model. MVP scope is text, link/article, and image posts; video is explicitly out of MVP but model-compatible for later addition.

**Steps**
1. Phase 1 - Lock scope and platform capabilities
- Confirm LinkedIn media support categories to model in-domain: NONE (text only), ARTICLE (URL share metadata), IMAGE (uploaded asset URN), and VIDEO (future).
- Freeze MVP to NONE + ARTICLE + IMAGE for this iteration.
- Define explicit exclusions for this pass: organization-page posting, carousels/documents, video publishing, and multi-post thread publishing to LinkedIn.
- Add explicit YouTube policy: YouTube links are treated as ARTICLE URL shares (link preview), not as embedded/native LinkedIn video.
- Require native LinkedIn video upload flow only when publishing actual video binaries (non-MVP).

2. Phase 2 - Define LinkedIn post domain contract (internal normalized shape)
- Introduce a normalized platform-agnostic-to-platform-boundary shape for one LinkedIn publication unit, derived from exactly one non-empty segment:
: author identity reference (member URN)
: body text
: visibility
: media kind enum {None, Article, Image, VideoFuture}
: media payload union
: source segment index and draft/thread ids for traceability.
- Add a hard input gate: LinkedIn publish accepts exactly one non-empty segment; 0 segments or more than 1 non-empty segment returns validation error (ThreadNotSupportedForLinkedIn).
- Include validation constraints in the contract design:
: max text length (3000)
: max media cardinality per MVP publish unit (1)
: disallow mixed media types in one unit
: disallow unresolved media:// references.
- Include an error taxonomy for mapping failures (UnsupportedCombination, MissingMediaAsset, MediaUploadFailed, InvalidSegmentLength, UnsupportedMvpFeature).

3. Phase 3 - Define LinkedIn API transport models (wire shape)
- Define typed request/response models for:
: UGC post create payload (author, lifecycleState, specificContent, visibility)
: article media fields (originalUrl/title/description)
: image register-upload request/response
: binary upload result handling
: post create response extraction (remote id from response header/body mapping).
- Define model transforms from normalized post contract -> LinkedIn wire payload with one-way deterministic mapping.

4. Phase 4 - Map Markdown representation to normalized LinkedIn contract
- Define deterministic Markdown parsing rules using existing segmenting and media patterns:
: split content by --- into segments
: require exactly one non-empty segment for LinkedIn publishing
: strip unsupported markdown formatting markers as already done by content policy normalization
: parse media:// GUID assets from markdown image tags
: parse URL link shares where no media:// is present and a primary URL exists.
- Define per-segment media classification rules:
: if media:// present -> Image
- Add YouTube-specific mapping rule: markdown YouTube links map to Article media with originalUrl set to the YouTube URL; do not map to Video media in MVP.
: else if canonical URL/article link present -> Article
: else -> None
: if both media:// and article URL are present in same segment -> invalid for MVP (explicit error).
- Define how alt text/title/description are sourced:
: prefer media asset alt text
: fallback to markdown image alt text
: fallback empty.

5. Phase 5 - Define conversion pipeline and execution boundaries
- Pipeline step A: thread content -> segments (existing split service).
- Pipeline step B: cardinality gate (exactly one non-empty segment) and fail-fast error if thread-like input is detected.
- Pipeline step C: segment normalization + policy validation (LinkedIn content policy).
- Pipeline step D: segment media analysis + asset lookup (existing shared patterns + media table).
- Pipeline step E: build normalized LinkedIn post contract.
- Pipeline step F: if Image, run registerUpload -> binary upload -> attach asset URN.
- Pipeline step G: build UGC payload and publish.
- Pipeline step H: map publish response into existing PublishedPost records.
- Keep each step pure and testable where possible, with side effects isolated to upload/publish API client steps.

6. Phase 6 - Integrate with existing publish orchestration points
- Keep IPublisher contract unchanged; LinkedInPublisher consumes normalized contract builder and LinkedIn API client.
- Route publish-time validation through existing platform policy plumbing so LinkedIn and Bluesky checks are platform-specific.
- Preserve existing Draft/PlatformThread/Post persistence flow and only add LinkedIn-specific mapping logic inside LinkedIn publishing feature slice.

7. Phase 7 - Verification-first test plan for contract mapping
- Add unit tests for Markdown -> normalized LinkedIn model classification:
: text only segment
: article segment
: image segment with media://
: invalid mixed media segment
: over-limit text segment
: invalid multi-segment input (thread separator present with 2+ non-empty segments).
- Add unit tests for normalized model -> LinkedIn payload shape correctness.
- Add publisher tests for upload+publish orchestration and publish result mapping.

**Relevant files**
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Drafts/Services/DraftSegmentService.cs - existing segment split behavior and segment media analysis entrypoints.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Infrastructure/SharedPatterns.cs - media:// extraction and markdown stripping rules to reuse in mapping.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Chat/Tools/LinkedInPlatformContentPolicy.cs - current LinkedIn constraints to tighten for final contract enforcement.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Chat/Tools/PlatformContentPolicyBase.cs - normalization and unsupported markdown handling reused before mapping.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Publishing/IPublisher.cs - publish contract boundary that remains stable.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Publishing/StubPublishers.cs - LinkedIn stub replacement point.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Publishing/PublishingEndpoint.cs - publish orchestration and platform validation routing.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Data/Entities/PlatformThread.cs - source thread content container for conversion input.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Data/Entities/ThreadSegment.cs - persisted segment model backing deterministic per-segment conversion.
- /home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Data/Entities/MediaAsset.cs - media metadata source for alt text, mime type, and file path.

**Verification**
1. Contract review checkpoint:
- Validate that every supported LinkedIn media category in MVP has one normalized model variant and one wire mapping.
2. Mapping determinism checkpoint:
- Same markdown input must always produce identical normalized LinkedIn contract output.
3. Policy checkpoint:
- LinkedIn content policy rejects all out-of-scope combinations before publish call.
4. Publish persistence checkpoint:
- Successful LinkedIn publish maps to existing PublishedPost fields without schema changes.

**Decisions**
- Included in MVP: text-only, URL/article share, single-image share.
- YouTube handling decision: publish as ARTICLE link shares (preview card behavior), not embedded playback posts.
- Native LinkedIn VIDEO posting remains out of MVP unless video binary upload pipeline is added.
- Excluded from MVP: video publish, organization/page publishing, and any thread-style multi-segment publish.
- LinkedIn cardinality rule: exactly one non-empty Markdown segment is required at publish time.
- Failure strategy: fail fast on unsupported combinations rather than silent coercion.

**Further Considerations**
1. Article detection heuristic
- Recommendation: prefer explicit markdown link priority over naked URL text to reduce false positives.
2. Video-ready contract shape
- Recommendation: keep VideoFuture enum/member in normalized model now to avoid a breaking type change later.

## Implementation Trackers

Use the per-phase TODO files in [planning/linkedin](planning/linkedin) to track active implementation progress across sessions.

- [planning/linkedin/README.md](planning/linkedin/README.md)
- [planning/linkedin/PHASE_1_SCOPE_AND_CAPABILITIES_TODO.md](planning/linkedin/PHASE_1_SCOPE_AND_CAPABILITIES_TODO.md)
- [planning/linkedin/PHASE_2_DOMAIN_CONTRACT_TODO.md](planning/linkedin/PHASE_2_DOMAIN_CONTRACT_TODO.md)
- [planning/linkedin/PHASE_3_WIRE_MODELS_TODO.md](planning/linkedin/PHASE_3_WIRE_MODELS_TODO.md)
- [planning/linkedin/PHASE_4_MARKDOWN_MAPPING_TODO.md](planning/linkedin/PHASE_4_MARKDOWN_MAPPING_TODO.md)
- [planning/linkedin/PHASE_5_CONVERSION_PIPELINE_TODO.md](planning/linkedin/PHASE_5_CONVERSION_PIPELINE_TODO.md)
- [planning/linkedin/PHASE_6_ORCHESTRATION_INTEGRATION_TODO.md](planning/linkedin/PHASE_6_ORCHESTRATION_INTEGRATION_TODO.md)
- [planning/linkedin/PHASE_7_VERIFICATION_AND_ROLLOUT_TODO.md](planning/linkedin/PHASE_7_VERIFICATION_AND_ROLLOUT_TODO.md)
