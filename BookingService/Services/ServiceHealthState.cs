namespace BookingService.Services;

/// <summary>Tracks critical degradation for readiness (e.g. sustained failures).</summary>
public sealed class ServiceHealthState
{
    private readonly object _sync = new();
    private int _consecutiveStepFailures;
    private bool _forceCriticalDegradation;

    public int ConsecutiveStepFailures
    {
        get { lock (_sync) return _consecutiveStepFailures; }
    }

    public void RegisterStepFailure()
    {
        lock (_sync)
        {
            _consecutiveStepFailures++;
        }
    }

    public void RegisterStepSuccess()
    {
        lock (_sync)
        {
            _consecutiveStepFailures = 0;
        }
    }

    public void SetForceCriticalDegradation(bool value)
    {
        lock (_sync)
        {
            _forceCriticalDegradation = value;
        }
    }

    /// <summary>Readiness fails when failures pile up or operator forces degradation.</summary>
    public bool IsCriticallyDegraded(int failureThreshold = 5)
    {
        lock (_sync)
        {
            return _forceCriticalDegradation || _consecutiveStepFailures >= failureThreshold;
        }
    }
}
