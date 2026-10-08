using System.Text;
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
/// <item>Lengths count Unicode code points, like PostgreSQL's char_length()
/// and [...text].length in JavaScript. "😀" is one code point; a combined
/// emoji such as a family (people joined by zero-width joiners) is several.</item>
/// <item>Missing, null or non-string fields are reported as field errors.</item>
/// </list>
/// </summary>
public static partial class ServiceRequestValidator
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int EmailLocalPartMaxLength = 64;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 2000;

    public static readonly IReadOnlyList<string> ServiceCodes = ["workflow-automation", "api-integration", "ai-triage"];

    private const string InvalidEmailMessage = "Geçerli bir e-posta adresi girin.";

    // E-mail = local part "@" domain; a format check only, it does not prove
    // that the address exists. Not a full RFC 5321/5322 or IDNA check.
    //
    // Local part: ASCII "dot-atom" (RFC 5322). One or more non-empty parts
    // joined by dots, each made of letters, digits and
    // ! # $ % & ' * + - / = ? ^ _ ` { | } ~. Leading, trailing or repeated
    // dots, spaces, control characters, "<" and ">" are rejected. Quoted and
    // non-ASCII local parts are out of scope. \z instead of $: in .NET, $ also
    // matches before a final "\n".
    [GeneratedRegex(@"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*\z")]
    private static partial Regex EmailLocalPartPattern();

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

        // "@" is not allowed in either part, so a valid address splits into exactly two.
        var parts = value.Split('@');
        if (parts.Length != 2)
        {
            return InvalidEmailMessage;
        }

        var (localPart, domain) = (parts[0], parts[1]);
        if (CountCharacters(localPart) > EmailLocalPartMaxLength)
        {
            return $"E-postanın @ işaretinden önceki kısmı en fazla {EmailLocalPartMaxLength} karakter olabilir.";
        }

        return EmailLocalPartPattern().IsMatch(localPart) && IsValidDomain(domain) ? null : InvalidEmailMessage;
    }

    // Domain: two or more labels joined by dots ("mail.example.com.tr").
    private static bool IsValidDomain(string domain)
    {
        var labels = domain.Split('.');
        return labels.Length >= 2 && labels.All(IsValidDomainLabel);
    }

    // A label is letters or decimal digits of any script, with hyphens only
    // inside ("mail", "my-company", "örnek", "xn--rnek-zoa"). It is checked per
    // code point (Rune), not per UTF-16 char, so that letters outside the BMP
    // such as U+10400 are treated like \p{L} / \p{Nd} with the u flag in
    // validation.js.
    private static bool IsValidDomainLabel(string label)
    {
        var runes = label.EnumerateRunes().ToArray();
        if (runes.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < runes.Length; i++)
        {
            var isLetterOrDigit = Rune.IsLetter(runes[i]) || Rune.IsDigit(runes[i]);
            var isInnerHyphen = runes[i].Value == '-' && i > 0 && i < runes.Length - 1;
            if (!isLetterOrDigit && !isInnerHyphen)
            {
                return false;
            }
        }

        return true;
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
