# Platform Variants: On-Demand Multi-Platform Adaptation

## Overview

Enable users to draft once on their primary platform, then intelligently adapt that draft to other social networks on-demand. Each variant is a **full, independent Draft** with platform-specific restructuring (not just text truncation).

## Key Insight

Same content requires different structure per platform:
- **Bluesky**: Multi-post thread (300 grapheme limit per post)
- **LinkedIn**: Single long-form article (professional tone, narrative)
- **Twitter**: Multi-post thread (280 char limit, punchy)
- **Instagram**: Visual-first, short caption (audience-aware tone)
- **Facebook**: Conversational, longer (no hard limit)

## Architecture

### Data Model Changes

**Add to Draft entity:**
```csharp
public SocialPlatform? TargetPlatform { get; set; }  // Bluesky, Twitter, LinkedIn, Facebook, Instagram
public Guid? CanonicalDraftId { get; set; }          // FK to canonical draft (null if canonical)
```

**New enum:**
```csharp
public enum SocialPlatform
{
    Bluesky,
    Twitter,
    LinkedIn,
    Facebook,
    Instagram
}
```

**EF Migration:**
- Add TargetPlatform column (nvarchar(50), nullable)
- Add CanonicalDraftId FK (nullable)
- Create index on CanonicalDraftId (for querying variants)
- Default existing drafts: TargetPlatform = "Bluesky"

**Relationships:**
- Draft → CanonicalDraft (one canonical, many variants)
- Variants share Source + MediaAsset rows (no duplication)

### Platform Constraints

| Platform | Text Limit | Posts/Thread | Format | Media | Audience |
|---|---|---|---|---|---|
| **Bluesky** | 300 graphemes | Multi (no limit) | Threaded | 1 image/post | Tech-savvy |
| **Twitter** | 280 chars | Multi (no hard limit) | Threaded | Up to 4 per post | Broad |
| **LinkedIn** | ~3000 chars | Single or multi | Long-form + professional | 1 recommended | Professional |
| **Facebook** | No hard limit | Single or multi | Conversational | Multiple | Broad age range |
| **Instagram** | 2200 chars caption | Single (carousel multi-image) | Visual-first | 1+ images | Visual/lifestyle |

---

## Phase 1: Draft Creation UI

### Create Draft Flow (Updated)

**Step 1: Name + Platform Selection**
```
Create New Draft

Title: _______________________

Target Platform: [Select]
  ○ Bluesky (I'll adapt to others later)
  ○ Twitter
  ○ LinkedIn
  ○ Facebook
  ○ Instagram

[Create Draft] [Cancel]
```

### Sidebar Display

Show platform badge next to draft name:
```
📝 My Thread                     [Bluesky]
📝 AI Policy Post                [LinkedIn]
📝 News Commentary               [Twitter]

(Click draft to open)
```

**If it's a variant, show badge + parent link:**
```
📝 My Thread → LinkedIn Variant  [LinkedIn] (variant of "My Thread")
```

---

## Phase 2: Backend Services

### New Service: `PlatformVariantService`

```csharp
public class PlatformVariantService
{
    // Generate variants for canonical draft
    public async Task<List<DraftDto>> GenerateVariantsAsync(
        Guid userId,
        Guid canonicalDraftId,
        List<SocialPlatform> targetPlatforms,
        CancellationToken ct)
    
    // Get canonical + all variants for a draft
    public async Task<DraftFamilyDto> GetDraftFamilyAsync(
        Guid userId,
        Guid draftId,
        CancellationToken ct)
    
    // Get variant by canonical + platform
    public async Task<DraftDto?> GetVariantAsync(
        Guid userId,
        Guid canonicalDraftId,
        SocialPlatform platform,
        CancellationToken ct)
}
```

**DTOs:**
```csharp
public record DraftFamilyDto(
    DraftDto Canonical,
    List<DraftDto> Variants  // variants[].TargetPlatform, CanonicalDraftId
);

public record GenerateVariantsRequest(
    List<SocialPlatform> Platforms  // [Twitter, LinkedIn, Instagram]
);
```

