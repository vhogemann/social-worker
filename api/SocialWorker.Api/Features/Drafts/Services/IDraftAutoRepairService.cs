using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Drafts.Services;

public sealed record DraftRepairResult(bool Success, string? RepairedContent, string? ErrorMessage)
{
    public static DraftRepairResult Succeeded(string repairedContent) => new(true, repairedContent, null);
    public static DraftRepairResult Failed(string errorMessage) => new(false, null, errorMessage);
}

public interface IDraftAutoRepairService
{
    Task<DraftRepairResult> RepairDraftAsync(
        Draft draft,
        string platform,
        string validationError,
        Guid userId,
        CancellationToken ct);

    Task<DraftRepairResult> RepairPlatformThreadAsync(
        PlatformThread thread,
        string validationError,
        Guid userId,
        CancellationToken ct);
}
