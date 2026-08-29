using System;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public sealed record BackgroundTaskDto(
    Guid Id,
    string TaskType,
    string PayloadJson,
    string Status,
    int Priority,
    int AttemptCount,
    int MaxAttempts,
    DateTime NextAttemptAt,
    DateTime? LockedUntil,
    string? LockedByWorker,
    string? LastError,
    string? DeduplicationKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt)
{
    public BackgroundTaskDto(BackgroundTask t) : this(
        t.Id,
        t.TaskType,
        t.PayloadJson,
        t.Status.ToString(),
        t.Priority,
        t.AttemptCount,
        t.MaxAttempts,
        t.NextAttemptAt,
        t.LockedUntil,
        t.LockedByWorker,
        t.LastError,
        t.DeduplicationKey,
        t.CreatedAt,
        t.UpdatedAt,
        t.CompletedAt)
    {
    }
}