### LLM Tool: `generate_platform_variants`

**Purpose:** Intelligently restructure canonical draft for target platforms

**Behavior:**
```
Tool: generate_platform_variants
Input: {
  "canonicalDraftId": "<uuid>",
  "platforms": ["Twitter", "LinkedIn", "Instagram"]
}

Process:
1. Fetch canonical draft + content
2. For each platform:
   a. Call LLM: "Adapt this Bluesky thread to Twitter (280 char limit, 2-3 posts)"
   b. LLM returns adapted content
   c. System creates Draft { Content=adapted, TargetPlatform="Twitter", CanonicalDraftId=canonical }
3. Return all created variants

Output: {
  "canonicalId": "<uuid>",
  "variants": [
    { "id": "<uuid>", "platform": "Twitter", "status": "Draft", "segmentCount": 3 },
    { "id": "<uuid>", "platform": "LinkedIn", "status": "Draft", "segmentCount": 1 },
    { "id": "<uuid>", "platform": "Instagram", "status": "Draft", "segmentCount": 1 }
  ]
}
```

**LLM Context (in system prompt):**
```
When adapting content for platforms, follow these rules:

TWITTER (280 chars/post, 2-3 posts typical):
- Punchy, conversational tone
- Break into short posts, each standalone
- Use hashtags sparingly (max 2)
- Reply threads: connect posts logically

LINKEDIN (3000 chars, 1-2 posts):
- Professional tone
- Single long-form post or 2-part series
- Emojis used strategically
- Call-to-action at end

INSTAGRAM (2200 char caption, visual-first):
- Lifestyle/visual tone, relatable
- Shorter sentences, more emojis
- Hashtags at end (5-10)
- Focus on visual story, not just text
- Assume user can see images

FACEBOOK (no hard limit, conversational):
- Friendly, engaging tone
- Slightly longer form than Twitter
- Multi-generational audience (simpler language)
- Emojis welcome, moderate use

Always preserve:
- Core message/meaning
- Key facts and links
- Source citations
- Code blocks (if applicable)
```

**Tool Registration:**
```csharp
builder.Services.AddScoped<IChatTool, GeneratePlatformVariantsTool>();
```

### New Endpoints

#### 1. Create Draft (Updated)
```
POST /api/drafts
Request: {
  "title": "My Thread",
  "content": "# My Thread\n\nHere's my take...",
  "targetPlatform": "Bluesky"  // NEW
}
Response: DraftDto { Id, Title, TargetPlatform, CanonicalDraftId }
```

#### 2. Generate Variants
```
POST /api/drafts/{canonicalDraftId}/generate-variants
Request: {
  "platforms": ["Twitter", "LinkedIn", "Instagram"]
}
Response: {
  "canonicalId": "<uuid>",
  "variants": [
    { "id": "<uuid>", "platform": "Twitter", "segmentCount": 3 },
    ...
  ]
}
```

#### 3. Get Draft Family
```
GET /api/drafts/{draftId}/family
Response: DraftFamilyDto {
  Canonical: { ... },
  Variants: [ { platform: "Twitter", ... }, ... ]
}
```

#### 4. Get Variant
```
GET /api/drafts/canonical/{canonicalDraftId}/variant/{platform}
Response: DraftDto { CanonicalDraftId, TargetPlatform }
```

### Updated Publishing Endpoint

```
POST /api/drafts/{draftId}/publish
Request: {
  "platforms": ["Bluesky"]  // NEW: select platforms (or publish all variants)
}
Response: { "threadIds": [...], "status": "published" }
```

**Behavior:**
- If draft has CanonicalDraftId (is variant): Publish only to its TargetPlatform
- If draft is canonical (CanonicalDraftId=null): Publish to requested platforms OR publish all variants

---

## Phase 3: Frontend UI

### Draft List Sidebar (Updated)

