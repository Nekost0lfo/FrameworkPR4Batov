using BookingService.Domain;

namespace BookingService.Api;

public sealed record PostEventRequest
{
    public required string IdempotencyKey { get; init; }

    public required BookingEventType Event { get; init; }

    public bool SimulateFailure { get; init; }
}

public sealed record PostEventResponse(
    string CorrelationId,
    string ProcessKey,
    string IdempotencyKey,
    string State,
    bool Applied,
    bool Duplicate,
    bool Compensated,
    string Message);

public sealed record ProcessStatusResponse(
    string ProcessKey,
    string State,
    IReadOnlyList<string> AppliedIdempotencyKeys,
    string? LastError,
    int CompensationCount);
