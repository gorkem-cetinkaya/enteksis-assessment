// Tests for the browser copy of the validation rules (wwwroot/validation.js).
// Run from the repository root: node --test tests/client/validation.test.mjs
// The expected messages are the same as in ServiceRequestValidatorTests.cs,
// so a rule changed on only one side shows up as a failing test.
import { test } from "node:test";
import assert from "node:assert/strict";
import { isValidRequestId, validate } from "../../src/ServiceRequests.Web/wwwroot/validation.js";

// U+1F600: one code point, but two UTF-16 code units in a JavaScript string.
const EMOJI = "\u{1F600}";
// Family emoji: three emoji joined by two zero-width joiners (U+200D), so
// five code points although it is drawn as one symbol.
const FAMILY_EMOJI = "\u{1F468}‍\u{1F469}‍\u{1F467}";

// All test data is fictional; example.com is a reserved domain.
const validValues = () => ({
  name: "Deniz Örnek",
  email: "deniz.ornek@example.com",
  service: "workflow-automation",
  description: "Gelen talepleri e-postadan Excel'e elle aktarıyoruz.",
});

const validateWith = (field, value) => validate({ ...validValues(), [field]: value });
const label = (value) => JSON.stringify(value).slice(0, 40);

test("valid values pass and surrounding whitespace is ignored", () => {
  const values = {
    name: "  Deniz Örnek \t",
    email: " deniz.ornek@example.com\n",
    service: "ai-triage",
    description: "\n  Gelen talepleri e-postadan Excel'e elle aktarıyoruz.  ",
  };

  assert.deepEqual(validate(values), {});
});

test("every invalid field is reported at once", () => {
  const errors = validate({ name: "A", email: "deniz", service: "consulting", description: "kısa" });

  assert.deepEqual(Object.keys(errors).sort(), ["description", "email", "name", "service"]);
});

const rejected = [
  ["name", "", "Ad soyad zorunludur."],
  ["name", "   ", "Ad soyad zorunludur."],
  ["name", "  D  ", "Ad soyad en az 2 karakter olmalıdır."],
  ["name", "a".repeat(101), "Ad soyad en fazla 100 karakter olabilir."],
  ["name", EMOJI, "Ad soyad en az 2 karakter olmalıdır."],
  ["name", FAMILY_EMOJI.repeat(21), "Ad soyad en fazla 100 karakter olabilir."],
  ["name", "Deniz\nÖrnek", "Ad soyad geçersiz karakter içeriyor."],
  ["name", "Deniz\tÖrnek", "Ad soyad geçersiz karakter içeriyor."],
  ["name", "Deniz\u0000Örnek", "Ad soyad geçersiz karakter içeriyor."],
  ["name", "Deniz \ud800", "Ad soyad geçersiz karakter içeriyor."],
  ["email", "", "E-posta zorunludur."],
  ["email", "  ", "E-posta zorunludur."],
  ["email", "a".repeat(64) + "@b" + "b".repeat(63) + "." + "c".repeat(63) + "." + "d".repeat(57) + ".com", "E-posta en fazla 254 karakter olabilir."],
  ["email", "a".repeat(65) + "@example.com", "E-postanın @ işaretinden önceki kısmı en fazla 64 karakter olabilir."],
  ...[
    "deniz",
    "deniz@",
    "@example.com",
    "deniz@example",
    "deniz ornek@example.com",
    "deniz@exa mple.com",
    "deniz@@example.com",
    "deniz@example..com",
    "deniz@.example.com",
    "deniz@example.com.",
    "deniz\u0000@example.com",
    "codex-review@exa/mple.com",
    "deniz@exa<mple.com",
    "deniz@example.com>",
    "deniz@exa_mple.com",
    "deniz@-example.com",
    "deniz@example-.com",
    "codex<phase2>@example.com",
    ".deniz@example.com",
    "deniz.@example.com",
    "de..niz@example.com",
    "deniz\n@example.com",
    "deniz(yorum)@example.com",
    "deniz,ornek@example.com",
    "\"deniz\"@example.com", // quoted local parts are out of scope
    "dеniz@example.com", // Cyrillic "е": non-ASCII local parts are out of scope
    "deniz@\u{1F600}.example", // an emoji is not a letter
  ].map((email) => ["email", email, "Geçerli bir e-posta adresi girin."]),
  ["service", "", "Hizmet seçimi zorunludur."],
  ...["consulting", "AI-TRIAGE", " ai-triage", "ai-triage "].map((service) => [
    "service",
    service,
    "Geçerli bir hizmet seçin.",
  ]),
  ["description", "   123456789   ", "Açıklama en az 10 karakter olmalıdır."],
  ["description", "x".repeat(2001), "Açıklama en fazla 2000 karakter olabilir."],
  ["description", "Kurgusal açıklama\u0000metni", "Açıklama geçersiz karakter içeriyor."],
];

for (const [field, value, expected] of rejected) {
  test(`${field} rejects ${label(value)}`, () => {
    assert.deepEqual(validateWith(field, value), { [field]: expected });
  });
}

const accepted = [
  ["name", "aa"],
  ["name", "a".repeat(100)],
  ["name", EMOJI.repeat(100)],
  ["name", FAMILY_EMOJI.repeat(20)],
  ["email", "deniz+test@mail.example.com.tr"],
  ["email", "d@e.co"],
  ["email", "deniz@my-company.example.com"],
  ["email", "deniz@123.example.com"],
  ["email", "deniz@örnek.com.tr"],
  ["email", "deniz@xn--rnek-zoa.com.tr"],
  ["email", "a".repeat(64) + "@" + "b".repeat(63) + "." + "c".repeat(63) + "." + "d".repeat(57) + ".com"],
  ["email", "a".repeat(64) + "@example.com"],
  ["email", "o'brien@example.com"],
  ["email", "deniz_ornek@example.com"],
  ["email", "deniz-ornek.test+etiket@example.com"],
  ["email", "a!#$%&'*+/=?^_`{|}~-z@example.com"],
  ["email", "codex@\u{10400}.example"], // U+10400 is a letter outside the BMP
  ["service", "workflow-automation"],
  ["service", "api-integration"],
  ["service", "ai-triage"],
  ["description", "x".repeat(10)],
  ["description", "x".repeat(2000)],
  ["description", "Birinci satır\r\nİkinci satır\tsekme ile"],
];

for (const [field, value] of accepted) {
  test(`${field} accepts ${label(value)}`, () => {
    assert.deepEqual(validateWith(field, value), {});
  });
}

test("only a UUID counts as a request id", () => {
  assert.equal(isValidRequestId("2495089e-a979-48fa-8506-4036331bde2a"), true);
  for (const value of [undefined, null, 42, "", "42", "2495089e-a979-48fa-8506"]) {
    assert.equal(isValidRequestId(value), false);
  }
});
