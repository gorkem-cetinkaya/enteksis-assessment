namespace ServiceRequests.Web;

public sealed record HealthResponse(string Status, string? Commit);

public static class HealthEndpoint
{
    /// <summary>
    /// GET /health shows that the app answers and which commit is running.
    /// It does not check the database. On Render the commit comes from the
    /// RENDER_GIT_COMMIT variable that Render sets for each deploy; elsewhere it
    /// is null. Nothing else (connection details, variables, records) is shown.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (IConfiguration configuration, HttpResponse response) =>
        {
            var commit = configuration["RENDER_GIT_COMMIT"];
            response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new HealthResponse("ok", string.IsNullOrWhiteSpace(commit) ? null : commit));
        });

        return app;
    }
}
