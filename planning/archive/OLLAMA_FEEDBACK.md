# Ollama Feedback on Tool Descriptions

Evaluation of tool metadata using model `gemma-e4b-32k`.

## list_sources
- **Current Description**: `List all sources attached to the active draft (e.g. text notes or URLs parsed from the text).`
- **Parameters**: `None`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `list_sources` is to retrieve a comprehensive list of all source materials that have been incorporated into or referenced by the current working document (the "active draft"). These sources are not just citations; they include various types of input data such as external URLs, internal text notes, or other parsed content.

**When to Call:** This tool should be called when:
*   The user needs to verify the provenance or origin of information within the draft.
*   The user is reviewing the draft and needs a complete list of all supporting materials for citation purposes.
*   The system needs to summarize *what* inputs were used to generate the current content.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Missing Details/Unclear Behaviors:**

1. **Output Format (Crucial):** The description does not specify what the output will look like. Does it return a list of strings (titles only)? Or does it return structured objects containing the source type, the actual content snippet, and the full URL/reference? *A developer needs to know if they are getting raw data or summarized metadata.*
2. **Scope Confirmation:** While "active draft" implies context, it doesn't explicitly state how the tool determines which draft is active (e.g., does it use a session ID, or is it always the most recently edited document?).

**Confusing Words:**

*   The description is generally clear, but "attached to the active draft" is slightly technical jargon. While understandable in context, simplifying this phrase would improve readability for non-technical users/LLMs.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
Retrieves a structured list of all source materials used or referenced within the current draft. This includes external URLs, uploaded notes, and other parsed content inputs.

---

## fetch_source
- **Current Description**: `Fetch the cached text content of a specific source by Guid ID. Returns a JSON object with: id, kind (Url/YouTube/File), reference (URL/filename), title, content (full text or video transcript), processingStatus (Pending/Processing/Complete/Failed), and formatting helper fields. If processingStatus is Pending or Processing, the content is not yet populated.`
- **Parameters**: `id (string Guid)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `fetch_source` is to retrieve the cached textual content (such as full text or video transcripts) associated with a specific source identified by its unique Guid ID. It acts as an information retrieval mechanism for data that has already been processed or is currently being tracked for processing.

**When to Call:** This tool should be called when:
1. The user needs the actual textual content from a known source (e.g., "What did this video say?" or "Give me the article text").
2. You have the unique Guid ID of that specific source.
3. **Crucially:** You must be prepared to handle cases where the `processingStatus` is `Pending` or `Processing`, as the returned content may not yet be populated and will require a follow-up call later.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Unclear Behaviors/Missing Details:**
* **Handling of Failure:** The description lists `Failed` as a possible status, but it does not specify what the tool returns (or if it throws an error) when the source processing has failed. This is a critical piece of missing information for robust calling logic.
* **Definition of "Cached":** While implied, it should be explicitly stated that this data might not be real-time and relies on previous background processing.

**Confusing Words:**
* **"Formatting helper fields":** This phrase is vague and adds no value to the user understanding the tool's function. It should be removed or generalized in the final description.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Use this tool to retrieve the cached textual content (transcripts, articles, etc.) for a source identified by its unique Guid ID. Note that if the `processingStatus` is not 'Complete', the returned content may be empty or incomplete.

---

## search_sources
- **Current Description**: `Search the user's source library for existing sources by keyword. Returns sources NOT already linked to the active draft. Use this to find relevant sources before adding new ones, to avoid duplication.`
- **Parameters**: `query (string), limit (integer)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `search_sources` is to allow the system to search a user's pre-existing library of source materials using specific keywords (`query`). Its critical function is not just searching, but **filtering out any sources that are already attached or cited in the current document/draft**, thereby preventing the model from suggesting redundant information.

**When to Call:** This tool should be called when:
1. The user needs supporting evidence or background information for a draft.
2. The system has identified a topic area (the `query`) but does not know which specific source to use.
3. It must be called *before* the model attempts to suggest adding new sources, as its primary value is preventing duplication and guiding the user toward existing knowledge.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Clarity/Behavior:** The description is generally very clear about its core function (searching *and* filtering). However, a few points could be improved:

*   **Ambiguity of "Linked":** While the intent is clear, the term "linked" might be ambiguous. Does it mean cited in the text, attached to the document metadata, or both? Clarifying this would improve developer understanding.
*   **Missing Error/Empty State Handling:** The description does not specify what happens if no sources are found (e.g., does it return an empty list `[]`, or does it raise a specific "No Sources Found" error?). This is crucial for robust API usage.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
Search your source library using keywords to find relevant materials. This function automatically filters out any sources already cited in the active draft, ensuring suggestions are unique and non-redundant.

---

## add_source
- **Current Description**: `Add a source to this draft. Either provide a new URL/YouTube/File reference, or pass an existing sourceId (from search_sources) to link an existing source without duplicating it.`
- **Parameters**: `source_id (string optional), kind (string optional), reference (string optional), title (string optional), content (string optional)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The primary purpose of `add_source` is to incorporate external supporting material or citations into a document draft. It acts as a centralized mechanism for managing source references.

