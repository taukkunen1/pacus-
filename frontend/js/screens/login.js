import { loginAdult, loginChild, resetAdultPassword } from "../api/auth-api.js";
import { getFamilyChildren, getChildrenByFamilyCode, getFamilyCode } from "../api/family-api.js";
import { createFamily } from "../api/bootstrap-api.js";
import { withSlowLoadHint, SLOW_LOAD_MESSAGE } from "../utils/slow-load-hint.js";

const CHILD_PROFILE_KEY = "pacus.child.profileId"; // so um id, nao e credencial — ok em localStorage
const CHILDREN_CACHE_KEY = "pacus.family.children"; // so nome + id de cada crianca — mesmo motivo
const FAMILY_CODE_KEY = "pacus.family.code"; // codigo curto da familia (ver User.FamilyCode) — nao e credencial

function getCachedChildren() {
  try {
    const raw = localStorage.getItem(CHILDREN_CACHE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function cacheChildren(children) {
  try {
    localStorage.setItem(CHILDREN_CACHE_KEY, JSON.stringify(children));
  } catch {
    // Melhor esforco — se falhar, so nao fica em cache pra proxima vez.
  }
}

function getSavedFamilyCode() {
  try {
    return localStorage.getItem(FAMILY_CODE_KEY) || "";
  } catch {
    return "";
  }
}

function saveFamilyCode(code) {
  try {
    localStorage.setItem(FAMILY_CODE_KEY, code);
  } catch {
    // Melhor esforco.
  }
}

// Fecha o problema do "ovo e a galinha": depois que um adulto loga uma vez
// neste aparelho, tanto a lista de criancas quanto o codigo da familia ficam
// salvos — a crianca nao precisa que ninguem digite o codigo pra ela na
// primeira vez, e ele continua disponivel (auto-preenchido) se algum dia o
// cache de nomes for limpo.
async function cacheFamilyChildren() {
  try {
    const children = await getFamilyChildren();
    cacheChildren(children);
  } catch {
    // Melhor esforco — se falhar, a tela de crianca cai no fallback de codigo.
  }

  try {
    const { familyCode } = await getFamilyCode();
    if (familyCode) saveFamilyCode(familyCode);
  } catch {
    // Melhor esforco.
  }
}

// Uppercase, so letras/numeros, no maximo 6 caracteres, com traco depois dos
// 3 primeiros -- mesmo formato "XXX-YYY" gerado por AuthService.GenerateFamilyCode.
function formatFamilyCodeInput(value) {
  const cleaned = String(value ?? "")
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "")
    .slice(0, 6);
  return cleaned.length > 3 ? `${cleaned.slice(0, 3)}-${cleaned.slice(3)}` : cleaned;
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
        <button type="button" class="btn btn-ghost btn-block" id="create-family-from-adult-btn">Criar uma família</button>
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
    slot.querySelector("#create-family-from-adult-btn").addEventListener("click", renderCreateFamilyForm);
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
      return;
    }

    const savedCode = getSavedFamilyCode();
    renderFamilyCodeEntry({ prefill: savedCode, autoLookup: Boolean(savedCode) });
  }

  // Fluxo principal: a crianca so toca no proprio nome. A lista vem do cache
  // populado no ultimo login de um adulto neste aparelho, ou de uma busca por
  // codigo da familia (ver cacheFamilyChildren / renderFamilyCodeEntry).
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
        <button type="button" class="btn btn-ghost btn-block" id="use-code-instead-btn">Nao encontrou seu nome?</button>
      </div>
    `;

    slot.querySelector("#profile-picker").addEventListener("click", (event) => {
      const btn = event.target.closest(".profile-picker__item");
      if (!btn) return;
      renderPinEntry({ id: btn.dataset.id, name: btn.dataset.name }, { showBackToPicker: true });
    });

    slot.querySelector("#use-code-instead-btn").addEventListener("click", () => {
      renderFamilyCodeEntry({ prefill: getSavedFamilyCode() });
    });
  }

  // Fallback (e ponto de entrada quando nenhum adulto ainda logou neste
  // aparelho): a crianca digita o codigo curto da familia (ver
  // User.FamilyCode) em vez de colar um ObjectId do Mongo -- o adulto encontra
  // esse codigo na tela PACUS, em "Configuracoes da familia". Auto-formata
  // "XXX-YYY" conforme digita, e tenta a busca sozinho quando ja existe um
  // codigo salvo neste aparelho (autoLookup), pra crianca so precisar tocar
  // o proprio nome direto, sem redigitar nada.
  function renderFamilyCodeEntry({ prefill = "", autoLookup = false } = {}) {
    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Peca o código da família para um adulto (tela PACUS → Configurações da família).</p>
        <div class="field">
          <label for="family-code">Código da família</label>
          <input
            id="family-code"
            type="text"
            value="${formatFamilyCodeInput(prefill)}"
            placeholder="XXX-YYY"
            autocomplete="off"
            maxlength="7"
          />
        </div>
        <p class="error-text hidden" id="family-code-error"></p>
        <button type="button" class="btn btn-primary btn-block" id="family-code-continue-btn">Continuar</button>
        <button type="button" class="btn btn-ghost btn-block" id="create-family-btn">Criar uma família</button>
      </div>
    `;

    const codeInput = slot.querySelector("#family-code");
    const errorEl = slot.querySelector("#family-code-error");
    const continueBtn = slot.querySelector("#family-code-continue-btn");

    codeInput.addEventListener("input", () => {
      codeInput.value = formatFamilyCodeInput(codeInput.value);
    });

    codeInput.addEventListener("keydown", (event) => {
      if (event.key === "Enter") {
        event.preventDefault();
        lookup();
      }
    });

    async function lookup() {
      const code = codeInput.value.trim();

      if (code.length < 7) {
        errorEl.textContent = "Digite o código completo (6 caracteres).";
        errorEl.classList.remove("hidden");
        return;
      }

      if (submitting) return;

      errorEl.classList.add("hidden");
      submitting = true;
      const originalLabel = continueBtn.textContent;
      continueBtn.disabled = true;
      continueBtn.textContent = "Procurando...";

      try {
        const children = await withSlowLoadHint(
          getChildrenByFamilyCode(code),
          () => { continueBtn.textContent = "Ainda conectando..."; }
        );

        if (!children.length) {
          errorEl.textContent = "Nenhuma criança encontrada com esse código. Confira com um adulto da família.";
          errorEl.classList.remove("hidden");
          return;
        }

        saveFamilyCode(code);
        cacheChildren(children);
        renderProfilePicker(children);
      } catch (err) {
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      } finally {
        submitting = false;
        continueBtn.disabled = false;
        continueBtn.textContent = originalLabel;
      }
    }

    continueBtn.addEventListener("click", lookup);
    slot.querySelector("#create-family-btn").addEventListener("click", renderCreateFamilyForm);

    // Codigo ja salvo neste aparelho (de um login de adulto anterior, ou de uma
    // busca por codigo anterior) -- tenta sozinho antes de pedir pra crianca
    // tocar em "Continuar".
    if (autoLookup && prefill) {
      lookup();
    }
  }

  // Cadastro de uma familia nova (1 adulto + 1 crianca) -- ate agora so existia
  // via chamada direta na API, sem tela nenhuma. Acessivel tanto da aba
  // Crianca (quando ninguem ainda tem um codigo) quanto da aba Adulto.
  function renderCreateFamilyForm() {
    slot.innerHTML = `
      <form class="login-form" id="create-family-form">
        <p class="profile-picker-hint">Crie a conta do responsável e o perfil da primeira criança.</p>

        <div class="field">
          <label for="new-adult-name">Seu nome</label>
          <input id="new-adult-name" type="text" required />
        </div>

        <div class="field">
          <label for="new-adult-email">Seu email</label>
          <input id="new-adult-email" type="email" autocomplete="username" required />
        </div>

        <div class="field">
          <label for="new-adult-password">Sua senha (mínimo 8 caracteres)</label>
          <input id="new-adult-password" type="password" autocomplete="new-password" minlength="8" required />
        </div>

        <div class="field">
          <label for="new-child-name">Nome da criança</label>
          <input id="new-child-name" type="text" required />
        </div>

        <div class="field">
          <label for="new-child-pin">PIN da criança (4 dígitos)</label>
          <input
            id="new-child-pin"
            type="text"
            inputmode="numeric"
            pattern="[0-9]{4}"
            maxlength="4"
            autocomplete="off"
            required
          />
        </div>

        <p class="error-text hidden" id="create-family-error"></p>
        <button type="submit" class="btn btn-primary btn-block">Criar família</button>
        <button type="button" class="btn btn-ghost btn-block" id="back-from-create-btn">Voltar</button>
      </form>
    `;

    slot.querySelector("#back-from-create-btn").addEventListener("click", () => {
      renderFamilyCodeEntry({ prefill: getSavedFamilyCode() });
    });

    slot.querySelector("#create-family-form").addEventListener("submit", async (event) => {
      event.preventDefault();
      if (submitting) return;

      const adultName = slot.querySelector("#new-adult-name").value.trim();
      const adultEmail = slot.querySelector("#new-adult-email").value.trim();
      const adultPassword = slot.querySelector("#new-adult-password").value;
      const childName = slot.querySelector("#new-child-name").value.trim();
      const childPin = slot.querySelector("#new-child-pin").value.trim();
      const errorEl = slot.querySelector("#create-family-error");
      errorEl.classList.add("hidden");

      const submitBtn = slot.querySelector('#create-family-form button[type="submit"]');
      const originalLabel = submitBtn.textContent;

      submitting = true;
      submitBtn.disabled = true;
      submitBtn.textContent = "Criando...";

      try {
        const result = await withSlowLoadHint(
          createFamily({ adultName, adultEmail, adultPassword, childName, childPin }),
          () => { submitBtn.textContent = "Ainda conectando..."; }
        );

        saveFamilyCode(result.familyCode);
        renderFamilyCreatedScreen(result);
      } catch (err) {
        errorEl.textContent = err.message;
        errorEl.classList.remove("hidden");
      } finally {
        submitting = false;
        submitBtn.disabled = false;
        submitBtn.textContent = originalLabel;
      }
    });
  }

  // Tela final do cadastro -- mostra os dois codigos (familia + recuperacao de
  // senha) uma unica vez, igual ao "esqueci minha senha". So aqui eles aparecem
  // em texto puro; depois disso so o hash fica salvo no banco.
  function renderFamilyCreatedScreen(result) {
    slot.innerHTML = `
      <div class="login-form">
        <p class="profile-picker-hint">Família criada! Guarde os dois códigos abaixo em lugar seguro antes de continuar.</p>

        <div class="field">
          <label for="created-family-code">Código da família (para a criança logar)</label>
          <input id="created-family-code" type="text" value="${escapeHtml(result.familyCode)}" readonly />
        </div>

        <div class="field">
          <label for="created-recovery-code">Código de recuperação de senha (para você, se esquecer a senha)</label>
          <input id="created-recovery-code" type="text" value="${escapeHtml(result.recoveryCode)}" readonly />
        </div>

        <button type="button" class="btn btn-primary btn-block" id="go-to-login-btn">Ir para o login</button>
      </div>
    `;

    slot.querySelector("#go-to-login-btn").addEventListener("click", () => {
      mode = "adult";
      roleButtons.forEach((b) => b.classList.toggle("is-active", b.dataset.role === "adult"));
      renderAdultForm();
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
  div.textContent = String(value);
  return div.innerHTML;
}
