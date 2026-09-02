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
