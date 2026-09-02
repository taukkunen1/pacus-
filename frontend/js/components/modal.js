import { isValidPoints, POINTS_HELP_TEXT } from "../utils/validation.js";

// Modal simples com textarea -- usado onde window.prompt() nao serve porque o
// campo precisa aceitar varias linhas (window.prompt e uma unica linha; Enter
// fecha o dialogo em vez de quebrar linha). Promise-based pra poder usar com
// await no lugar de window.prompt nos fluxos existentes.
//
// Resolve com o texto (pode ser string vazia) se a pessoa confirmar, ou com
// `null` se cancelar -- mesma convencao de window.prompt, pra ficar facil de
// trocar um pelo outro nos chamadores.
let activeModal = null;

export function promptTextarea({
  title = "",
  label = "",
  value = "",
  placeholder = "",
  confirmLabel = "OK",
  cancelLabel = "Cancelar"
} = {}) {
  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box" role="dialog" aria-modal="true" ${title ? `aria-label="${escapeHtml(title)}"` : ""}>
        ${title ? `<h3 class="modal-title">${escapeHtml(title)}</h3>` : ""}
        <div class="field">
          ${label ? `<label for="modal-textarea-input">${escapeHtml(label)}</label>` : ""}
          <textarea
            id="modal-textarea-input"
            class="modal-textarea"
            rows="4"
            placeholder="${escapeHtml(placeholder)}"
          >${escapeHtml(value)}</textarea>
        </div>
        <p class="modal-hint">Enter quebra linha — Ctrl+Enter (ou ⌘+Enter) confirma.</p>
        <div class="modal-actions">
          <button type="button" class="btn btn-ghost" data-modal-action="cancel">${escapeHtml(cancelLabel)}</button>
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    const textarea = overlay.querySelector(".modal-textarea");
    textarea.focus();
    textarea.setSelectionRange(textarea.value.length, textarea.value.length);

    function finish(result) {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve(result);
    }

    overlay
      .querySelector('[data-modal-action="ok"]')
      .addEventListener("click", () => finish(textarea.value));

    overlay
      .querySelector('[data-modal-action="cancel"]')
      .addEventListener("click", () => finish(null));

    // Clique fora da caixa fecha como "cancelar" (mesma UX de um dialogo nativo).
    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(null);
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        finish(null);
        return;
      }

      // Enter sozinho tem que continuar quebrando linha dentro do textarea --
      // so confirma com Ctrl/Cmd+Enter, senao ninguem consegue digitar
      // "uma linha por vez", que e exatamente o problema que este modal existe
      // pra resolver.
      if (event.key === "Enter" && (event.ctrlKey || event.metaKey)) {
        finish(textarea.value);
      }
    });
  });
}

// Modal com um unico campo de linha -- substitui window.prompt() nos fluxos
// existentes (fuso horario, PIN, custo, etc.) pra parar de misturar o dialogo
// feio e sem estilo do navegador com o resto da interface do Pacus. Mesma
// convencao de promptTextarea: resolve com o texto (pode ser vazio) se
// confirmar, `null` se cancelar.
export function promptInput({
  title = "",
  label = "",
  value = "",
  placeholder = "",
  type = "text",
  hint = "",
  confirmLabel = "OK",
  cancelLabel = "Cancelar"
} = {}) {
  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box" role="dialog" aria-modal="true" ${title ? `aria-label="${escapeHtml(title)}"` : ""}>
        ${title ? `<h3 class="modal-title">${escapeHtml(title)}</h3>` : ""}
        <div class="field">
          ${label ? `<label for="modal-input-field">${escapeHtml(label)}</label>` : ""}
          <input
            id="modal-input-field"
            type="${escapeHtml(type)}"
            value="${escapeHtml(value)}"
            placeholder="${escapeHtml(placeholder)}"
          />
        </div>
        ${hint ? `<p class="modal-hint">${escapeHtml(hint)}</p>` : ""}
        <div class="modal-actions">
          <button type="button" class="btn btn-ghost" data-modal-action="cancel">${escapeHtml(cancelLabel)}</button>
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    const input = overlay.querySelector("#modal-input-field");
    input.focus();
    input.select();

    function finish(result) {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve(result);
    }

    overlay
      .querySelector('[data-modal-action="ok"]')
      .addEventListener("click", () => finish(input.value));

    overlay
      .querySelector('[data-modal-action="cancel"]')
      .addEventListener("click", () => finish(null));

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(null);
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        finish(null);
        return;
      }
      if (event.key === "Enter") finish(input.value);
    });
  });
}

