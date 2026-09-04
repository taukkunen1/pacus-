import { loginAdult, loginChild, resetAdultPassword } from "../api/auth-api.js";
import { getFamilyChildren, getChildrenByFamilyCode, getFamilyCode } from "../api/family-api.js";
import { createFamily } from "../api/bootstrap-api.js";
import { withSlowLoadHint, SLOW_LOAD_MESSAGE } from "../utils/slow-load-hint.js";

const CHILD_PROFILE_KEY = "pacus.child.profileId"; // so um id, nao e credencial — ok em localStorage

// Codigo curto da familia (ex.: "K7Q-X9F", ver User.FamilyCode no backend) --
// e o que a crianca digita pra achar a familia num aparelho novo, no lugar de
// colar um ObjectId cru. So e pedido uma vez por aparelho: depois de digitado
// (ou apos um adulto logar aqui, ver cacheFamilyChildren), fica salvo aqui e a
// tela pula direto pra lista de nomes.
const FAMILY_CODE_KEY = "pacus.family.code";

// Chamado apos um login de adulto neste aparelho -- alem de popular a lista de
// nomes (como ja fazia), agora tambem guarda o codigo da familia, entao se uma
// crianca for logar depois no mesmo aparelho ela nem precisa digitar o codigo.
async function cacheFamilyChildren() {
  try {
    const [children, codeResult] = await Promise.all([getFamilyChildren(), getFamilyCode()]);
    if (codeResult?.familyCode) localStorage.setItem(FAMILY_CODE_KEY, codeResult.familyCode);
  } catch {
    // Melhor esforco — se falhar, a tela de crianca cai no fluxo de digitar o codigo.
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
        <button type="button" class="btn btn-ghost btn-block" id="register-btn">Criar uma família</button>
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
    slot.querySelector("#register-btn").addEventListener("click", renderRegisterForm);
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

  // Cadastro inicial da familia (1 adulto + 1 crianca) -- so escopo do bootstrap
  // ja existente no backend; adicionar uma segunda crianca depois ficou fora
  // deste recurso por decisao explicita.
  function renderRegisterForm() {
    slot.innerHTML = `
      <form class="login-form" id="register-form">
        <div class="field">
          <label for="reg-adult-name">Seu nome</label>
          <input id="reg-adult-name" type="text" autocomplete="name" required />
        </div>
        <div class="field">
          <label for="reg-adult-email">Email</label>
          <input id="reg-adult-email" type="email" autocomplete="username" required />
        </div>
        <div class="field">
          <label for="reg-adult-password">Senha (mínimo 8 caracteres)</label>
          <input id="reg-adult-password" type="password" autocomplete="new-password" minlength="8" required />
        </div>
        <div class="field">
          <label for="reg-child-name">Nome da criança</label>
          <input id="reg-child-name" type="text" required />
        </div>
        <div class="field">
          <label for="reg-child-pin">PIN da criança (4 dígitos)</label>
          <input id="reg-child-pin" type="text" inputmode="numeric" pattern="[0-9]{4}" maxlength="4" required />
        </div>
        <label class="consent-check">
          <input id="reg-responsible-consent" type="checkbox" required />
          <span>Confirmo que sou responsável pela criança e autorizo o tratamento dos dados necessários para usar o PACUS.</span>
        </label>
        <p class="error-text hidden" id="register-error"></p>
        <button type="submit" class="btn btn-primary btn-block">Criar família</button>
        <button type="button" class="btn btn-ghost btn-block" id="back-to-login-from-register-btn">Voltar</button>
      </form>
    `;

    slot.querySelector("#back-to-login-from-register-btn").addEventListener("click", renderAdultForm);

    slot.querySelector("#register-form").addEventListener("submit", async (event) => {
      event.preventDefault();
      if (submitting) return;

      const adultName = slot.querySelector("#reg-adult-name").value.trim();
      const adultEmail = slot.querySelector("#reg-adult-email").value.trim();
      const adultPassword = slot.querySelector("#reg-adult-password").value;
      const childName = slot.querySelector("#reg-child-name").value.trim();
      const childPin = slot.querySelector("#reg-child-pin").value.trim();
      const responsibleConsent = slot.querySelector("#reg-responsible-consent").checked;
      const errorEl = slot.querySelector("#register-error");
      const submitBtn = slot.querySelector('#register-form button[type="submit"]');
      const originalLabel = submitBtn.textContent;
      errorEl.classList.add("hidden");

      submitting = true;
      submitBtn.disabled = true;
      submitBtn.textContent = "Criando...";
      try {
        const result = await withSlowLoadHint(
          createFamily(adultName, adultEmail, adultPassword, childName, childPin, responsibleConsent),
          () => { submitBtn.textContent = "Ainda conectando..."; errorEl.textContent = SLOW_LOAD_MESSAGE; errorEl.classList.remove("hidden", "error-text"); errorEl.classList.add("hint-text"); }
        );
        renderRegisterSuccess(result, adultEmail);
      } catch (err) {
        errorEl.classList.remove("hint-text");
        errorEl.classList.add("error-text");
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
        submitting = false;
        submitBtn.disabled = false;
        submitBtn.textContent = originalLabel;
      }
    });
  }

  function renderRegisterSuccess(result, adultEmail) {
    submitting = false;

    // Ja aproveita o cache deste aparelho -- quem acabou de criar a familia
    // provavelmente vai logar (ou deixar a crianca logar) em seguida aqui mesmo.
    try {
      localStorage.setItem(FAMILY_CODE_KEY, result.familyCode);
    } catch {
      // localStorage indisponivel (modo privado, etc.) — sem problema, so nao cacheia.
    }

    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Família criada! Guarde estas informações em lugar seguro:</p>
        <div class="field">
          <label>Código da família — a criança usa para logar em novos aparelhos</label>
          <p class="register-code">${escapeHtml(result.familyCode)}</p>
        </div>
        <div class="field">
          <label>Código de recuperação — para "esqueci minha senha"</label>
          <p class="register-code">${escapeHtml(result.recoveryCode)}</p>
        </div>
        <button type="button" class="btn btn-primary btn-block" id="go-to-login-btn">Ir para o login</button>
      </div>
    `;

    slot.querySelector("#go-to-login-btn").addEventListener("click", () => {
      renderAdultForm();
      const emailInput = slot.querySelector("#email");
      if (emailInput) emailInput.value = adultEmail;
    });
  }

  // Ponto de entrada da aba "Crianca": se este aparelho ja tem o codigo da
  // familia guardado (de um cadastro ou login de adulto anteriores, ou de uma
  // digitacao anterior), pula direto pra lista de nomes; senao pede o codigo.
  async function renderChildForm() {
    const cachedCode = localStorage.getItem(FAMILY_CODE_KEY) || "";
    if (cachedCode) {
      await loadChildrenForCode(cachedCode);
    } else {
      renderFamilyCodeEntry();
    }
  }

  async function loadChildrenForCode(code) {
    slot.innerHTML = `<div class="login-form"><p class="profile-picker-hint">Carregando...</p></div>`;
    try {
      const children = await getChildrenByFamilyCode(code);
      localStorage.setItem(FAMILY_CODE_KEY, code);
        renderProfilePicker(children);
    } catch (err) {
      renderFamilyCodeEntry(err.message);
    }
  }

  function renderFamilyCodeEntry(initialError) {
    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Digite o código da família (peça para o responsável).</p>
        <div class="field">
          <label for="family-code">Código da família</label>
          <input id="family-code" type="text" autocomplete="off" placeholder="XXX-XXX" maxlength="7" />
        </div>
        <p class="error-text ${initialError ? "" : "hidden"}" id="family-code-error">${escapeHtml(initialError || "")}</p>
        <button type="button" class="btn btn-primary btn-block" id="family-code-continue-btn">Continuar</button>
      </div>
    `;

    const input = slot.querySelector("#family-code");
    const errorEl = slot.querySelector("#family-code-error");

    // Auto-formata "XXXYYY" -> "XXX-YYY" enquanto digita (ou cola sem o traco) --
    // o codigo gerado pelo backend ja vem nesse formato (ver AuthService.GenerateFamilyCode).
    input.addEventListener("input", () => {
      let raw = input.value.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 6);
      input.value = raw.length > 3 ? `${raw.slice(0, 3)}-${raw.slice(3)}` : raw;
    });

    async function submitCode() {
      const code = input.value.trim();
      if (!code) {
        errorEl.textContent = "Digite o código da família.";
        errorEl.classList.remove("hidden");
        return;
      }
      errorEl.classList.add("hidden");
      await loadChildrenForCode(code);
    }

    slot.querySelector("#family-code-continue-btn").addEventListener("click", submitCode);
    input.addEventListener("keydown", (event) => {
      if (event.key === "Enter") { event.preventDefault(); submitCode(); }
    });
  }

  // Fluxo principal: a crianca so toca no proprio nome.
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
        <button type="button" class="btn btn-ghost btn-block" id="change-family-code-btn">Não é este aparelho / trocar código</button>
      </div>
    `;

    slot.querySelector("#profile-picker").addEventListener("click", (event) => {
      const btn = event.target.closest(".profile-picker__item");
      if (!btn) return;
      renderPinEntry({ id: btn.dataset.id, name: btn.dataset.name }, { showBackToPicker: true });
    });

    slot.querySelector("#change-family-code-btn").addEventListener("click", () => {
      renderFamilyCodeEntry();
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

function escapeHtml(value = "") {
  const div = document.createElement("div");
  div.textContent = value;
  return div.innerHTML;
}
