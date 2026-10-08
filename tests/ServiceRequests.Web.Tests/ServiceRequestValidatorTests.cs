using System.Text.Json;
using System.Text.Json.Nodes;
using ServiceRequests.Web.Requests;

namespace ServiceRequests.Web.Tests;

public class ServiceRequestValidatorTests
{
    // U+1F600: one code point, but two UTF-16 code units in a .NET string.
    private const string Emoji = "\U0001F600";

    // Family emoji: three emoji joined by two zero-width joiners (U+200D), so
    // five code points although it is drawn as one symbol.
    private const string FamilyEmoji = "\U0001F468‍\U0001F469‍\U0001F467";

    // All test data is fictional; example.com is a reserved domain.
    private static JsonObject ValidBody() => new()
    {
        ["name"] = "Deniz Örnek",
        ["email"] = "deniz.ornek@example.com",
        ["service"] = "workflow-automation",
        ["description"] = "Gelen talepleri e-postadan Excel'e elle aktarıyoruz.",
    };

    [Fact]
    public void Valid_request_is_accepted_with_trimmed_values()
    {
        var body = ValidBody();
        body["name"] = "  Deniz Örnek \t";
        body["email"] = " deniz.ornek@example.com\n";
        body["description"] = "\n  Gelen talepleri e-postadan Excel'e elle aktarıyoruz.  ";

        var result = Validate(body);

        Assert.Empty(result.Errors);
        Assert.Equal(
            new ServiceRequestInput(
                "Deniz Örnek",
                "deniz.ornek@example.com",
                "workflow-automation",
                "Gelen talepleri e-postadan Excel'e elle aktarıyoruz."),
            result.Request);
    }

    [Fact]
    public void Unknown_fields_such_as_id_or_created_at_are_ignored()
    {
        var body = ValidBody();
        body["id"] = "00000000-0000-0000-0000-000000000001";
        body["created_at"] = "2000-01-01T00:00:00Z";

        Assert.NotNull(Validate(body).Request);
    }

    [Fact]
    public void Every_invalid_field_is_reported_at_once()
    {
        var result = Validate("""{"name":"A","email":"deniz","service":"consulting","description":"kısa"}""");

        Assert.Null(result.Request);
        Assert.Equal(["description", "email", "name", "service"], result.Errors.Keys.Order());
    }

    [Fact]
    public void Body_must_be_a_json_object()
    {
        Assert.Throws<ArgumentException>(() => Validate("[]"));
    }

    [Theory]
    [InlineData("name", "Ad soyad zorunludur.")]
    [InlineData("email", "E-posta zorunludur.")]
    [InlineData("service", "Hizmet seçimi zorunludur.")]
    [InlineData("description", "Açıklama zorunludur.")]
    public void Missing_field_is_required(string field, string expectedError)
    {
        var body = ValidBody();
        body.Remove(field);

        var result = Validate(body);

        Assert.Equal(expectedError, SingleError(result, field));
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("name", "Ad soyad zorunludur.")]
    [InlineData("email", "E-posta zorunludur.")]
    [InlineData("service", "Hizmet seçimi zorunludur.")]
    [InlineData("description", "Açıklama zorunludur.")]
    public void Null_field_is_required(string field, string expectedError)
    {
        Assert.Equal(expectedError, SingleError(ValidateWith(field, null), field));
    }

    [Theory]
    [InlineData("name", "42", "Ad soyad metin olmalıdır.")]
    [InlineData("email", "true", "E-posta metin olmalıdır.")]
    [InlineData("service", """["ai-triage"]""", "Hizmet seçimi metin olmalıdır.")]
    [InlineData("description", """{"text":"Gelen talepleri sınıflandırmak istiyoruz."}""", "Açıklama metin olmalıdır.")]
    public void Non_string_field_is_rejected(string field, string jsonValue, string expectedError)
    {
        Assert.Equal(expectedError, SingleError(ValidateWith(field, JsonNode.Parse(jsonValue)), field));
    }

