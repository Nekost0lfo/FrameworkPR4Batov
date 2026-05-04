using BookingService.Options;
using BookingService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace BookingService.Health;

public sealed class ReadinessHealthCheck(
    ServiceHealthState healthState,
    IOptions<HealthOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (opts.SimulateCriticalDegradation)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Simulated critical degradation is enabled."));
        }

        if (healthState.IsCriticallyDegraded(opts.CriticalFailureThreshold))
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    $"Too many consecutive step failures ({healthState.ConsecutiveStepFailures} >= {opts.CriticalFailureThreshold})."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Ready to accept booking traffic."));
    }
}
