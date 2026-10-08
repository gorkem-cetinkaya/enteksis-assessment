using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ServiceRequests.Web.Requests;

/// <summary>
/// Limits for POST /api/requests: at most 20 requests per 60-second window and
/// a 32 KiB request body.
/// </summary>
/// <remarks>
/// The rate limit is one counter shared by every client of this app instance.
/// That is a deliberate choice for a small single-instance demo: all visitors
/// share the limit, a restart resets it, separate instances would each count on
/// their own, and it is not DDoS protection. Client IPs are not used, so a
/// forged X-Forwarded-For header cannot change the result.
/// </remarks>
public static class RequestLimits
{
    public const string RateLimitPolicy = "service-requests";
    public const int PermitLimit = 20;
    public const int MaxBodyBytes = 32 * 1024;
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddServiceRequestRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            // A single partition, so the window is shared by all requests.
            options.AddFixedWindowLimiter(RateLimitPolicy, limiter =>
            {
                limiter.PermitLimit = PermitLimit;
                limiter.Window = Window;
                limiter.QueueLimit = 0;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : Window;
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                await TypedResults.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Kısa sürede çok fazla talep gönderildi. Lütfen biraz bekleyip tekrar deneyin.")
                    .ExecuteAsync(context.HttpContext);
            };
        });
}
