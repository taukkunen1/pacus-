import { loginAdult, loginChild, resetAdultPassword } from "../api/auth-api.js";
import { getFamilyChildren } from "../api/family-api.js";
import { withSlowLoadHint, SLOW_LOAD_MESSAGE } from "../utils/slow-load-hint.js";

const CHILD_PROFILE_KEY = "pacus.child.profileId"; // so um id, nao e credencial — ok em localStorage
const CHILDREN_CACHE_KEY = "pacus.family.children"; // so nome + id de cada crianca — mesmo motivo

function getCachedChildren() {
  try {
    const raw = localStorage.getItem(CHILDREN_CACHE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function cacheFamilyChildren() {
  try {
    const children = await getFamilyChildren();
    localStorage.setItem(CHILDREN_CACHE_KEY, JSON.stringify(children));
  } catch {
    // Melhor esforco — se falhar, a tela de crianca cai no fallback manual.
  }
}

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
        <button type="button" class="btn btn-ghost btn-block" id="forgot-password-btn">Esqueci minha senha</button>
      </form>
    `;

    slot.querySelector("#adult-form").addEventListener("submit", async (event) => {
      event.preventDefault();
      if (submitting) return;
      const email = slot.querySelector("#email").value.trim();
      const password = slot.querySelector("#password").value;
      const errorEl = slot.querySelector("#adult-error");
      const submitBtn = slot.querySelector('#adult-form button[type="submit"]');
      const originalLabel = submitBtn.textContent;
      errorEl.classList.add("hidden");

      submitting = true;
      submitBtn.disabled = true;
      submitBtn.textContent = "Entrando...";
      try {
        const result = await withSlowLoadHint(
          loginAdult(email, password),
          () => { submitBtn.textContent = "Ainda conectando..."; errorEl.textContent = SLOW_LOAD_MESSAGE; errorEl.classList.remove("hidden", "error-text"); errorEl.classList.add("hint-text"); }
        );
        await cacheFamilyChildren();
        onSuccess(result);
      } catch (err) {
        errorEl.classList.remove("hint-text");
        errorEl.classList.add("error-text");
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      } finally {
        submitting = false;
        submitBtn.disabled = false;
        submitBtn.textContent = originalLabel;
      }
    });

    slot.querySelector("#forgot-password-btn").addEventListener("click", renderForgotPasswordForm);
  }

  // "Esqueci minha senha" -- sem provedor de e-mail configurado, usa o codigo de
  // recuperacao mostrado uma unica vez no cadastro da familia (ver BootstrapService).
  function renderForgotPasswordForm() {
    slot.innerHTML = `
      <form class="login-form" id="forgot-form">
        <p class="profile-picker-hint">Use o código de recuperação que você guardou ao criar a família.</p>
        <div class="field">
          <label for="reset-email">Email</label>
          <input id="reset-email" name="email" type="email" autocomplete="username" required />
        </div>
        <div class="field">
          <label for="reset-code">Código de recuperação</label>
          <input id="reset-code" name="code" type="text" autocomplete="off" required />
        </div>
        <div class="field">
          <label for="reset-password">Nova senha</label>
          <input id="reset-password" name="newPassword" type="password" autocomplete="new-password" required />
        </div>
        <p class="error-text hidden" id="forgot-error"></p>
        <p class="hidden" id="forgot-success"></p>
        <button type="submit" class="btn btn-primary btn-block">Redefinir senha</button>
        <button type="button" class="btn btn-ghost btn-block" id="back-to-login-btn">Voltar</button>
      </form>
    `;

    slot.querySelector("#back-to-login-btn").addEventListener("click", renderAdultForm);

    slot.querySelector("#forgot-form").addEventListener("submit", async (event) => {
      event.preventDefault();
      if (submitting) return;

      const email = slot.querySelector("#reset-email").value.trim();
      const code = slot.querySelector("#reset-code").value.trim();
      const newPassword = slot.querySelector("#reset-password").value;
      const errorEl = slot.querySelector("#forgot-error");
      const successEl = slot.querySelector("#forgot-success");
      errorEl.classList.add("hidden");
      successEl.classList.add("hidden");

      const resetBtn = slot.querySelector("#forgot-form button[type=submit]");
      const resetOriginalLabel = resetBtn.textContent;

      submitting = true;
      resetBtn.disabled = true;
      resetBtn.textContent = "Enviando...";
      try {
        const result = await withSlowLoadHint(
          resetAdultPassword(email, code, newPassword),
          () => { resetBtn.textContent = "Ainda conectando..."; errorEl.textContent = SLOW_LOAD_MESSAGE; errorEl.classList.remove("hidden", "error-text"); errorEl.classList.add("hint-text"); }
        );
        errorEl.classList.add("hidden");
        successEl.textContent = `Senha redefinida! Seu novo código de recuperação é: ${result.newRecoveryCode} — guarde em lugar seguro, ele substitui o anterior.`;
        successEl.classList.remove("hidden");
        resetBtn.disabled = true;
        resetBtn.textContent = resetOriginalLabel;
        return;
      } catch (err) {
        errorEl.classList.remove("hint-text");
        errorEl.classList.add("error-text");
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
        resetBtn.disabled = false;
        resetBtn.textContent = resetOriginalLabel;
      } finally {
        submitting = false;
      }
    });
  }

  function renderChildForm() {
    const cachedChildren = getCachedChildren();
    if (cachedChildren.length > 0) {
      renderProfilePicker(cachedChildren);
    } else {
      renderManualProfileEntry();
    }
  }

  // Fluxo principal: a crianca so toca no proprio nome. A lista vem do cache
  // populado no ultimo login de um adulto neste aparelho (ver cacheFamilyChildren).
  function renderProfilePicker(children) {
    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Quem e voce?</p>
        <div class="profile-picker" id="profile-picker">
          ${children
            .map(
              (child) =>
                `<button type="button" class="profile-picker__item" data-id="${child.id}" data-name="${child.name}">${child.name}</button>`
            )
            .join("")}
        </div>
        <button type="button" class="btn btn-ghost btn-block" id="use-id-instead-btn">Nao encontrou seu nome?</button>
      </div>
    `;

    slot.querySelector("#profile-picker").addEventListener("click", (event) => {
      const btn = event.target.closest(".profile-picker__item");
      if (!btn) return;
      renderPinEntry({ id: btn.dataset.id, name: btn.dataset.name }, { showBackToPicker: true });
    });

    slot.querySelector("#use-id-instead-btn").addEventListener("click", () => {
      renderManualProfileEntry();
    });
  }

  // Fallback: usado so quando nenhum adulto ainda logou neste aparelho (por isso
  // nao ha nomes em cache) ou se a crianca nao se encontrar na lista.
  function renderManualProfileEntry() {
    const savedProfileId = localStorage.getItem(CHILD_PROFILE_KEY) || "";

    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Peca para um adulto entrar uma vez neste aparelho — assim seu nome aparece na lista da proxima vez.</p>
        <div class="field">
          <label for="profile-id">Id do perfil</label>
          <input id="profile-id" type="text" value="${savedProfileId}" placeholder="cole o id do seu perfil" />
        </div>
        <p class="error-text hidden" id="manual-error"></p>
        <button type="button" class="btn btn-primary btn-block" id="manual-continue-btn">Continuar</button>
      </div>
    `;

    const errorEl = slot.querySelector("#manual-error");
    const profileInput = slot.querySelector("#profile-id");

    slot.querySelector("#manual-continue-btn").addEventListener("click", () => {
      const profileId = profileInput.value.trim();
      if (!profileId) {
        errorEl.textContent = "Cole o id do perfil para continuar.";
        errorEl.classList.remove("hidden");
        return;
      }
      renderPinEntry({ id: profileId, name: "" }, { showBackToPicker: false });
    });
  }

  function renderPinEntry(child, { showBackToPicker }) {
    slot.innerHTML = `
      <div class="login-form">
        ${child.name ? `<p class="profile-picker-hint">Ola, ${child.name}! Digite seu PIN.</p>` : ""}
        <div class="pin-dots" id="pin-dots"></div>
        <div class="pin-keypad" id="pin-keypad"></div>
        <p class="error-text hidden" id="child-error"></p>
        <button type="button" class="btn btn-ghost btn-block" id="back-btn">
          ${showBackToPicker ? "Nao sou eu" : "Voltar"}
        </button>
      </div>
    `;

    pin = "";
    const dotsEl = slot.querySelector("#pin-dots");
    const keypadEl = slot.querySelector("#pin-keypad");
    const errorEl = slot.querySelector("#child-error");

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
      if (pin.length === 0 || submitting) return;

      errorEl.classList.add("hidden");
      submitting = true;
      try {
        const result = await withSlowLoadHint(
          loginChild(child.id, pin),
          () => { errorEl.textContent = SLOW_LOAD_MESSAGE; errorEl.classList.remove("hidden", "error-text"); errorEl.classList.add("hint-text"); }
        );
        localStorage.setItem(CHILD_PROFILE_KEY, child.id);
        onSuccess(result);
      } catch (err) {
        pin = "";
        renderDots();
        errorEl.classList.remove("hint-text");
        errorEl.classList.add("error-text");
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      } finally {
        submitting = false;
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

    slot.querySelector("#back-btn").addEventListener("click", () => {
      renderChildForm();
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
