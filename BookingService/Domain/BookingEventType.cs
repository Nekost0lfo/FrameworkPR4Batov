namespace BookingService.Domain;

public enum BookingEventType
{
    ReserveRoom,
    ConfirmDetails,
    CapturePayment,
    SendConfirmation,
}
