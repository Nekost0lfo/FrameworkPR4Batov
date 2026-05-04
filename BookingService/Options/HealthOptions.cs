namespace BookingService.Options;

public sealed class HealthOptions
{
    public const string SectionName = "Health";

    /// <summary>After this many consecutive failed transitions, readiness fails.</summary>
    public int CriticalFailureThreshold { get; set; } = 5;

    /// <summary>When true, readiness is always unhealthy (for local demos).</summary>
    public bool SimulateCriticalDegradation { get; set; }
}
