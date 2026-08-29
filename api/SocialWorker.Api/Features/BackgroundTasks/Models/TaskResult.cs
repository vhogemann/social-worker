using System;

namespace SocialWorker.Api.Features.BackgroundTasks.Models;

public enum TaskOutcome
{
    Success,
    Retry,
    Fatal
}

public sealed record TaskResult
{
    public TaskOutcome Outcome { get; }
    public string? ErrorMessage { get; }
    public TimeSpan? RetryDelay { get; }

    private TaskResult(TaskOutcome outcome, string? errorMessage, TimeSpan? retryDelay)
    {
        Outcome = outcome;
        ErrorMessage = errorMessage;
        RetryDelay = retryDelay;
    }

    public static TaskResult Success() => new(TaskOutcome.Success, null, null);

    public static TaskResult Retry(TimeSpan delay, string reason) =>
        new(TaskOutcome.Retry, reason, delay);

    public static TaskResult Fatal(string error) =>
        new(TaskOutcome.Fatal, error, null);
}
