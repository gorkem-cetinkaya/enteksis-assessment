import { isValidRequestId, validate } from "./validation.js";

const FIELDS = ["name", "email", "service", "description"];

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

// Sends the request exactly once. There is deliberately no automatic retry:
// repeating a request whose response was lost could store it twice.
async function sendRequest(values) {
  let response;
  try {
    response = await fetch("/api/requests", {
      method: "POST",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: JSON.stringify(values),
    });
  } catch {
    return { outcome: "network-error" };
  }

  const body = await response.json().catch(() => null);

  // Success requires both the 201 status and a well-formed requestId.
  if (response.status === 201 && isValidRequestId(body?.requestId)) {
    return { outcome: "saved", requestId: body.requestId };
  }
  if (response.status === 400 && body?.errors) {
    return { outcome: "invalid", errors: firstErrorPerField(body.errors) };
  }
  return { outcome: "failed", status: response.status, title: typeof body?.title === "string" ? body.title : null };
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