```
📋 Drafts
─────────────────────
📝 AI Policy Post              [Bluesky]
   ├─ LinkedIn Variant         [LinkedIn]
   └─ Twitter Variant          [Twitter]

📝 News Commentary             [LinkedIn]

📝 Tech Thread                 [Bluesky]
   └─ Facebook Adaptation      [Facebook]
```

**Interactions:**
- Click draft: Open editor (shows its content)
- Right-click variant: Options (delete variant, swap canonical, etc.)
- Drag variant: Reorder within family (UI only, cosmetic)

### Stage Stepper (Updated)

When canonical is at "Ready" stage:

```
┌─────────────────────────────────┐
│ Draft  →  Ready  →  Sent        │
│ (editing)  ✓ ready              │
└─────────────────────────────────┘

[Adapt to Other Platforms]  ← NEW BUTTON
```

On click:
```
┌────────────────────────────────────┐
│ Adapt to Other Platforms           │
├────────────────────────────────────┤
│ ☐ Twitter                          │
│ ☐ LinkedIn                         │
│ ☐ Facebook                         │
│ ☐ Instagram                        │
│                                    │
│ [Generate Variants] [Cancel]       │
└────────────────────────────────────┘
```

On "Generate Variants":
- Shows spinner: "Creating variants..."
- Creates new drafts in background
- On success: Shows list of created variants with links
- User can click each to review/edit

### Variant Switcher in Editor

When editing a variant draft:

```
┌─ My AI Policy Post (Canonical)
├─ Twitter Variant        ← (currently editing)
├─ LinkedIn Variant
└─ Facebook Adaptation

Editing: Twitter Variant
Platform: [Twitter Badge]
Canonical: My AI Policy Post [Link to canonical]

[Edit] [Publish to Twitter] [Delete Variant]
```

### Variant Preview Panel (Optional v1.1)

Show all variants side-by-side (tabs):

```
Canonical    │    Twitter    │    LinkedIn    │    Facebook
──────────────────────────────────────────────────────────────
[Content]    │   [Content]   │   [Content]    │   [Content]
             │               │                │
```

---

## Phase 4: Publishing Workflow

### Before: Simple Linear
```
Draft → Ready → Publish → Published (to all platforms at once)
```

### After: Platform-Aware
```
Canonical Draft (Bluesky)
    ↓ (Ready)
    ├─ Generate Variants → [Twitter Draft, LinkedIn Draft, ...]
    ├─ Twitter Draft → Ready → Publish → Twitter
    ├─ LinkedIn Draft → Ready → Publish → LinkedIn
    └─ Facebook Draft → Ready → Publish → Facebook

Result: 4 PlatformThread rows (one per platform)
```

**Each variant publishes independently:**
- User decides which variants to publish
- Publish timing can differ per variant
- Each creates a PlatformThread linked to its Draft

---

## Phase 5: Data Integrity

### Variant Lifecycle

**Created:**
- CanonicalDraftId = canonical UUID
- TargetPlatform = Twitter (or other)
- Status = Draft
- ChatHistory = empty (user chats independently per variant)

**Edited:**
- User can edit variant content independently
- Edits don't affect canonical or other variants
- User can re-generate variants (overwrites old ones? or creates new?)

**Deleted:**
- Deleting variant: Only deletes that variant Draft
- Deleting canonical: Should warn "This will delete X variants too"
- Cascading delete or manual confirmation?

### Sources & Media Sharing

- Canonical has Sources [S1, S2, S3]
- All variants reference same Sources
- No duplication (efficient, aligns with v2 Sources Library design)
- MediaAssets: Same (images reused per platform)
- When user adds a Source to canonical, visible to all variants
- When user adds a Source to a variant, visible to all variants
- **Constraint:** If variant is deleted, don't delete shared sources (other variants might need them)
- **v2 Connection:** Sources Library (v2) extends this pattern to multi-draft sharing globally

---

## Phase 6: Tests

### Backend Unit Tests

