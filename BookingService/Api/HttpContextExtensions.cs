using BookingService.Middleware;

namespace BookingService.Api;

public static class HttpContextExtensions
{
    public static string GetRequiredCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) && value is string s && !string.IsNullOrEmpty(s))
        {
            return s;
        }

        return Guid.NewGuid().ToString("N");
    }
}
