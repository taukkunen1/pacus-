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