- `PlatformVariantService.GenerateVariantsAsync()`: Creates drafts with correct platform + canonical link
- `GeneratePlatformVariantsTool.ExecuteAsync()`: LLM tool creates variants, returns correct format
- Draft creation: TargetPlatform persists to DB
- Variant queries: GetVariantAsync filters by canonical + platform correctly
- Publishing: Variant publishes to correct platform only

### Integration Tests

- Full flow: Create canonical → Generate 3 variants → Review each → Publish all → Verify 4 PlatformThread rows
- Variant editing: Edit Twitter variant, verify canonical + LinkedIn unaffected
- Cascade deletes: Delete canonical, verify variants also deleted (if implemented)

### Target: 10+ tests

---

## Implementation Order

1. **Database**: EF migration (TargetPlatform, CanonicalDraftId columns + index)
2. **Backend services**: PlatformVariantService + endpoints (CRUD)
3. **LLM tool**: GeneratePlatformVariantsTool + system prompt context
4. **Updated publishing**: Endpoint respects TargetPlatform when publishing
5. **Frontend**: Draft creation with platform selector
6. **Frontend**: Stage stepper "Adapt to Other Platforms" button
7. **Frontend**: Sidebar variant display + variant switcher
8. **E2E testing**: Full workflow from creation → variants → publishing

---

## Verification Checklist

### Backend
- [ ] EF migration creates columns + index
- [ ] Draft.TargetPlatform defaults to null (or Bluesky)
- [ ] Draft.CanonicalDraftId FK works correctly
- [ ] PlatformVariantService.GenerateVariantsAsync creates N drafts
- [ ] Variants share Sources/MediaAssets (no duplication)
- [ ] Publishing respects TargetPlatform
- [ ] GetDraftFamily returns canonical + all variants
- [ ] GeneratePlatformVariantsTool LLM integration works
- [ ] 10+ unit/integration tests passing

### Frontend
- [ ] Create Draft: Platform selector appears + saves to DB
- [ ] Draft sidebar: Platform badges display
- [ ] Variant sidebar: Shows nested variants + parent link
- [ ] Stage Stepper: "Adapt to Other Platforms" button at Ready stage
- [ ] Variant modal: Select platforms → Generate → Shows created variants
- [ ] Variant switcher: Edit button navigates to variant, shows platform badge
- [ ] Publish: Each variant publishes to its platform only
- [ ] No TypeScript errors or circular imports

### E2E Workflow
- [ ] Create Draft (Bluesky) with content
- [ ] Advance to Ready stage
- [ ] Click "Adapt to Other Platforms"
- [ ] Select Twitter + LinkedIn + Instagram
- [ ] Verify 3 new Drafts created with correct platforms
- [ ] Edit each variant independently
- [ ] Publish each variant
- [ ] Verify 4 PlatformThread rows created (1 canonical + 3 variants)
- [ ] Verify each publishes to correct platform

---

## Scope Boundaries

### In Scope (v1)
- ✅ Draft creation with platform selection
- ✅ On-demand variant generation via LLM tool
- ✅ Variant independence (edit separately)
- ✅ Shared sources/media (no duplication)
- ✅ Platform-aware publishing
- ✅ Sidebar variant display
- ✅ Variant switcher in editor

### Out of Scope (v1.1+)
- ❌ Auto-sync canonical → variants on edit
- ❌ Variant preview side-by-side
- ❌ Template-based variants (manually craft variant structure)
- ❌ A/B testing (publish variant, measure engagement)
- ❌ Scheduled multi-platform publishing
- ❌ Variant versioning / history

---

## Notes

- **LLM intelligence**: Quality of variants depends entirely on LLM capability and system prompt context
- **Platform constraints**: Constraints are enforced at validation time (similar to existing `validate_draft` tool)
- **Manual editing**: Users should always be able to manually tweak variants (not locked to LLM output)
- **Cascade concerns**: Deleting canonical while variants exist should warn and ask for confirmation
- **Reusability**: Variants can be re-generated if canonical changes (overwrites or creates new?)
- **Chat history**: Each variant has independent chat (users can iterate each variant separately)