// Modal so-leitura, pra revelar algo (ex.: a reacao do adulto sobre o dia -- ver
// pacus/habitat.js e screens/home.js) sem o peso de um textarea/formulario. Resolve
// quando a pessoa fecha (clique no botao, fora da caixa, ou Escape) -- nao ha
// "cancelar" real aqui, e so leitura.
export function showMessageModal({
  title = "",
  body = "",
  confirmLabel = "Fechar"
} = {}) {
  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box modal-box--reveal" role="dialog" aria-modal="true" ${title ? `aria-label="${escapeHtml(title)}"` : ""}>
        ${title ? `<h3 class="modal-title">${escapeHtml(title)}</h3>` : ""}
        <p class="modal-reveal-body">${escapeHtml(body)}</p>
        <div class="modal-actions">
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    function finish() {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve();
    }

    overlay.querySelector('[data-modal-action="ok"]').addEventListener("click", finish);

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish();
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape" || event.key === "Enter") finish();
    });
  });
}

const TYPE_OPTIONS = [
  { value: "mandatory", label: "Obrigatória" },
  { value: "expected", label: "Deve fazer" },
  { value: "challenge", label: "Desafio" }
];

const PERIOD_OPTIONS = [
  { value: "morning", label: "Manhã" },
  { value: "afternoon", label: "Tarde" },
  { value: "evening", label: "Noite" }
];