    [Fact]
    public void Invalid_utf16_escape_is_rejected_instead_of_failing_later()
    {
        var result = Validate("""
            {"name":"Deniz \ud800","email":"deniz@example.com","service":"ai-triage","description":"Kurgusal test açıklaması."}
            """);

        Assert.Equal("Ad soyad geçersiz karakter içeriyor.", SingleError(result, "name"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_that_is_empty_after_trimming_is_required(string name)
    {
        Assert.Equal("Ad soyad zorunludur.", SingleError(ValidateWith("name", name), "name"));
    }

    [Fact]
    public void Name_length_is_checked_after_trimming()
    {
        Assert.Equal("Ad soyad en az 2 karakter olmalıdır.", SingleError(ValidateWith("name", "  D  "), "name"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void Name_length_limits_are_inclusive(int length)
    {
        Assert.NotNull(ValidateWith("name", new string('a', length)).Request);
    }

    [Fact]
    public void Name_longer_than_100_characters_is_rejected()
    {
        Assert.Equal(
            "Ad soyad en fazla 100 karakter olabilir.",
            SingleError(ValidateWith("name", new string('a', 101)), "name"));
    }

    [Fact]
    public void Name_length_counts_code_points_not_utf16_code_units()
    {
        Assert.Equal("Ad soyad en az 2 karakter olmalıdır.", SingleError(ValidateWith("name", Emoji), "name"));
        Assert.NotNull(ValidateWith("name", string.Concat(Enumerable.Repeat(Emoji, 100))).Request);
    }

    [Fact]
    public void Combined_emoji_counts_as_several_code_points()
    {
        Assert.NotNull(ValidateWith("name", string.Concat(Enumerable.Repeat(FamilyEmoji, 20))).Request); // 100
        Assert.Equal(
            "Ad soyad en fazla 100 karakter olabilir.",
            SingleError(ValidateWith("name", string.Concat(Enumerable.Repeat(FamilyEmoji, 21))), "name")); // 105
    }

    [Theory]
    [InlineData("Deniz\nÖrnek")]
    [InlineData("Deniz\tÖrnek")]
    [InlineData("Deniz\u0000Örnek")]
    public void Name_with_control_character_is_rejected(string name)
    {
        Assert.Equal("Ad soyad geçersiz karakter içeriyor.", SingleError(ValidateWith("name", name), "name"));
    }

    [Theory]
    [InlineData("deniz.ornek@example.com")]
    [InlineData("deniz+test@mail.example.com.tr")]
    [InlineData("d@e.co")]
    [InlineData("deniz@my-company.example.com")]
    [InlineData("deniz@123.example.com")]
    [InlineData("deniz@örnek.com.tr")]
    [InlineData("deniz@xn--rnek-zoa.com.tr")]
    public void Valid_email_is_accepted(string email)
    {
        Assert.NotNull(ValidateWith("email", email).Request);
    }

    [Theory]
    [InlineData("deniz")]
    [InlineData("deniz@")]
    [InlineData("@example.com")]
    [InlineData("deniz@example")]
    [InlineData("deniz ornek@example.com")]
    [InlineData("deniz@exa mple.com")]
    [InlineData("deniz@@example.com")]
    [InlineData("deniz@example..com")]
    [InlineData("deniz@.example.com")]
    [InlineData("deniz@example.com.")]
    [InlineData("deniz\u0000@example.com")]
    [InlineData("codex-review@exa/mple.com")]
    [InlineData("deniz@exa<mple.com")]
    [InlineData("deniz@example.com>")]
    [InlineData("deniz@exa_mple.com")]
    [InlineData("deniz@-example.com")]
    [InlineData("deniz@example-.com")]
    public void Malformed_email_is_rejected(string email)
    {
        Assert.Equal("Geçerli bir e-posta adresi girin.", SingleError(ValidateWith("email", email), "email"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Email_that_is_empty_after_trimming_is_required(string email)
    {
        Assert.Equal("E-posta zorunludur.", SingleError(ValidateWith("email", email), "email"));
    }

    [Fact]
    public void Email_may_be_254_characters_but_not_more()
    {
        const string domain = "@example.com";
        var longest = new string('a', 254 - domain.Length) + domain;

        Assert.NotNull(ValidateWith("email", longest).Request);
        Assert.Equal(
            "E-posta en fazla 254 karakter olabilir.",
            SingleError(ValidateWith("email", "a" + longest), "email"));
    }

    [Theory]
    [InlineData("workflow-automation")]
    [InlineData("api-integration")]
    [InlineData("ai-triage")]
    public void Each_service_code_is_accepted(string service)
    {
        Assert.Equal(service, ValidateWith("service", service).Request?.Service);
    }

    [Theory]
    [InlineData("consulting")]
    [InlineData("AI-TRIAGE")]
    [InlineData(" ai-triage")]
    [InlineData("ai-triage ")]
    public void Unknown_or_inexact_service_code_is_rejected(string service)
    {
        Assert.Equal("Geçerli bir hizmet seçin.", SingleError(ValidateWith("service", service), "service"));
    }

    [Fact]
    public void Empty_service_is_required()
    {
        Assert.Equal("Hizmet seçimi zorunludur.", SingleError(ValidateWith("service", ""), "service"));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(2000)]
    public void Description_length_limits_are_inclusive(int length)
    {
        Assert.NotNull(ValidateWith("description", new string('x', length)).Request);
    }

    [Fact]
    public void Description_shorter_than_10_characters_after_trimming_is_rejected()
    {
        Assert.Equal(
            "Açıklama en az 10 karakter olmalıdır.",
            SingleError(ValidateWith("description", "   123456789   "), "description"));
    }

    [Fact]
    public void Description_longer_than_2000_characters_is_rejected()
    {
        Assert.Equal(
            "Açıklama en fazla 2000 karakter olabilir.",
            SingleError(ValidateWith("description", new string('x', 2001)), "description"));
    }

    [Fact]
    public void Description_may_contain_tabs_and_line_breaks()
    {
        const string description = "Birinci satır\r\nİkinci satır\tsekme ile";

        Assert.Equal(description, ValidateWith("description", description).Request?.Description);
    }

    [Fact]
    public void Description_with_other_control_characters_is_rejected()
    {
        Assert.Equal(
            "Açıklama geçersiz karakter içeriyor.",
            SingleError(ValidateWith("description", "Kurgusal açıklama\u0000metni"), "description"));
    }

    private static ServiceRequestValidationResult ValidateWith(string field, JsonNode? value)
    {
        var body = ValidBody();
        body[field] = value;
        return Validate(body);
    }

    private static ServiceRequestValidationResult Validate(JsonObject body) => Validate(body.ToJsonString());

    private static ServiceRequestValidationResult Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ServiceRequestValidator.Validate(document.RootElement);
    }

    private static string SingleError(ServiceRequestValidationResult result, string field)
    {
        Assert.Null(result.Request);
        Assert.True(result.Errors.ContainsKey(field), $"Expected an error for '{field}'.");
        return Assert.Single(result.Errors[field]);
    }
}
