import { loginAdult, loginChild } from "../api/auth-api.js";

const CHILD_PROFILE_KEY = "pacus.child.profileId"; // so um id, nao e credencial — ok em localStorage

export function renderLogin(root, onSuccess) {
  let mode = "adult"; // "adult" | "child"
  let pin = "";
  let submitting = false;

  root.innerHTML = `
    <div class="screen login-screen">
      <div class="login-card">
        <h1 class="login-brand">Pacus</h1>
        <p class="login-tagline">Sua rotina, seu bichinho de estimacao.</p>

        <div class="role-switch" role="tablist">
          <button type="button" data-role="adult" class="is-active" role="tab">Adulto</button>
          <button type="button" data-role="child" role="tab">Crianca</button>
        </div>

        <div id="login-form-slot"></div>
      </div>
    </div>
  `;

  const slot = root.querySelector("#login-form-slot");
  const roleButtons = root.querySelectorAll(".role-switch button");

  function renderAdultForm() {
    slot.innerHTML = `
      <form class="login-form" id="adult-form">
        <div class="field">
          <label for="email">Email</label>
          <input id="email" name="email" type="email" autocomplete="username" required />
        </div>
        <div class="field">
          <label for="password">Senha</label>
          <input id="password" name="password" type="password" autocomplete="current-password" required />
        </div>
        <p class="error-text hidden" id="adult-error"></p>
        <button type="submit" class="btn btn-primary btn-block">Entrar</button>
      </form>
    `;

    slot.querySelector("#adult-form").addEventListener("submit", async (event) => {
      event.preventDefault();
      if (submitting) return;
      const email = slot.querySelector("#email").value.trim();
      const password = slot.querySelector("#password").value;
      const errorEl = slot.querySelector("#adult-error");
      errorEl.classList.add("hidden");

      submitting = true;
      try {
        const result = await loginAdult(email, password);
        onSuccess(result);
      } catch (err) {
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      } finally {
        submitting = false;
      }
    });
  }

  function renderChildForm() {
    const savedProfileId = localStorage.getItem(CHILD_PROFILE_KEY) || "";

    slot.innerHTML = `
      <div class="login-form">
        <div class="pin-dots" id="pin-dots"></div>
        <div class="pin-keypad" id="pin-keypad"></div>
        <p class="error-text hidden" id="child-error"></p>
        <button type="button" class="btn btn-ghost btn-block" id="change-profile-btn">Trocar perfil</button>
        <div class="field hidden" id="profile-field">
          <label for="profile-id">Id do perfil</label>
          <input id="profile-id" type="text" value="${savedProfileId}" placeholder="cole o id do seu perfil" />
        </div>
      </div>
    `;

    const dotsEl = slot.querySelector("#pin-dots");
    const keypadEl = slot.querySelector("#pin-keypad");
    const errorEl = slot.querySelector("#child-error");
    const profileField = slot.querySelector("#profile-field");
    const profileInput = slot.querySelector("#profile-id");

    function renderDots() {
      dotsEl.innerHTML = Array.from({ length: 4 })
        .map((_, i) => `<span class="pin-dot ${i < pin.length ? "is-filled" : ""}"></span>`)
        .join("");
    }

    function renderKeys() {
      const keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "apagar", "0", "ok"];
      keypadEl.innerHTML = keys
        .map((key) => {
          if (key === "apagar" || key === "ok") {
            return `<button type="button" class="pin-key pin-key--action" data-key="${key}">${key === "apagar" ? "⌫" : "OK"}</button>`;
          }
          return `<button type="button" class="pin-key" data-key="${key}">${key}</button>`;
        })
        .join("");
    }

    async function trySubmit() {
      const profileId = profileInput.value.trim() || savedProfileId;
      if (!profileId) {
        errorEl.textContent = "Informe o id do perfil primeiro (toque em \"Trocar perfil\").";
        errorEl.classList.remove("hidden");
        profileField.classList.remove("hidden");
        return;
      }
      if (pin.length === 0) return;

      errorEl.classList.add("hidden");
      try {
        const result = await loginChild(profileId, pin);
        localStorage.setItem(CHILD_PROFILE_KEY, profileId);
        onSuccess(result);
      } catch (err) {
        pin = "";
        renderDots();
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      }
    }

    keypadEl.addEventListener("click", (event) => {
      const key = event.target.closest(".pin-key")?.dataset.key;
      if (!key) return;

      if (key === "apagar") {
        pin = pin.slice(0, -1);
      } else if (key === "ok") {
        trySubmit();
        return;
      } else if (pin.length < 4) {
        pin += key;
      }
      renderDots();
      if (pin.length === 4) trySubmit();
    });

    slot.querySelector("#change-profile-btn").addEventListener("click", () => {
      profileField.classList.toggle("hidden");
    });

    renderDots();
    renderKeys();
  }

  roleButtons.forEach((btn) => {
    btn.addEventListener("click", () => {
      mode = btn.dataset.role;
      pin = "";
      roleButtons.forEach((b) => b.classList.toggle("is-active", b === btn));
      mode === "adult" ? renderAdultForm() : renderChildForm();
    });
  });

  renderAdultForm();
}