// Painel unico de criar/editar tarefa -- substitui a sequencia antiga de
// varios window.prompt/confirm em fila (nome > descricao > pontos > tipo >
// opcoes > motivo), que obrigava a pessoa a ir clicando OK varias vezes sem
// ver o resto dos campos, e deixava o tipo (obrigatoria/deve fazer/desafio)
// escondido dentro de mais um prompt generico, facil de nao perceber que da
// pra mudar. Aqui tudo aparece de uma vez, incluindo o tipo como um grupo de
// botoes visivel. Resolve com os campos preenchidos, ou `null` se cancelar.
export function promptTaskForm({
  title = "Tarefa",
  values = {},
  showPermanentToggle = false,
  confirmLabel = "Salvar"
} = {}) {
  const initialOptions =
    Array.isArray(values.options) && values.options.length > 0
      ? values.options
      : [];

  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box modal-box--form" role="dialog" aria-modal="true" aria-label="${escapeHtml(title)}">
        <h3 class="modal-title">${escapeHtml(title)}</h3>

        <div class="field">
          <label for="task-form-title">Nome</label>
          <input
            id="task-form-title"
            type="text"
            value="${escapeHtml(values.title ?? "")}"
            placeholder="Ex.: Arrumar a cama"
          />
        </div>

        <div class="field">
          <label for="task-form-description">Descrição (opcional) — um item por linha</label>
          <textarea
            id="task-form-description"
            class="modal-textarea"
            rows="3"
            placeholder="Ex.: 48 ÷ 6 = ___${"\n"}72 ÷ 8 = ___"
          >${escapeHtml(values.description ?? "")}</textarea>
        </div>

        <div class="field">
          <label for="task-form-points">Pontos</label>
          <input
            id="task-form-points"
            type="number"
            step="1"
            value="${escapeHtml(String(values.points ?? 1))}"
          />
          <p class="modal-hint">${escapeHtml(POINTS_HELP_TEXT)}.</p>
        </div>

        <div class="field">
          <label>Tipo</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Tipo da tarefa">
            ${TYPE_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="task-form-type"
                  value="${opt.value}"
                  ${(values.type ?? "mandatory") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label>Período</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Período da tarefa">
            ${PERIOD_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="task-form-period"
                  value="${opt.value}"
                  ${(values.period ?? "morning") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label class="task-form-checkbox">
            <input type="checkbox" id="task-form-has-options" ${initialOptions.length > 0 ? "checked" : ""} />
            <span>Tarefa com opções pra criança escolher (ex.: torre de copos / ponte de papel)</span>
          </label>
          <div id="task-form-options-list" class="task-form-options-list ${initialOptions.length > 0 ? "" : "hidden"}"></div>
        </div>

        <div class="field">
          <label for="task-form-reason">Por que essa tarefa importa (opcional)</label>
          <input
            id="task-form-reason"
            type="text"
            value="${escapeHtml(values.reason ?? "")}"
            placeholder="O que a criança vê como motivo, não como fazer"
          />
        </div>

        ${
          showPermanentToggle
            ? `
        <div class="field">
          <label class="task-form-checkbox">
            <input type="checkbox" id="task-form-permanent" />
            <span>Repetir nos próximos dias (tarefa permanente)</span>
          </label>
        </div>`
            : ""
        }

        <p class="error-text hidden" id="task-form-error"></p>

        <div class="modal-actions">
          <button type="button" class="btn btn-ghost" data-modal-action="cancel">Cancelar</button>
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    const titleInput = overlay.querySelector("#task-form-title");
    const pointsInput = overlay.querySelector("#task-form-points");
    const hasOptionsCheckbox = overlay.querySelector("#task-form-has-options");
    const optionsList = overlay.querySelector("#task-form-options-list");
    const errorText = overlay.querySelector("#task-form-error");

    let optionInputs = [];

    function renderOptionInputs(seedValues) {
      optionsList.innerHTML = "";
      optionInputs = [];

      const seeds =
        seedValues && seedValues.length > 0
          ? seedValues
          : ["", ""];

      seeds.forEach((value, index) => addOptionRow(value, index));
      if (optionInputs.length < 2) {
        while (optionInputs.length < 2) addOptionRow("", optionInputs.length);
      }
    }

    function addOptionRow(value, index) {
      const row = document.createElement("div");
      row.className = "task-form-option-row";
      row.innerHTML = `
        <input
          type="text"
          class="task-form-option-input"
          placeholder="Opção ${index + 1}"
          value="${escapeHtml(value ?? "")}"
        />
        <button type="button" class="btn btn-ghost task-form-option-remove" aria-label="Remover opção">✕</button>
      `;
      const input = row.querySelector(".task-form-option-input");
      const removeBtn = row.querySelector(".task-form-option-remove");

      removeBtn.addEventListener("click", () => {
        if (optionInputs.length <= 2) return; // minimo de 2 opcoes
        row.remove();
        optionInputs = optionInputs.filter((i) => i !== input);
        updateAddButtonState();
      });

      optionsList.appendChild(row);
      optionInputs.push(input);
    }

    let addOptionBtn = null;
    function updateAddButtonState() {
      if (!addOptionBtn) return;
      addOptionBtn.disabled = optionInputs.length >= 4;
    }

    function ensureAddButton() {
      if (addOptionBtn) return;
      addOptionBtn = document.createElement("button");
      addOptionBtn.type = "button";
      addOptionBtn.className = "btn btn-ghost task-form-option-add";
      addOptionBtn.textContent = "+ Adicionar opção";
      addOptionBtn.addEventListener("click", () => {
        if (optionInputs.length >= 4) return;
        addOptionRow("", optionInputs.length);
        updateAddButtonState();
      });
      optionsList.after(addOptionBtn);
    }

    renderOptionInputs(initialOptions);
    ensureAddButton();
    updateAddButtonState();

    hasOptionsCheckbox.addEventListener("change", () => {
      optionsList.classList.toggle("hidden", !hasOptionsCheckbox.checked);
      addOptionBtn.classList.toggle("hidden", !hasOptionsCheckbox.checked);
    });
    addOptionBtn.classList.toggle("hidden", !hasOptionsCheckbox.checked);

    titleInput.focus();

    function showError(message) {
      errorText.textContent = message;
      errorText.classList.remove("hidden");
    }

    function finish(result) {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve(result);
    }

    function submit() {
      const trimmedTitle = titleInput.value.trim();
      if (!trimmedTitle) {
        showError("O nome da tarefa é obrigatório.");
        titleInput.focus();
        return;
      }

      const points = Number(pointsInput.value);
      if (!isValidPoints(points)) {
        showError(`Pontos inválidos. ${POINTS_HELP_TEXT}.`);
        pointsInput.focus();
        return;
      }

      const type =
        overlay.querySelector('input[name="task-form-type"]:checked')
          ?.value ?? "mandatory";

      const period =
        overlay.querySelector('input[name="task-form-period"]:checked')
          ?.value ?? "morning";

      let options = [];
      if (hasOptionsCheckbox.checked) {
        options = optionInputs
          .map((input) => input.value.trim())
          .filter((value) => value.length > 0);

        if (options.length < 2) {
          showError("Uma tarefa com opções precisa de pelo menos 2 preenchidas.");
          return;
        }
      }

      const description =
        overlay.querySelector("#task-form-description").value.trim() || null;
      const reason =
        overlay.querySelector("#task-form-reason").value.trim() || null;
      const permanent = showPermanentToggle
        ? Boolean(overlay.querySelector("#task-form-permanent")?.checked)
        : false;

      finish({
        title: trimmedTitle,
        description,
        points,
        type,
        period,
        options,
        reason,
        permanent
      });
    }

    overlay
      .querySelector('[data-modal-action="ok"]')
      .addEventListener("click", submit);

    overlay
      .querySelector('[data-modal-action="cancel"]')
      .addEventListener("click", () => finish(null));

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(null);
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape") finish(null);
    });
  });
}

