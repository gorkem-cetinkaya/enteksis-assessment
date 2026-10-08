using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceRequests.Web.Requests;

/// <summary>A request that passed validation. Text values are already trimmed.</summary>
public sealed record ServiceRequestInput(string Name, string Email, string Service, string Description);

/// <summary>Either a valid <see cref="Request"/> or field errors keyed by JSON property name.</summary>
public sealed record ServiceRequestValidationResult(ServiceRequestInput? Request, Dictionary<string, string[]> Errors);

/// <summary>
/// Server-side rules for POST /api/requests. wwwroot/app.js applies the same
/// rules in the browser, but this class always runs and is the authority.
/// <list type="bullet">
/// <item>Trimming removes Unicode whitespace from both ends (string.Trim).</item>
/// <item>Lengths count Unicode characters (code points), like PostgreSQL's
/// char_length() and [...text].length in JavaScript.</item>
/// <item>Missing, null or non-string fields are reported as field errors.</item>
/// </list>
/// </summary>
public static partial class ServiceRequestValidator
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 2000;

    public static readonly IReadOnlyList<string> ServiceCodes = ["workflow-automation", "api-integration", "ai-triage"];

    // local@domain without whitespace, control characters or a second "@";
    // the domain needs at least one dot and no empty labels ("a..b", ".a", "a.").
    [GeneratedRegex(@"^[^\s@\p{Cc}]+@(?:[^\s@\p{Cc}.]+\.)+[^\s@\p{Cc}.]+$")]
    private static partial Regex EmailPattern();

    /// <param name="body">The parsed request body; must be a JSON object.</param>
    public static ServiceRequestValidationResult Validate(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("The request body must be a JSON object.", nameof(body));
        }

        var errors = new Dictionary<string, string[]>();

        var name = ReadString(body, "name", "Ad soyad", errors)?.Trim();
        if (name is not null)
        {
            AddError(errors, "name", CheckText(name, "Ad soyad", NameMinLength, NameMaxLength, allowLineBreaks: false));
        }

        var email = ReadString(body, "email", "E-posta", errors)?.Trim();
        if (email is not null)
        {
            AddError(errors, "email", CheckEmail(email));
        }

        // Service codes are compared exactly, without trimming.
        var service = ReadString(body, "service", "Hizmet seçimi", errors);
        if (service is not null)
        {
            AddError(errors, "service", CheckService(service));
        }

        var description = ReadString(body, "description", "Açıklama", errors)?.Trim();
        if (description is not null)
        {
            AddError(errors, "description",
                CheckText(description, "Açıklama", DescriptionMinLength, DescriptionMaxLength, allowLineBreaks: true));
        }

        return errors.Count == 0
            ? new(new ServiceRequestInput(name!, email!, service!, description!), errors)
            : new(null, errors);
    }

    /// <summary>Returns the string value, or null after recording why it is unusable.</summary>
    private static string? ReadString(JsonElement body, string field, string label, Dictionary<string, string[]> errors)
    {
        if (!body.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            errors[field] = [$"{label} zorunludur."];
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            errors[field] = [$"{label} metin olmalıdır."];
            return null;
        }

        try
        {
            return value.GetString();
        }
        catch (InvalidOperationException)
        {
            // Invalid UTF-16 escapes such as a lone "\ud800" cannot be read as text.
            errors[field] = [$"{label} geçersiz karakter içeriyor."];
            return null;
        }
    }

    private static string? CheckText(string value, string label, int minLength, int maxLength, bool allowLineBreaks)
    {
        if (value.Length == 0)
        {
            return $"{label} zorunludur.";
        }

        // Control characters are rejected; the description may contain tabs and line breaks.
        if (value.Any(c => char.IsControl(c) && !(allowLineBreaks && (c is '\t' or '\n' or '\r'))))
        {
            return $"{label} geçersiz karakter içeriyor.";
        }

        var length = CountCharacters(value);
        if (length < minLength)
        {
            return $"{label} en az {minLength} karakter olmalıdır.";
        }

        if (length > maxLength)
        {
            return $"{label} en fazla {maxLength} karakter olabilir.";
        }

        return null;
    }

    private static string? CheckEmail(string value)
    {
        if (value.Length == 0)
        {
            return "E-posta zorunludur.";
        }

        if (CountCharacters(value) > EmailMaxLength)
        {
            return $"E-posta en fazla {EmailMaxLength} karakter olabilir.";
        }

        return EmailPattern().IsMatch(value) ? null : "Geçerli bir e-posta adresi girin.";
    }

    private static string? CheckService(string value)
    {
        if (value.Length == 0)
        {
            return "Hizmet seçimi zorunludur.";
        }

        return ServiceCodes.Contains(value) ? null : "Geçerli bir hizmet seçin.";
    }

    private static int CountCharacters(string value) => value.EnumerateRunes().Count();

    private static void AddError(Dictionary<string, string[]> errors, string field, string? error)
    {
        if (error is not null)
        {
            errors[field] = [error];
        }
    }
}
