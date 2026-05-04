namespace BookingService.Domain;

public sealed class ProcessSnapshot
{
    public required BookingState State { get; init; }
    public required string ProcessKey { get; init; }
    public required IReadOnlyList<string> AppliedIdempotencyKeys { get; init; }
    public string? LastError { get; init; }
    public int CompensationCount { get; init; }
}