const STORE_CATEGORY_OPTIONS = [
  { value: "screen_time", label: "Tempo de tela" },
  { value: "toy", label: "Brinquedo" },
  { value: "activity", label: "Atividade" },
  { value: "other", label: "Outro" }
];

// Painel unico de criar/editar item da loja -- mesmo espirito do promptTaskForm
// acima: todos os campos visiveis de uma vez, em vez da fila antiga de
// window.prompt (nome > descricao > custo > categoria > icone > estoque >
// limite diario > minutos de tela), que tambem obrigava digitar a categoria
// por extenso ("screen_time", "toy"...) sem nenhuma pista visual de quais
// valores eram aceitos. Resolve com os campos prontos pro payload de
// createStoreItem/updateStoreItem, ou `null` se cancelar.
export function promptStoreItemForm({
  title = "Item da loja",
  values = {},
  confirmLabel = "Salvar"
} = {}) {
  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box modal-box--form" role="dialog" aria-modal="true" aria-label="${escapeHtml(title)}">
        <h3 class="modal-title">${escapeHtml(title)}</h3>

        <div class="field">
          <label for="store-form-title">Nome</label>
          <input
            id="store-form-title"
            type="text"
            value="${escapeHtml(values.title ?? "")}"
            placeholder="Ex.: 30 minutos de videogame"
          />
        </div>

        <div class="field">
          <label for="store-form-description">Descrição (opcional) — um item por linha</label>
          <textarea
            id="store-form-description"
            class="modal-textarea"
            rows="3"
            placeholder="Ex.: válido só aos fins de semana"
          >${escapeHtml(values.description ?? "")}</textarea>
        </div>

        <div class="field">
          <label for="store-form-cost">Custo</label>
          <input
            id="store-form-cost"
            type="number"
            step="1"
            min="1"
            value="${escapeHtml(String(values.cost ?? 100))}"
          />
          <p class="modal-hint">Em Pacus Points.</p>
        </div>

        <div class="field">
          <label>Categoria</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Categoria do item">
            ${STORE_CATEGORY_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="store-form-category"
                  value="${opt.value}"
                  ${(values.category ?? "other") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label for="store-form-icon">Emoji (opcional)</label>
          <input
            id="store-form-icon"
            type="text"
            maxlength="4"
            value="${escapeHtml(values.icon ?? "")}"
            placeholder="🎮"
          />
        </div>

        <div class="field">
          <label for="store-form-stock">Estoque (opcional)</label>
          <input
            id="store-form-stock"
            type="number"
            step="1"
            min="1"
            value="${values.stock !== null && values.stock !== undefined ? escapeHtml(String(values.stock)) : ""}"
            placeholder="Em branco = ilimitado"
          />
          <p class="modal-hint">Quantas vezes esse item pode ser resgatado ao todo.</p>
        </div>

        <div class="field">
          <label for="store-form-daily-limit">Limite por dia (opcional)</label>
          <input
            id="store-form-daily-limit"
            type="number"
            step="1"
            min="1"
            value="${values.dailyLimit ? escapeHtml(String(values.dailyLimit)) : ""}"
            placeholder="Em branco = sem limite"
          />
          <p class="modal-hint">Quantas vezes esse item pode ser resgatado por dia.</p>
        </div>

        <div class="field">
          <label for="store-form-screen-time">Tempo de tela concedido (opcional)</label>
          <input
            id="store-form-screen-time"
            type="number"
            step="1"
            min="1"
            value="${values.screenTimeMinutes ? escapeHtml(String(values.screenTimeMinutes)) : ""}"
            placeholder="Minutos — só pra recompensas de tempo de tela"
          />
          <p class="modal-hint">Preenchido, esse tempo é concedido automaticamente ao aprovar o resgate.</p>
        </div>

        <p class="error-text hidden" id="store-form-error"></p>

        <div class="modal-actions">
          <button type="button" class="btn btn-ghost" data-modal-action="cancel">Cancelar</button>
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    const titleInput = overlay.querySelector("#store-form-title");
    const errorText = overlay.querySelector("#store-form-error");
    titleInput.focus();

    function showError(message) {
      errorText.textContent = message;
      errorText.classList.remove("hidden");
    }

    function finish(result) {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve(result);
    }

    function readOptionalInt(selector) {
      const raw = overlay.querySelector(selector).value.trim();
      return raw ? Number(raw) : null;
    }

    function submit() {
      const trimmedTitle = titleInput.value.trim();
      if (!trimmedTitle) {
        showError("O nome do item é obrigatório.");
        titleInput.focus();
        return;
      }

      const cost = Number(overlay.querySelector("#store-form-cost").value);
      if (!Number.isInteger(cost) || cost <= 0) {
        showError("Custo inválido. Use um número inteiro maior que zero.");
        return;
      }

      const category =
        overlay.querySelector('input[name="store-form-category"]:checked')
          ?.value ?? "other";

      const stock = readOptionalInt("#store-form-stock");
      if (stock !== null && (!Number.isInteger(stock) || stock <= 0)) {
        showError("Estoque inválido. Deixe em branco para ilimitado ou use um número inteiro maior que zero.");
        return;
      }

      const dailyLimit = readOptionalInt("#store-form-daily-limit");
      if (dailyLimit !== null && (!Number.isInteger(dailyLimit) || dailyLimit <= 0)) {
        showError("Limite diário inválido. Deixe em branco para sem limite ou use um número inteiro maior que zero.");
        return;
      }

      const screenTimeMinutes = readOptionalInt("#store-form-screen-time");
      if (screenTimeMinutes !== null && (!Number.isInteger(screenTimeMinutes) || screenTimeMinutes <= 0)) {
        showError("Minutos de tela inválidos. Deixe em branco ou use um número inteiro maior que zero.");
        return;
      }

      const description =
        overlay.querySelector("#store-form-description").value.trim() || null;
      const icon =
        overlay.querySelector("#store-form-icon").value.trim() || null;

      finish({
        title: trimmedTitle,
        description,
        cost,
        category,
        icon,
        stock,
        dailyLimit,
        screenTimeMinutes
      });
    }

    overlay
      .querySelector('[data-modal-action="ok"]')
      .addEventListener("click", submit);

    overlay
      .querySelector('[data-modal-action="cancel"]')
      .addEventListener("click", () => finish(null));

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(null);
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape") finish(null);
    });
  });
}

