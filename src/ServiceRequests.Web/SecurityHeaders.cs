namespace ServiceRequests.Web;

public static class SecurityHeaders
{
    // The page loads only its own HTML, CSS and JS and posts to its own API,
    // with no inline script or style, so "self" is enough everywhere.
    public const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; " +
        "object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

    /// <summary>
    /// Adds Content-Security-Policy, X-Content-Type-Options and Referrer-Policy
    /// to every response, including error responses.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Set when the response starts rather than now: the exception
            // handler clears headers that were set earlier.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });

            await next(context);
        });
}
