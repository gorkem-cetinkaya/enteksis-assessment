// Browser copy of the rules in Requests/ServiceRequestValidator.cs. The server
// always validates again and is the authority; keep the two files in sync.
//
// - Trimming removes Unicode whitespace from both ends. \p{White_Space} is the
//   same character set as .NET's string.Trim() and regex \s.
// - Lengths count Unicode code points, like the server and PostgreSQL's
//   char_length(). "😀" is one code point; a combined emoji such as a family
//   (people joined by zero-width joiners) is several.

export const LIMITS = Object.freeze({
  nameMin: 2,
  nameMax: 100,
  emailMax: 254,
  emailLocalPartMax: 64,
  descriptionMin: 10,
  descriptionMax: 2000,
});

export const SERVICE_CODES = Object.freeze(["workflow-automation", "api-integration", "ai-triage"]);

const EDGE_WHITESPACE = /^\p{White_Space}+|\p{White_Space}+$/gu;
const CONTROL_CHARACTER = /\p{Cc}/u;
// E-mail = local part "@" domain; a format check only, it does not prove that
// the address exists. Same rules as the server.
//
// Local part: ASCII "dot-atom" (RFC 5322). One or more non-empty parts joined
// by dots, each made of letters, digits and ! # $ % & ' * + - / = ? ^ _ ` { | } ~.
// Quoted and non-ASCII local parts are out of scope.
const EMAIL_LOCAL_PART_PATTERN = /^[A-Za-z0-9!#$%&'*+\/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+\/=?^_`{|}~-]+)*$/;
// One code point that is a letter or a decimal digit of any script.
const LETTER_OR_DIGIT = /^[\p{L}\p{Nd}]$/u;
const INVALID_EMAIL_MESSAGE = "Geçerli bir e-posta adresi girin.";
const REQUEST_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

const trim = (value) => value.replace(EDGE_WHITESPACE, "");
const countCharacters = (value) => [...value].length;

// Text with a lone surrogate cannot be stored; the server rejects it as well.
const isWellFormed = (value) => typeof value.isWellFormed !== "function" || value.isWellFormed();

function checkText(value, label, minLength, maxLength, allowLineBreaks) {
  if (value.length === 0) return `${label} zorunludur.`;

  // Control characters are rejected; the description may contain tabs and line breaks.
  const hasDisallowedControl = [...value].some(
    (character) => CONTROL_CHARACTER.test(character) && !(allowLineBreaks && "\t\n\r".includes(character)),
  );
  if (hasDisallowedControl) return `${label} geçersiz karakter içeriyor.`;

  const length = countCharacters(value);
  if (length < minLength) return `${label} en az ${minLength} karakter olmalıdır.`;
  if (length > maxLength) return `${label} en fazla ${maxLength} karakter olabilir.`;
  return null;
}

// A domain label is letters or decimal digits of any script, with hyphens only
// inside ("mail", "my-company", "örnek", "xn--rnek-zoa"); checked per code point.
function isValidDomainLabel(label) {
  const characters = [...label];
  return (
    characters.length > 0 &&
    characters.every(
      (character, index) =>
        LETTER_OR_DIGIT.test(character) || (character === "-" && index > 0 && index < characters.length - 1),
    )
  );
}

// Domain: two or more labels joined by dots ("mail.example.com.tr").
function isValidDomain(domain) {
  const labels = domain.split(".");
  return labels.length >= 2 && labels.every(isValidDomainLabel);
}

function checkEmail(value) {
  if (value.length === 0) return "E-posta zorunludur.";
  if (countCharacters(value) > LIMITS.emailMax) return `E-posta en fazla ${LIMITS.emailMax} karakter olabilir.`;

  // "@" is not allowed in either part, so a valid address splits into exactly two.
  const parts = value.split("@");
  if (parts.length !== 2) return INVALID_EMAIL_MESSAGE;

  const [localPart, domain] = parts;
  if (countCharacters(localPart) > LIMITS.emailLocalPartMax) {
    return `E-postanın @ işaretinden önceki kısmı en fazla ${LIMITS.emailLocalPartMax} karakter olabilir.`;
  }

  return EMAIL_LOCAL_PART_PATTERN.test(localPart) && isValidDomain(domain) ? null : INVALID_EMAIL_MESSAGE;
}

function checkService(value) {
  if (value.length === 0) return "Hizmet seçimi zorunludur.";
  return SERVICE_CODES.includes(value) ? null : "Geçerli bir hizmet seçin.";
}

/**
 * Checks the raw form values (strings) and returns { field: message } for each
 * invalid field. An empty object means the values can be sent.
 */
export function validate({ name, email, service, description }) {
  const errors = {
    name: isWellFormed(name)
      ? checkText(trim(name), "Ad soyad", LIMITS.nameMin, LIMITS.nameMax, false)
      : "Ad soyad geçersiz karakter içeriyor.",
    email: isWellFormed(email) ? checkEmail(trim(email)) : "E-posta geçersiz karakter içeriyor.",
    // Service codes are compared exactly, without trimming.
    service: isWellFormed(service) ? checkService(service) : "Hizmet seçimi geçersiz karakter içeriyor.",
    description: isWellFormed(description)
      ? checkText(trim(description), "Açıklama", LIMITS.descriptionMin, LIMITS.descriptionMax, true)
      : "Açıklama geçersiz karakter içeriyor.",
  };

  return Object.fromEntries(Object.entries(errors).filter(([, message]) => message !== null));
}

export const isValidRequestId = (value) => typeof value === "string" && REQUEST_ID_PATTERN.test(value);
