using System.Diagnostics.Metrics;

namespace BookingService.Telemetry;

public sealed class BookingTelemetry
{
    public const string MeterName = "BookingService.Booking";

    private readonly Counter<long> _transitionsSuccess;
    private readonly Counter<long> _transitionsError;
    private readonly Counter<long> _redeliveries;
    private readonly Counter<long> _compensations;
    private readonly Histogram<double> _stepLatencyMs;

    public BookingTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _transitionsSuccess = meter.CreateCounter<long>("booking.transitions.success", description: "Successful state transitions");
        _transitionsError = meter.CreateCounter<long>("booking.transitions.error", description: "Failed transitions (including simulated failures)");
        _redeliveries = meter.CreateCounter<long>("booking.events.redelivery", description: "Idempotent duplicate deliveries");
        _compensations = meter.CreateCounter<long>("booking.compensations", description: "Compensation actions executed");
        _stepLatencyMs = meter.CreateHistogram<double>(
            "booking.step.latency_ms",
            unit: "ms",
            description: "Rough end-to-end handling time per applied event");
    }

    public void RecordTransitionSuccess(string eventName) =>
        _transitionsSuccess.Add(1, new KeyValuePair<string, object?>("event", eventName));

    public void RecordTransitionError(string eventName) =>
        _transitionsError.Add(1, new KeyValuePair<string, object?>("event", eventName));

    public void RecordRedelivery(string eventName) =>
        _redeliveries.Add(1, new KeyValuePair<string, object?>("event", eventName));

    public void RecordCompensation(string reason) =>
        _compensations.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void RecordStepLatency(string eventName, double elapsedMs) =>
        _stepLatencyMs.Record(elapsedMs, new KeyValuePair<string, object?>("event", eventName));
}
