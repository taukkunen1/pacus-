let hideTimer = null;

export function showToast(message, { error = false } = {}) {
  let el = document.getElementById("toast");
  if (!el) {
    el = document.createElement("div");
    el.id = "toast";
    el.className = "toast";
    document.body.appendChild(el);
  }

  el.textContent = message;
  el.classList.toggle("is-error", error);
  el.classList.add("is-visible");

  clearTimeout(hideTimer);
  hideTimer = setTimeout(() => el.classList.remove("is-visible"), 2600);
}