const RECURRENCE_OPTIONS = [
  { value: "daily", label: "Todos os dias" },
  { value: "weekday", label: "Dias úteis" },
  { value: "weekend", label: "Fim de semana" },
  { value: "custom", label: "Dias específicos" },
  { value: "weekday_rotation", label: "Atividade diferente por dia útil" }
];

const CUSTOM_DAY_OPTIONS = [
  { value: "Monday", label: "Seg" },
  { value: "Tuesday", label: "Ter" },
  { value: "Wednesday", label: "Qua" },
  { value: "Thursday", label: "Qui" },
  { value: "Friday", label: "Sex" },
  { value: "Saturday", label: "Sáb" },
  { value: "Sunday", label: "Dom" }
];

// So segunda a sexta -- mesma lista de WEEKDAYS em screens/pacus.js, duplicada
// aqui (ver comentario de promptForOptions em pacus.js sobre a duplicacao ja
// existente entre os arquivos de prompt).
const WEEKDAY_ROTATION_DAYS = [
  { key: "Monday", label: "Segunda-feira" },
  { key: "Tuesday", label: "Terça-feira" },
  { key: "Wednesday", label: "Quarta-feira" },
  { key: "Thursday", label: "Quinta-feira" },
  { key: "Friday", label: "Sexta-feira" }
];

