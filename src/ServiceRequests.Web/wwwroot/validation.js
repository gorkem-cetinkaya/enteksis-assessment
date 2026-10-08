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
  descriptionMin: 10,
  descriptionMax: 2000,
});

export const SERVICE_CODES = Object.freeze(["workflow-automation", "api-integration", "ai-triage"]);

const EDGE_WHITESPACE = /^\p{White_Space}+|\p{White_Space}+$/gu;
const CONTROL_CHARACTER = /\p{Cc}/u;
// One domain label: letters or digits, with hyphens only inside
// ("mail", "my-company", "örnek", "xn--rnek-zoa").
const DOMAIN_LABEL = String.raw`[\p{L}\p{Nd}](?:[\p{L}\p{Nd}-]*[\p{L}\p{Nd}])?`;
// local@domain. The local part may not contain whitespace, control characters
// or a second "@". The domain is two or more labels joined by dots, so "/",
// "<", ">", "_" and empty labels are rejected. Same pattern as the server.
const EMAIL_PATTERN = new RegExp(
  String.raw`^[^\p{White_Space}@\p{Cc}]+@(?:${DOMAIN_LABEL}\.)+${DOMAIN_LABEL}$`,
  "u",
);
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

function checkEmail(value) {
  if (value.length === 0) return "E-posta zorunludur.";
  if (countCharacters(value) > LIMITS.emailMax) return `E-posta en fazla ${LIMITS.emailMax} karakter olabilir.`;
  return EMAIL_PATTERN.test(value) ? null : "Geçerli bir e-posta adresi girin.";
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
