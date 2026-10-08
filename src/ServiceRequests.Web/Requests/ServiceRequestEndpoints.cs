using System.Text.Json;
using ServiceRequests.Web.Data;

namespace ServiceRequests.Web.Requests;

public sealed record CreateServiceRequestResponse(Guid RequestId);

public static class ServiceRequestEndpoints
{
    public static IEndpointRouteBuilder MapServiceRequestEndpoints(this IEndpointRouteBuilder app)
    {
        // Only creating a request is exposed. There is deliberately no endpoint
        // that lists or reads back stored requests.
        app.MapPost("/api/requests", CreateRequestAsync).RequireRateLimiting(RequestLimits.RateLimitPolicy);
        return app;
    }

    private static async Task<IResult> CreateRequestAsync(
        HttpRequest request,
        IServiceRequestStore store,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "İstek gövdesi JSON olmalıdır (Content-Type: application/json).");
        }

        // Bodies over 32 KiB get 413: at once when Content-Length says so,
        // otherwise (e.g. chunked) as soon as one byte past the limit is read.
        if (request.ContentLength > RequestLimits.MaxBodyBytes)
        {
            return BodyTooLarge();
        }

        MemoryStream? body;
        try
        {
            body = await ReadAtMostAsync(request.Body, RequestLimits.MaxBodyBytes, cancellationToken);
        }
        catch (BadHttpRequestException exception)
        {
            // Raised by the server for a malformed body, e.g. broken chunked encoding.
            return TypedResults.Problem(statusCode: exception.StatusCode, title: "İstek gövdesi okunamadı.");
        }

        if (body is null)
        {
            return BodyTooLarge();
        }

        // The body is parsed by hand (not bound to a C# type) so that missing,
        // null and wrongly typed fields become field errors instead of a
        // framework-generated 400 without details.
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "İstek gövdesi geçerli bir JSON değil.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "İstek gövdesi bir JSON nesnesi olmalıdır.");
            }

            var validation = ServiceRequestValidator.Validate(document.RootElement);
            if (validation.Request is null)
            {
                return TypedResults.ValidationProblem(validation.Errors, title: "Gönderilen bilgilerde hata var.");
            }

            try
            {
                var requestId = await store.InsertAsync(validation.Request, cancellationToken);
                return TypedResults.Created((string?)null, new CreateServiceRequestResponse(requestId));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Full details go to the server log only. The client gets a
                // generic message: no SQL, connection string or stack trace.
                // It does not claim the request was not saved: if the
                // connection drops right after COMMIT, the row may exist.
                loggerFactory.CreateLogger(typeof(ServiceRequestEndpoints))
                    .LogError(exception, "Saving a service request failed");

                return TypedResults.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Talebinizin kaydedildiğini doğrulayamadık. Lütfen bir süre sonra tekrar deneyin.");
            }
        }
    }

    private static IResult BodyTooLarge() => TypedResults.Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "İstek gövdesi çok büyük. En fazla 32 KiB gönderilebilir.");

    /// <summary>
    /// Reads the whole body if it is at most <paramref name="maxBytes"/> long;
    /// returns null as soon as it turns out to be longer. Works the same with
    /// or without a Content-Length header and on any server.
    /// </summary>
    private static async Task<MemoryStream?> ReadAtMostAsync(Stream body, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes + 1];
        var length = 0;
        int read;
        while (length < buffer.Length && (read = await body.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
        {
            length += read;
        }

        return length > maxBytes ? null : new MemoryStream(buffer, 0, length, writable: false);
    }
}