// Painel unico de criar/editar tarefa permanente -- antes era uma fila de mais
// de 10 window.prompt/confirm em sequencia (nome, descricao, tipo, periodo,
// pontos, recorrencia -- com ate mais prompts aninhados pra dias especificos ou
// pra atividade por dia util --, opcoes, motivos), pedindo pra digitar por
// extenso valores como "mandatory" ou "morning" sem nenhuma pista visual do
// que era aceito. Agora e um painel so, no mesmo padrao visual do
// promptTaskForm (tarefa de hoje) -- inclusive reaproveitando as mesmas
// classes de type-group/checkbox/options-list. Resolve com os campos prontos
// pro payload de createTask/updateTask, ou `null` se cancelar.
export function promptPermanentTaskForm({
  title = "Tarefa permanente",
  values = {},
  confirmLabel = "Salvar"
} = {}) {
  const initialOptions =
    Array.isArray(values.options) && values.options.length > 0
      ? values.options
      : [];

  const initialReasons =
    Array.isArray(values.reasons) && values.reasons.length > 0
      ? values.reasons
      : (values.reason ? [values.reason] : []);

  const initialCustomDays = Array.isArray(values.customDays) ? values.customDays : [];

  const initialVariants = WEEKDAY_ROTATION_DAYS.map(({ key }) =>
    (values.variants ?? []).find(
      (variant) => String(variant.dayOfWeek).toLowerCase() === key.toLowerCase()
    ) ?? null
  );

  return new Promise((resolve) => {
    closeActiveModal();

    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    overlay.innerHTML = `
      <div class="modal-box modal-box--form" role="dialog" aria-modal="true" aria-label="${escapeHtml(title)}">
        <h3 class="modal-title">${escapeHtml(title)}</h3>

        <div class="field">
          <label for="template-form-title">Nome</label>
          <input
            id="template-form-title"
            type="text"
            value="${escapeHtml(values.title ?? "")}"
            placeholder="Ex.: Arrumar a cama"
          />
        </div>

        <div class="field">
          <label for="template-form-description">Descrição (opcional) — um item por linha</label>
          <textarea
            id="template-form-description"
            class="modal-textarea"
            rows="3"
            placeholder="Ex.: 48 ÷ 6 = ___${"\n"}72 ÷ 8 = ___"
          >${escapeHtml(values.description ?? "")}</textarea>
        </div>

        <div class="field">
          <label for="template-form-points">Pontos</label>
          <input
            id="template-form-points"
            type="number"
            step="1"
            value="${escapeHtml(String(values.points ?? 1))}"
          />
          <p class="modal-hint">${escapeHtml(POINTS_HELP_TEXT)}.</p>
        </div>

        <div class="field">
          <label>Tipo</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Tipo da tarefa">
            ${TYPE_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="template-form-type"
                  value="${opt.value}"
                  ${(values.type ?? "mandatory") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label>Período</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Período da tarefa">
            ${PERIOD_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="template-form-period"
                  value="${opt.value}"
                  ${(values.period ?? "morning") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label>Quando aparece</label>
          <div class="task-form-type-group" role="radiogroup" aria-label="Recorrência da tarefa" id="template-form-recurrence-group">
            ${RECURRENCE_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="radio"
                  name="template-form-recurrence"
                  value="${opt.value}"
                  ${(values.recurrence ?? "daily") === opt.value ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>

          <div id="template-form-custom-days" class="task-form-type-group hidden" style="margin-top: var(--space-2);">
            ${CUSTOM_DAY_OPTIONS.map(
              (opt) => `
              <label class="task-form-type-option">
                <input
                  type="checkbox"
                  name="template-form-custom-day"
                  value="${opt.value}"
                  ${initialCustomDays.some((day) => String(day).toLowerCase() === opt.value.toLowerCase()) ? "checked" : ""}
                />
                <span>${escapeHtml(opt.label)}</span>
              </label>
            `
            ).join("")}
          </div>

          <div id="template-form-variants" class="hidden" style="margin-top: var(--space-3); display: flex; flex-direction: column; gap: var(--space-3);">
            <p class="modal-hint">Uma atividade diferente pra cada dia útil — o dia todo usa a mesma pontuação, a menos que você preencha outra abaixo.</p>
            ${WEEKDAY_ROTATION_DAYS.map(
              ({ key, label }, index) => `
              <div class="field" style="border-top: 1px solid rgba(234, 246, 243, 0.12); padding-top: var(--space-2);">
                <label for="template-form-variant-title-${index}">${escapeHtml(label)}</label>
                <input
                  id="template-form-variant-title-${index}"
                  type="text"
                  data-variant-day="${key}"
                  data-variant-field="title"
                  value="${escapeHtml(initialVariants[index]?.title ?? "")}"
                  placeholder="Título da atividade"
                />
                <textarea
                  id="template-form-variant-description-${index}"
                  class="modal-textarea"
                  rows="2"
                  data-variant-day="${key}"
                  data-variant-field="description"
                  placeholder="Descrição (opcional)"
                >${escapeHtml(initialVariants[index]?.description ?? "")}</textarea>
                <input
                  id="template-form-variant-points-${index}"
                  type="number"
                  step="1"
                  data-variant-day="${key}"
                  data-variant-field="points"
                  value="${initialVariants[index]?.points ?? ""}"
                  placeholder="Pontos (padrão: o valor de cima)"
                />
              </div>
            `
            ).join("")}
          </div>
        </div>

        <div class="field">
          <label class="task-form-checkbox">
            <input type="checkbox" id="template-form-has-options" ${initialOptions.length > 0 ? "checked" : ""} />
            <span>Tarefa com opções pra criança escolher (ex.: torre de copos / ponte de papel)</span>
          </label>
          <div id="template-form-options-list" class="task-form-options-list ${initialOptions.length > 0 ? "" : "hidden"}"></div>
        </div>

        <div class="field">
          <label>Por que essa tarefa importa (opcional)</label>
          <p class="modal-hint">Pode cadastrar mais de uma frase — o app sorteia uma diferente a cada dia, pra não repetir sempre a mesma.</p>
          <div id="template-form-reasons-list" class="task-form-options-list"></div>
        </div>

        <p class="error-text hidden" id="template-form-error"></p>

        <div class="modal-actions">
          <button type="button" class="btn btn-ghost" data-modal-action="cancel">Cancelar</button>
          <button type="button" class="btn btn-primary" data-modal-action="ok">${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `;

    document.body.appendChild(overlay);
    activeModal = overlay;

    const titleInput = overlay.querySelector("#template-form-title");
    const pointsInput = overlay.querySelector("#template-form-points");
    const hasOptionsCheckbox = overlay.querySelector("#template-form-has-options");
    const optionsList = overlay.querySelector("#template-form-options-list");
    const reasonsList = overlay.querySelector("#template-form-reasons-list");
    const errorText = overlay.querySelector("#template-form-error");
    const customDaysBlock = overlay.querySelector("#template-form-custom-days");
    const variantsBlock = overlay.querySelector("#template-form-variants");

    // Editor de lista repetivel (min/max linhas, com botao de adicionar/remover) --
    // usado tanto pras Opções quanto pros Motivos abaixo. Duplicar essa logica em
    // vez de compartilhar com promptTaskForm segue o mesmo padrão ja aceito no
    // resto do arquivo de prompts (ver comentario de promptForOptions em
    // screens/pacus.js).
    function createListEditor(container, { seeds, min, max, placeholder }) {
      let inputs = [];
      let addBtn = null;

      function updateAddButtonState() {
        if (addBtn) addBtn.disabled = inputs.length >= max;
      }

      function addRow(value) {
        const row = document.createElement("div");
        row.className = "task-form-option-row";
        row.innerHTML = `
          <input
            type="text"
            class="task-form-option-input"
            placeholder="${escapeHtml(placeholder)} ${inputs.length + 1}"
            value="${escapeHtml(value ?? "")}"
          />
          <button type="button" class="btn btn-ghost task-form-option-remove" aria-label="Remover">✕</button>
        `;
        const input = row.querySelector(".task-form-option-input");
        const removeBtn = row.querySelector(".task-form-option-remove");

        removeBtn.addEventListener("click", () => {
          if (inputs.length <= min) return;
          row.remove();
          inputs = inputs.filter((i) => i !== input);
          updateAddButtonState();
        });

        container.appendChild(row);
        inputs.push(input);
      }

      const initialSeeds = seeds.length > 0 ? seeds : (min > 0 ? Array(min).fill("") : [""]);
      initialSeeds.forEach((value) => addRow(value));
      while (inputs.length < min) addRow("");

      addBtn = document.createElement("button");
      addBtn.type = "button";
      addBtn.className = "btn btn-ghost task-form-option-add";
      addBtn.textContent = `+ Adicionar ${placeholder.toLowerCase()}`;
      addBtn.addEventListener("click", () => {
        if (inputs.length >= max) return;
        addRow("");
        updateAddButtonState();
      });
      container.after(addBtn);
      updateAddButtonState();

      return {
        getValues: () => inputs.map((i) => i.value.trim()).filter((v) => v.length > 0),
        count: () => inputs.length
      };
    }

    const optionsEditor = createListEditor(optionsList, {
      seeds: initialOptions,
      min: 2,
      max: 4,
      placeholder: "Opção"
    });

    const reasonsEditor = createListEditor(reasonsList, {
      seeds: initialReasons,
      min: 0,
      max: 6,
      placeholder: "Motivo"
    });

    hasOptionsCheckbox.addEventListener("change", () => {
      optionsList.classList.toggle("hidden", !hasOptionsCheckbox.checked);
      optionsList.nextElementSibling?.classList.toggle("hidden", !hasOptionsCheckbox.checked);
    });
    optionsList.nextElementSibling?.classList.toggle("hidden", !hasOptionsCheckbox.checked);

    function updateRecurrenceVisibility() {
      const recurrence =
        overlay.querySelector('input[name="template-form-recurrence"]:checked')?.value ?? "daily";
      customDaysBlock.classList.toggle("hidden", recurrence !== "custom");
      variantsBlock.classList.toggle("hidden", recurrence !== "weekday_rotation");
    }

    overlay
      .querySelectorAll('input[name="template-form-recurrence"]')
      .forEach((input) => input.addEventListener("change", updateRecurrenceVisibility));
    updateRecurrenceVisibility();

    titleInput.focus();

    function showError(message) {
      errorText.textContent = message;
      errorText.classList.remove("hidden");
    }

    function finish(result) {
      overlay.remove();
      if (activeModal === overlay) activeModal = null;
      resolve(result);
    }

    function submit() {
      const trimmedTitle = titleInput.value.trim();
      if (!trimmedTitle) {
        showError("O nome da tarefa é obrigatório.");
        titleInput.focus();
        return;
      }

      const points = Number(pointsInput.value);
      if (!isValidPoints(points)) {
        showError(`Pontos inválidos. ${POINTS_HELP_TEXT}.`);
        pointsInput.focus();
        return;
      }

      const type =
        overlay.querySelector('input[name="template-form-type"]:checked')?.value ?? "mandatory";
      const period =
        overlay.querySelector('input[name="template-form-period"]:checked')?.value ?? "morning";
      const recurrence =
        overlay.querySelector('input[name="template-form-recurrence"]:checked')?.value ?? "daily";

      let customDays = null;
      if (recurrence === "custom") {
        customDays = Array.from(
          overlay.querySelectorAll('input[name="template-form-custom-day"]:checked')
        ).map((input) => input.value);

        if (customDays.length === 0) {
          showError("Escolha pelo menos um dia da semana.");
          return;
        }
      }

      let variants = null;
      if (recurrence === "weekday_rotation") {
        variants = [];
        for (const { key, label } of WEEKDAY_ROTATION_DAYS) {
          const dayTitle = overlay
            .querySelector(`input[data-variant-day="${key}"][data-variant-field="title"]`)
            .value.trim();

          if (!dayTitle) {
            showError(`Falta o título da atividade de ${label}.`);
            return;
          }

          const dayDescription = overlay
            .querySelector(`textarea[data-variant-day="${key}"][data-variant-field="description"]`)
            .value.trim();

          const dayPointsRaw = overlay
            .querySelector(`input[data-variant-day="${key}"][data-variant-field="points"]`)
            .value.trim();

          const dayPoints = dayPointsRaw ? Number(dayPointsRaw) : null;
          if (dayPoints !== null && !isValidPoints(dayPoints)) {
            showError(`Pontos inválidos em ${label}. ${POINTS_HELP_TEXT}.`);
            return;
          }

          variants.push({
            dayOfWeek: key,
            title: dayTitle,
            description: dayDescription || null,
            points: dayPoints
          });
        }
      }

      if (hasOptionsCheckbox.checked && optionsEditor.getValues().length < 2) {
        showError("Uma tarefa com opções precisa de pelo menos 2 preenchidas.");
        return;
      }

      const description =
        overlay.querySelector("#template-form-description").value.trim() || null;

      finish({
        title: trimmedTitle,
        description,
        points,
        type,
        period,
        recurrence,
        customDays,
        variants,
        options: hasOptionsCheckbox.checked ? optionsEditor.getValues() : [],
        reasons: reasonsEditor.getValues()
      });
    }

    overlay
      .querySelector('[data-modal-action="ok"]')
      .addEventListener("click", submit);

    overlay
      .querySelector('[data-modal-action="cancel"]')
      .addEventListener("click", () => finish(null));

    overlay.addEventListener("click", (event) => {
      if (event.target === overlay) finish(null);
    });

    overlay.addEventListener("keydown", (event) => {
      if (event.key === "Escape") finish(null);
    });
  });
}

function closeActiveModal() {
  if (activeModal) {
    activeModal.remove();
    activeModal = null;
  }
}

function escapeHtml(value = "") {
  const div = document.createElement("div");
  div.textContent = String(value);
  return div.innerHTML;
}
