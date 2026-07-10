using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SocialWorker.Api.Features.CodeImages;

public static class CodeImageEndpoint
{
    public sealed record RenderCodeImageRequest(
        string Code,
        string Language,
        string? Theme);

    public static void MapCodeImageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/drafts/{draftId:guid}/code-image", async (
            ClaimsPrincipal principal,
            CodeImageService codeImageService,
            Guid draftId,
            RenderCodeImageRequest request,
            CancellationToken ct) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Code))
                return Results.BadRequest("Code is required.");

            var block = new CodeBlock(request.Language ?? "", request.Code);
            var theme = CodeTheme.FromString(request.Theme);

            try
            {
                var result = await codeImageService.RenderAndStoreAsync(userId.Value, draftId, block, theme, ct);
                return Results.Ok(new { id = result.Id, markdownTag = result.MarkdownTag });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound("Draft not found or access denied.");
            }
        }).RequireAuthorization();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }
}
