import { isValidRequestId, validate } from "./validation.js";

const FIELDS = ["name", "email", "service", "description"];
// How long the browser waits for the server before giving up on a submission.
const REQUEST_TIMEOUT_MS = 30_000;

const form = document.getElementById("request-form");
const submitButton = document.getElementById("submit-button");
const statusMessage = document.getElementById("form-status");
const alertMessage = document.getElementById("form-alert");
const submitLabel = submitButton.textContent;
let isSubmitting = false;

const readValues = () => Object.fromEntries(FIELDS.map((field) => [field, form.elements[field].value]));

function showFieldError(field, message) {
  const input = form.elements[field];
  const errorElement = document.getElementById(`${field}-error`);
  errorElement.textContent = message ?? "";
  errorElement.hidden = !message;
  if (message) {
    input.setAttribute("aria-invalid", "true");
  } else {
    input.removeAttribute("aria-invalid");
  }
}

function showFieldErrors(errors) {
  for (const field of FIELDS) {
    showFieldError(field, errors[field]);
  }
}

/** Moves focus to the first field with an error; returns false if there is none. */
function focusFirstInvalid(errors) {
  const field = FIELDS.find((name) => errors[name]);
  if (!field) return false;
  form.elements[field].focus();
  return true;
}

/** Shows one message: "status" for progress and success, "alert" for problems. */
function showMessage(kind, text) {
  statusMessage.textContent = kind === "status" ? text : "";
  alertMessage.textContent = kind === "alert" ? text : "";
}

function setSubmitting(value) {
  isSubmitting = value;
  // Lock the fields as well as the button: anything typed while waiting would
  // not be part of the request and would be lost when the form resets.
  for (const field of FIELDS) {
    form.elements[field].disabled = value;
  }
  submitButton.disabled = value;
  submitButton.textContent = value ? "Gönderiliyor…" : submitLabel;
  form.setAttribute("aria-busy", String(value));
}

function firstErrorPerField(serverErrors) {
  return Object.fromEntries(
    FIELDS.filter((field) => Array.isArray(serverErrors[field]) && serverErrors[field].length > 0)
      .map((field) => [field, String(serverErrors[field][0])]),
  );
}

// Seconds from a Retry-After header such as "60"; null if missing or not a number.
function retryAfterSeconds(response) {
  const seconds = Number.parseInt(response.headers.get("Retry-After") ?? "", 10);
  return Number.isFinite(seconds) && seconds > 0 ? seconds : null;
}

// Sends the request exactly once. There is deliberately no automatic retry:
// repeating a request whose response was lost could store it twice. After
// REQUEST_TIMEOUT_MS the browser stops waiting; the server may still finish.
async function sendRequest(values) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
  try {
    let response;
    try {
      response = await fetch("/api/requests", {
        method: "POST",
        headers: { "Content-Type": "application/json", Accept: "application/json" },
        body: JSON.stringify(values),
        signal: controller.signal,
      });
    } catch {
      return { outcome: controller.signal.aborted ? "timeout" : "network-error" };
    }

    let body = null;
    try {
      body = await response.json();
    } catch {
      // The timeout can also fire while the body is still arriving.
      if (controller.signal.aborted) return { outcome: "timeout" };
    }

    // Success requires both the 201 status and a well-formed requestId.
    if (response.status === 201 && isValidRequestId(body?.requestId)) {
      return { outcome: "saved", requestId: body.requestId };
    }
    if (response.status === 400 && body?.errors) {
      return { outcome: "invalid", errors: firstErrorPerField(body.errors) };
    }
    if (response.status === 429) {
      return { outcome: "rate-limited", retryAfter: retryAfterSeconds(response) };
    }
    if (response.status === 413) {
      return { outcome: "too-large" };
    }
    return { outcome: "failed", status: response.status, title: typeof body?.title === "string" ? body.title : null };
  } finally {
    clearTimeout(timeout);
  }
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  if (isSubmitting) return;

  const values = readValues();
  const errors = validate(values);
  showFieldErrors(errors);
  if (Object.keys(errors).length > 0) {
    showMessage("alert", "Lütfen işaretli alanları düzeltin.");
    focusFirstInvalid(errors);
    return;
  }

  setSubmitting(true);
  showMessage("status", "Talebiniz gönderiliyor…");
  let result;
  try {
    result = await sendRequest(values);
  } finally {
    setSubmitting(false);
  }

  // Except after success, the entered values stay in the form.
  switch (result.outcome) {
    case "saved":
      form.reset();
      showMessage("status", `Talebiniz kaydedildi. Talep numaranız: ${result.requestId}`);
      statusMessage.focus();
      break;
    case "invalid":
      showFieldErrors(result.errors);
      showMessage("alert", "Lütfen işaretli alanları düzeltin.");
      if (!focusFirstInvalid(result.errors)) alertMessage.focus();
      break;
    case "network-error":
      // The request may or may not have reached the server, so do not claim either.
      showMessage(
        "alert",
        "Sunucuya ulaşılamadı veya yanıt alınamadı. Talebinizin kaydedilip kaydedilmediğini doğrulayamıyoruz. " +
          "Bilgileriniz formda duruyor; yeniden göndermeden önce bağlantınızı kontrol edin.",
      );
      alertMessage.focus();
      break;
    case "timeout":
      // Giving up in the browser does not cancel the request on the server.
      showMessage(
        "alert",
        `Sunucudan ${REQUEST_TIMEOUT_MS / 1000} saniye içinde yanıt alınamadı. ` +
          "Talebinizin kaydedilip kaydedilmediğini bilmiyoruz. Bilgileriniz formda duruyor; " +
          "yeniden göndermeden önce biraz bekleyin.",
      );
      alertMessage.focus();
      break;
    case "rate-limited":
      // 429 comes from the rate limiter before the request is handled, so nothing was saved.
      showMessage(
        "alert",
        "Kısa sürede çok fazla talep gönderildi; talebiniz kaydedilmedi. " +
          (result.retryAfter ? `Lütfen yaklaşık ${result.retryAfter} saniye sonra` : "Lütfen biraz sonra") +
          " tekrar deneyin. Bilgileriniz formda duruyor.",
      );
      alertMessage.focus();
      break;
    case "too-large":
      showMessage(
        "alert",
        "Gönderilen bilgiler izin verilen boyutu aşıyor; talebiniz kaydedilmedi. " +
          "Lütfen açıklamayı kısaltıp tekrar deneyin.",
      );
      alertMessage.focus();
      break;
    default:
      showMessage(
        "alert",
        result.title ??
          `Sunucudan beklenmeyen bir yanıt alındı (HTTP ${result.status}). ` +
            "Talebinizin kaydedilip kaydedilmediğini doğrulayamıyoruz.",
      );
      alertMessage.focus();
  }
});

// Once a field shows an error, check it again while the user corrects it.
form.addEventListener("input", (event) => {
  const field = event.target.name;
  if (FIELDS.includes(field) && form.elements[field].hasAttribute("aria-invalid")) {
    showFieldError(field, validate(readValues())[field]);
  }
});
