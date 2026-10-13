using System;

namespace SocialWorker.Api.Features.Scheduling.Services;

public static class SchedulePriorityCalculator
{
    public const double DecayPerHour = 0.25;
    public const double MinScoreFloor = 1.0;
    public const double PreemptionThreshold = 1.5;
    public static readonly TimeSpan FreezeHorizon = TimeSpan.FromHours(1);
    public static readonly TimeSpan MaxQueueTtl = TimeSpan.FromHours(72);

    public static double CalculateEffectiveScore(int? baseScore, DateTime createdAtUtc, DateTime nowUtc)
    {
        var baseVal = (double)(baseScore.HasValue ? Math.Clamp(baseScore.Value, 1, 10) : 7);
        var ageHours = Math.Max(0.0, (nowUtc - createdAtUtc).TotalHours);
        return Math.Max(MinScoreFloor, baseVal - (DecayPerHour * ageHours));
    }

    public static bool IsExpired(DateTime createdAtUtc, DateTime nowUtc) =>
        (nowUtc - createdAtUtc) >= MaxQueueTtl;

    public static bool CanPreempt(double candidateScore, double incumbentScore) =>
        candidateScore >= incumbentScore + PreemptionThreshold;

    public static bool IsFrozen(DateTime slotUtc, DateTime nowUtc) =>
        slotUtc <= nowUtc + FreezeHorizon;
}
