using System.Collections.Generic;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Publishing.GitHubBlog;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed class GitHubBlogPlatformContentPolicy : IPlatformContentPolicy
{
    public SocialPlatform Platform => SocialPlatform.GitHubBlog;

    public PlatformContentPolicyResult Evaluate(string content, bool normalizeFormatting)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var raw = content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            errors.Add("Blog post content cannot be empty.");
        }
        else if (GitHubBlogDocumentBuilder.HasUnclosedFrontmatter(raw))
        {
            errors.Add("YAML frontmatter starts with '---' but is missing a closing '---' delimiter.");
        }

        return new PlatformContentPolicyResult(errors.Count == 0, raw, errors, warnings);
    }

    public string GetAdaptationRules()
    {
        return """
GitHub Blog (Jekyll) rules:
- Long-form CommonMark Markdown article (no character limit; aim for clear, well-structured prose)
- Optional YAML frontmatter at the top (layout, title, tags, categories, excerpt) or start directly with a '# Title' heading
- Use full Markdown freely: ## and ### subheadings, bullet lists, blockquotes, links, tables, and fenced code blocks with language tags
- Do NOT split into short social thread segments; write one cohesive article (use --- only for thematic breaks or frontmatter)
- Always include descriptive alt text on embedded images
""";
    }
}
