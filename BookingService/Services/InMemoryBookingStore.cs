using System.Collections.Concurrent;
using BookingService.Domain;

namespace BookingService.Services;

public sealed class InMemoryBookingStore
{
    private readonly ConcurrentDictionary<string, ProcessInstance> _processes = new(StringComparer.Ordinal);

    public ProcessInstance GetOrCreate(string processKey) =>
        _processes.GetOrAdd(processKey, static key => new ProcessInstance(key));

    public bool TryGet(string processKey, out ProcessInstance? instance) =>
        _processes.TryGetValue(processKey, out instance);

    public int ProcessCount => _processes.Count;
}

public sealed class ProcessInstance(string processKey)
{
    public string ProcessKey { get; } = processKey;
    public object Sync { get; } = new();

    public BookingState State { get; set; } = BookingState.Pending;
    public int CompensationCount { get; set; }
    public string? LastError { get; set; }

    /// <summary>Idempotency key -> last recorded outcome signature for replay.</summary>
    public Dictionary<string, IdempotentOutcome> Outcomes { get; } = new(StringComparer.Ordinal);

    public List<string> AppliedKeysInOrder { get; } = [];
}

public sealed class IdempotentOutcome
{
    public required BookingState State { get; init; }
    public required bool Duplicate { get; init; }
    public required bool Applied { get; init; }
    public required bool Compensated { get; init; }
    public string? Message { get; init; }
}
