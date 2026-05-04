using System.Diagnostics;
using BookingService.Domain;
using BookingService.Telemetry;

namespace BookingService.Services;

public sealed class BookingApplicationService(
    InMemoryBookingStore store,
    BookingTelemetry telemetry,
    ILogger<BookingApplicationService> logger,
    ServiceHealthState healthState)
{
    public ProcessResult HandleEvent(
        string processKey,
        string idempotencyKey,
        BookingEventType eventType,
        string correlationId,
        bool simulateFailure)
    {
        var instance = store.GetOrCreate(processKey);
        var sw = Stopwatch.StartNew();

        lock (instance.Sync)
        {
            if (instance.Outcomes.TryGetValue(idempotencyKey, out var cached))
            {
                telemetry.RecordRedelivery(eventType.ToString());
                logger.LogInformation(
                    "Redelivery ignored: same idempotency key. ProcessKey={ProcessKey}, IdempotencyKey={IdempotencyKey}, Event={Event}, State={State}, CorrelationId={CorrelationId}",
                    processKey, idempotencyKey, eventType, cached.State, correlationId);
                sw.Stop();
                return new ProcessResult(
                    correlationId,
                    cached.State,
                    Applied: false,
                    Duplicate: true,
                    Compensated: cached.Compensated,
                    Message: "Duplicate delivery; state unchanged.");
            }

            var (newState, applied, compensated, message) = ApplyTransition(
                instance,
                eventType,
                correlationId,
                simulateFailure);

            sw.Stop();
            telemetry.RecordStepLatency(eventType.ToString(), sw.Elapsed.TotalMilliseconds);

            instance.Outcomes[idempotencyKey] = new IdempotentOutcome
            {
                State = newState,
                Duplicate = false,
                Applied = applied,
                Compensated = compensated,
                Message = message,
            };
            if (applied)
            {
                instance.AppliedKeysInOrder.Add(idempotencyKey);
            }

            return new ProcessResult(correlationId, newState, applied, Duplicate: false, compensated, message);
        }
    }

    public ProcessSnapshot GetSnapshot(string processKey)
    {
        if (!store.TryGet(processKey, out var instance) || instance is null)
        {
            return new ProcessSnapshot
            {
                ProcessKey = processKey,
                State = BookingState.Pending,
                AppliedIdempotencyKeys = [],
                LastError = null,
                CompensationCount = 0,
            };
        }

        lock (instance.Sync)
        {
            return new ProcessSnapshot
            {
                ProcessKey = processKey,
                State = instance.State,
                AppliedIdempotencyKeys = instance.AppliedKeysInOrder.ToList(),
                LastError = instance.LastError,
                CompensationCount = instance.CompensationCount,
            };
        }
    }

    private (BookingState state, bool applied, bool compensated, string message) ApplyTransition(
        ProcessInstance p,
        BookingEventType eventType,
        string correlationId,
        bool simulateFailure)
    {
        p.LastError = null;

        if (!IsExpectedEvent(p.State, eventType))
        {
            telemetry.RecordTransitionError(eventType.ToString());
            healthState.RegisterStepFailure();
            var msg = $"Event {eventType} is not valid for state {p.State}.";
            logger.LogWarning(
                "Invalid transition rejected. ProcessKey={ProcessKey}, State={State}, Event={Event}, CorrelationId={CorrelationId}, Reason={Reason}",
                p.ProcessKey, p.State, eventType, correlationId, msg);
            return (p.State, applied: false, compensated: false, msg);
        }

        if (simulateFailure)
        {
            return HandleFailureAfterValidation(p, eventType, correlationId);
        }

        var next = Advance(p.State, eventType);
        p.State = next;
        telemetry.RecordTransitionSuccess(eventType.ToString());
        healthState.RegisterStepSuccess();
        logger.LogInformation(
            "Transition applied. ProcessKey={ProcessKey}, Event={Event}, NewState={NewState}, CorrelationId={CorrelationId}",
            p.ProcessKey, eventType, next, correlationId);
        return (next, applied: true, compensated: false, $"Moved to {next}.");
    }

    private (BookingState state, bool applied, bool compensated, string message) HandleFailureAfterValidation(
        ProcessInstance p,
        BookingEventType eventType,
        string correlationId)
    {
        telemetry.RecordTransitionError(eventType.ToString());
        healthState.RegisterStepFailure();

        // Failure of a step after the room was held: compensate by releasing the room (single compensation type).
        if (p.State != BookingState.Pending)
        {
            logger.LogWarning(
                "Simulated step failure; running compensation ReleaseRoom. ProcessKey={ProcessKey}, FailedEvent={Event}, StateBeforeFailure={State}, CorrelationId={CorrelationId}",
                p.ProcessKey, eventType, p.State, correlationId);
            RunReleaseRoomCompensation(p, correlationId);
            var msg = $"Step {eventType} failed; compensation ReleaseRoom executed; state reset to {p.State}.";
            return (p.State, applied: true, compensated: true, msg);
        }

        p.LastError = $"Simulated failure on {eventType} while still {p.State}.";
        logger.LogWarning(
            "Simulated failure before any resource was held. ProcessKey={ProcessKey}, Event={Event}, CorrelationId={CorrelationId}",
            p.ProcessKey, eventType, correlationId);
        return (p.State, applied: false, compensated: false, p.LastError);
    }

    private void RunReleaseRoomCompensation(ProcessInstance p, string correlationId)
    {
        p.CompensationCount++;
        telemetry.RecordCompensation("ReleaseRoom");
        logger.LogInformation(
            "Compensation ReleaseRoom completed. ProcessKey={ProcessKey}, CorrelationId={CorrelationId}, CompensationCount={Count}",
            p.ProcessKey, correlationId, p.CompensationCount);
        p.State = BookingState.Pending;
    }

    private static bool IsExpectedEvent(BookingState state, BookingEventType evt) =>
        state switch
        {
            BookingState.Pending => evt == BookingEventType.ReserveRoom,
            BookingState.RoomReserved => evt == BookingEventType.ConfirmDetails,
            BookingState.DetailsConfirmed => evt == BookingEventType.CapturePayment,
            BookingState.PaymentCaptured => evt == BookingEventType.SendConfirmation,
            BookingState.Completed => false,
            _ => false,
        };

    private static BookingState Advance(BookingState state, BookingEventType evt) =>
        (state, evt) switch
        {
            (BookingState.Pending, BookingEventType.ReserveRoom) => BookingState.RoomReserved,
            (BookingState.RoomReserved, BookingEventType.ConfirmDetails) => BookingState.DetailsConfirmed,
            (BookingState.DetailsConfirmed, BookingEventType.CapturePayment) => BookingState.PaymentCaptured,
            (BookingState.PaymentCaptured, BookingEventType.SendConfirmation) => BookingState.Completed,
            _ => state,
        };
}

public sealed record ProcessResult(
    string CorrelationId,
    BookingState State,
    bool Applied,
    bool Duplicate,
    bool Compensated,
    string Message);