**When to Call:** This tool should be called whenever the user needs to:
1. **Cite new information:** When the user has a specific URL, file, YouTube link, or piece of content they want to include and attribute in the current draft.
2. **Link existing sources:** When the user wants to reference material that was previously found or stored using another tool (like `search_sources`), ensuring the source is linked without duplicating its data.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Unclear Behaviors/Missing Details:**
1. **Parameter Overlap:** The relationship between the parameters (`reference`, `kind`, `content`) when adding a *new* source is not clearly defined. For example, if I provide a URL, should I populate `reference` with the URL and `kind` with 'URL', or are these fields redundant?
2. **Scope of "Draft":** The description assumes the existence of a "draft" context. It would be helpful to clarify whether the tool operates on an implicit draft state or if the target document/draft ID must be passed as an argument.

**Confusing Words:**
* **"Pass an existing sourceId (from search_sources)":** While functional, this phrasing is slightly clunky. It implies a dependency that should ideally be handled by clearer documentation or type hints.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Adds external supporting material (sources) to the draft. Use this tool to either provide new content/URLs, or link an existing source using its unique ID.

---

## replace_editor_content
- **Current Description**: `Replace the entire content of the markdown editor with the provided text.`
- **Parameters**: `text (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of this tool is to programmatically overwrite all existing content within a designated markdown editor interface using the text provided in the `text` parameter. It does not append or modify; it completely replaces the entire body of text.

**When to Call:** This tool should be called when the user's intent is to set, reset, or entirely change the source material displayed in the markdown editor, and all previous content must be discarded.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

The description is very clear regarding its function (replacement). However, it could benefit from a stronger emphasis on the **destructive nature** of the action to prevent misuse.

*   **Missing Detail/Clarification:** While "replace" implies overwriting, explicitly stating that *no existing content will be preserved or appended* would eliminate any ambiguity for the calling model.
*   **Confusing Words:** None. The language is direct and actionable.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Use this tool to completely overwrite all content within the markdown editor with the provided text string. This action discards any existing material in the editor.

---

## validate_draft
- **Current Description**: `Validates the draft's formatting compliance for Bluesky (character limits, image counts, YouTube embeds, and missing ALT texts). YouTube videos must use embed syntax: ![Title](https://www.youtube.com/watch?v=VIDEO_ID).`
- **Parameters**: `content (string optional)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `validate_draft` is to act as a compliance checker for content intended for publication on the Bluesky platform. It ensures that the provided draft adheres to all technical formatting rules and best practices required by the platform.

**When to Call:** This tool should be called **before** any attempt is made to publish or submit a piece of content (a "draft") to Bluesky, especially if the content includes media (images, videos) or is expected to be long-form. It serves as a pre-flight check to prevent publication errors and ensure optimal formatting.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

Yes, while the description is highly informative, it lacks specific technical constraints, which makes its behavior ambiguous in several areas:

**Missing Details/Unclear Behaviors:**

1. **Specific Limits (Character Count):** The phrase "character limits" is vague. Does the tool check against a single maximum limit (e.g., 300 characters), or does it check for multiple context-dependent limits?
2. **Image Counts:** The description mentions "image counts," but doesn't specify if there is a minimum requirement, a maximum allowance, or if it simply checks that the count is non-zero.
3. **ALT Text Source/Scope:** It validates "missing ALT texts." This implies the tool must have access to image metadata (the alt text). The description does not clarify *how* this metadata is provided to the function—is it part of the `content` string, or is it handled by external API objects that are passed alongside the content?
4. **Failure Mode:** If validation fails, what does the tool return? Does it return a single boolean (`False`) and a general error message, or does it provide a detailed list of *all* violations (e.g., "Error 1: Exceeded character limit," "Error 2: Missing ALT text on image X")?

**Confusing Words:**
The description is generally clear, but the combination of listing multiple distinct checks ("character limits, image counts, YouTube embeds, and missing ALT texts") makes it sound like a checklist rather than a single cohesive validation process.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
Validates draft content for compliance with Bluesky formatting standards. It checks for character limits, required media elements (images/videos), and accessibility features like ALT texts.

---

## publish
- **Current Description**: `Triggers the publication of a drafted thread to a target platform. This is only allowed when the draft's platform variant is not already Sent.`
- **Parameters**: `platform (string enum: Bluesky)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of the `publish` tool is to finalize and deploy a pre-written piece of content (a "drafted thread") from an internal system out to a specific social media platform. It acts as the final publishing mechanism for drafted content.

**When to Call:** This tool should be called when the user has completed drafting the content, reviewed it, and explicitly intends for it to go live on the specified target platform (in this case, Bluesky). **Crucially, it must only be called if the system confirms that the draft has not already been published/sent to that specific platform.**

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Unclear Behaviors / Missing Details:**

1. **Input Content (Major Gap):** The most significant omission is that the tool only lists `platform` as a parameter. It does not show how the *drafted thread* itself is passed to the function. Does the tool assume the draft exists in a global state? Is it retrieved by an ID? This needs clarification for proper execution.
2. **"Platform Variant":** While understandable, this phrase suggests that the content might need platform-specific formatting (e.g., character limits or hashtag usage). The description should clarify if the tool handles this transformation automatically, or if the draft must already be perfectly formatted for the target platform.

**Confusing Words:**

* **"Sent":** While clear in context, defining "Sent" as a state check would improve robustness (e.g., *The function checks if the content's publication status is 'Draft' before proceeding.*).

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Publishes a stored draft thread to the specified platform. This action will only succeed if the content has not already been published for that target platform.

---

## image_search
- **Current Description**: `Search the web for images and return compact candidate URLs. For visual inspection, import a candidate via add_image_source first, then call view_image with media://{guid}.`
- **Parameters**: `query (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `image_search` is to perform a web search specifically for visual content (images) based on a user-provided query, and return a set of candidate URLs pointing to those images.

**When to Call:** This tool should be called whenever the user's request requires visual examples, illustrations, or real-world pictures that cannot be answered with text alone. Examples include:
*   "What does a blue whale look like?"
*   "Show me examples of Renaissance architecture."
*   "Find images of tropical rainforest flora."

***Note on Workflow:** The second half of the description ("For visual inspection, import... then call view_image...") is not part of the tool's *purpose*, but rather a mandatory **operational instruction** for how the system must handle and display the results after the search is executed.

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Confusing/Overly Technical:**
*   The description mixes the high-level function (searching) with extremely low-level API workflow instructions (`add_image_source`, `view_image`). These operational steps are too technical for a general tool description and should be moved to internal documentation.
*   "Compact candidate URLs" is slightly vague. While it implies efficiency, simply stating "a list of image URLs" would be clearer.

**Missing Details:**
*   It does not specify the scope or limitations (e.g., Is this search limited to Google Images? Does it handle complex queries well?).
*   It doesn't mention if the tool can handle multiple queries or if there is a limit on the number of results returned.

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
"Searches the web for images based on a given query and returns a list of candidate URLs. Use this when the user requires visual examples or illustrations."

---

## add_image_source
- **Current Description**: `Downloads an image from a URL, processes it, resizes it, saves it as a media asset for this draft, and returns the markdown image tag (e.g. ![alt](media://{guid})).`
- **Parameters**: `url (string), altText (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `add_image_source` is to act as a media ingestion pipeline. It takes an external image URL, downloads the content, performs necessary backend processing (resizing, optimization), saves the resulting asset locally within the current drafting environment, and finally returns the fully formatted markdown tag required for inclusion in the document.

**When to Call:** This tool should be called whenever the user provides a public URL pointing to an image that needs to be incorporated into the generated draft content. It is used when the goal is not just to reference an image, but to embed and make it part of the working media assets for the current output.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Missing Details / Unclear Behaviors (Critical):**
1. **Resizing Parameters:** The description states that the tool "resizes it," but it does not specify *how* the user controls this. Does the tool automatically resize to fit a default container size? Can the calling code pass desired dimensions (e.g., `width` or `height`)? This is the most critical missing piece of information for reliable use.
2. **Processing Details:** The phrase "processes it" is vague. It needs clarification: Does this include compression, format conversion (e.g., converting PNG to WebP), or simple optimization?
3. **Error Handling/Failure Modes:** While not part of the description itself, a robust tool definition should clarify what happens if the URL is inaccessible, the image file is corrupted, or the asset saving process fails.

**Confusing Words:**
* **"Processes it":** This phrase is too generic and needs to be replaced with specific actions (e.g., "optimizes," "compresses," or "resizes").

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Downloads an image from a provided URL, optimizes it, and saves it as a media asset for this draft. It returns the complete markdown image tag ready for immediate inclusion in the document.

---

## view_image
- **Current Description**: `Fetch a specific image for visual inspection. Supports media://{guid}, file://{guid}, plain guid, or a direct http/https image URL (which will be imported first).`
- **Parameters**: `id (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The sole purpose of `view_image` is to retrieve and display a specific visual asset (an image) for the user's inspection. It acts as a universal fetcher for images, regardless of whether the source is an internal GUID, a local file path, or a public URL.

**When to Call:** This tool should be called whenever the conversation requires the user to see or inspect a specific picture that has been referenced by its unique identifier (GUID), a known file location, or a direct web link.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Confusing/Overly Technical Details:**
*   The description is highly technical regarding input formats (`media://{guid}`, `file://{guid}`, etc.). While accurate for a developer, this level of detail can clutter the prompt and might confuse an LLM trying to determine if the tool is relevant.
*   The phrase **"(which will be imported first)"** related to URLs suggests a complex internal workflow (importing before viewing). This operational detail is unnecessary for the model's decision-making process; it just needs to know that URLs work.

**Missing Details:**
*   **Error Handling:** The description does not mention what happens if the provided `id` is invalid, expired, or points to a non-existent image (e.g., "Image not found"). This information would be helpful for robust tool usage.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
Use this tool to fetch and display a specific image for visual inspection. It accepts various identifiers, including GUIDs, file paths, or direct web URLs.

---

## render_code_blocks
- **Current Description**: `Renders code blocks (triple-backtick fences) in the current draft as syntax-highlighted images and attaches them. Use when the user wants to post code as a visual image (Carbon-style). After rendering, the code fence is replaced with a compact ![code snippet](media://...) reference, which significantly reduces the post's character/word count and helps resolve character limit errors.`
- **Parameters**: `theme (string enum), language (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `render_code_blocks` is twofold:
1. **Visual Enhancement:** To take raw code enclosed in markdown fences (```) and convert it into a professional, syntax-highlighted image (like those generated by Carbon).
2. **Optimization/Efficiency:** Crucially, after rendering the visual image, the tool replaces the original, bulky source code block with a compact media reference (`![code snippet](media://...)`). This significantly reduces the character count of the final post, which is useful for platforms with strict limits or when optimizing word count.

**When to Call:** The tool should be called whenever the user provides a chunk of code that they intend to share in the draft and want it to appear as a visually appealing image rather than plain text markdown.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Unclear Behaviors/Missing Details:**
* **The Replacement Mechanism:** While described, the dual nature (render *and then replace*) is the most critical behavior and needs to be emphasized. A developer might assume it just renders an image and leaves the original code block intact; the description must clarify that the source material is replaced by a reference link.

**Confusing Words/Phrasing:**
* **"Carbon-style":** This is jargon. While helpful for context, focusing on "syntax-highlighted image" is clearer and more universally understood than relying on brand names.
* **Length/Density:** The description is very long and dense. It contains too much operational detail (e.g., mentioning the specific `![code snippet](media://...)` format) which should be reserved for internal documentation, not the public-facing tool description.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

**Suggested Description:**
> Renders raw code blocks into visually appealing, syntax-highlighted images. This process replaces the original source code with a compact media reference, significantly reducing character count while maintaining visual quality.

---

## set_bluesky_reply_target
- **Current Description**: `Set the active draft's Bluesky reply target from a strict URL in the form https://bsky.app/profile/<handle>/post/<rkey>. Once set, the reply target cannot be changed.`
- **Parameters**: `url (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The sole purpose of this tool is to programmatically designate a specific existing post on Bluesky as the intended recipient (the "reply target") for an active draft reply. It requires a highly precise URL format (`https://bsky.app/profile/<handle>/post/<rkey>`).

**When to Call:** This tool should be called *before* any drafting or sending of a reply, and only when the user has identified the exact Bluesky post they intend to reply to. It is crucial to understand that calling this function locks in the target; subsequent changes are impossible.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Clarity/Behavioral Notes:**
*   **Strength of Language:** The description uses strong language ("strict URL," "cannot be changed"), which is excellent for setting expectations and preventing misuse. This should be maintained.
*   **Missing Detail (Error Handling):** While not strictly necessary if the API handles it, it would be helpful to explicitly state what happens if the provided `url` does *not* match the required format or points to a non-existent post (e.g., "Returns an error if the URL is malformed or invalid.").
*   **Confusing Words:** There are no confusing words, but the concept of "active draft" implies that the user must already have initiated a drafting process for this tool call to be meaningful.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Sets the mandatory reply target for an active draft using a strict Bluesky URL format. This action is irreversible and locks the draft's intended recipient.

---

## web_search
- **Current Description**: `Search the web for current information, facts, news, or articles.`
- **Parameters**: `query (string)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The exact purpose of `web_search` is to access real-time, external information from the public internet. It allows the AI to retrieve facts, current events, news articles, or data that are too recent, specialized, or volatile to be contained within its static training knowledge base.

**When to Call:** This tool should be called when:
1. **Recency is required:** The user asks about an event that happened recently (e.g., "Who won the game last night?" or "What are today's stock prices?").
2. **Verification is needed:** The user asks for a specific fact that requires confirmation against current sources (e.g., "What is the current population of Tokyo?").
3. **General research is required:** The topic is broad and requires up-to-date background information or multiple perspectives (e.g., "Compare the latest features of Model X vs. Model Y.").

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Clarity/Behavior:**
* **Redundancy:** The list ("current information, facts, news, or articles") is slightly redundant but not misleading. It effectively covers the scope.
* **Missing Detail (Minor):** While not strictly necessary for a description, it doesn't specify *how* the search results are processed (e.g., if they need summarization or synthesis). However, this is an implementation detail, so the current description is functionally adequate.

**Conclusion:** The metadata is very clear and effective. It accurately communicates that the tool bridges the gap between static knowledge and real-time information. No major changes are needed.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Use this tool when you need up-to-date facts, current news, or specific details that require searching the live web. It is essential for answering questions about recent events or verifying information outside of your training data.

---

## format_validate_platform_content
- **Current Description**: `Formats and validates a draft content block against target platform constraints. Use this before saving or publishing platform variants.`
- **Parameters**: `content (string), platform (string enum)`

### Ollama Analysis:
### 1. What is the EXACT purpose of this tool, and when should it be called?

**Purpose:** The primary purpose of `format_validate_platform_content` is to act as a quality gate for content. It ensures that raw, draft content (`content`) is not only structurally correct (validated) but also properly styled or converted into the required format (formatted) necessary for a specific destination environment (`platform`).

**When to Call:** This tool must be called **immediately before** any attempt to persist or publish the content. If you are preparing to save, upload, or display the content on a platform (e.g., WordPress, Shopify, mobile app), this function should run first to guarantee compliance with that platform's technical rules and formatting requirements.

***

### 2. Are there any unclear behaviors, missing details, or confusing words?

**Missing Details/Ambiguities:**
1. **Mechanism of Action (The "How"):** The description is vague about *how* it formats or validates. Does it return a boolean (`True`/`False`) and an error message, or does it return the corrected content string? A developer needs to know if calling this function modifies the input `content` or just checks its validity.
2. **Definition of "Constraints":** While we assume constraints are rules (e.g., character limits, required tags, forbidden characters), the tool description doesn't specify *what* kind of constraints it handles (e.g., HTML sanitization, Markdown conversion, length checking).

**Confusing Words/Phrasing:**
1. **"Formats and validates":** These two actions are distinct. "Validation" is a check (is this okay?), while "Formatting" is an action (make this okay). Combining them suggests the tool does both simultaneously, which might confuse users about whether they need to provide clean content or if the tool will fix it for them.

***

### 3. Suggest a concise, clear description (1-2 sentences) to put inside the tool code.

> Use this function to ensure draft content is correctly formatted and meets all technical constraints before publishing or saving platform variants. It validates compliance against the specified target environment.

---

