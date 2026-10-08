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
        app.MapPost("/api/requests", CreateRequestAsync);
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

        // The body is parsed by hand (not bound to a C# type) so that missing,
        // null and wrongly typed fields become field errors instead of a
        // framework-generated 400 without details.
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
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
                loggerFactory.CreateLogger(typeof(ServiceRequestEndpoints))
                    .LogError(exception, "Saving a service request failed");

                return TypedResults.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Talep şu anda kaydedilemedi. Lütfen daha sonra tekrar deneyin.");
            }
        }
    }
}
