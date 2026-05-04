namespace BookingService.Domain;

/// <summary>States of the four-step meeting room booking workflow.</summary>
public enum BookingState
{
    /// <summary>Process created, no room held yet.</summary>
    Pending,

    /// <summary>Step 1 done: room slot is held.</summary>
    RoomReserved,

    /// <summary>Step 2 done: organizer details confirmed.</summary>
    DetailsConfirmed,

    /// <summary>Step 3 done: payment captured.</summary>
    PaymentCaptured,

    /// <summary>Step 4 done: confirmation sent, terminal success.</summary>
    Completed,
}
