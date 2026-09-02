import {
  getStoreItems,
  getAllStoreItems,
  createStoreItem,
  updateStoreItem,
  setStoreItemActive,
  requestRedemption,
  getPendingRedemptions,
  approveRedemption,
  rejectRedemption
} from "../api/store-api.js";
import { getPoints } from "../api/points-api.js";
import { showToast } from "../components/toast.js";
import { appState } from "../state/app-state.js";
import { formatBrl } from "../utils/format.js";
import { promptStoreItemForm } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

const CATEGORY_LABELS = {
  screen_time: "Tempo de tela",
  toy: "Brinquedo",
  activity: "Atividade",
  other: "Outro"
};

export async function renderStore(root, navigate = () => {}) {
  const role = appState.user?.role ?? "";
  const isAdult = role.toLowerCase() === "adult";

  root.innerHTML = `
    <div class="screen">
      <div class="container" id="store-content">
        <p class="task-empty">Carregando loja...</p>
      </div>
    </div>
  `;

  const content = root.querySelector("#store-content");

  let items = [];
  let pending = [];
  let balance = { balance: 0, brl: 0 };

  async function load() {
    // Adulto ve tambem os itens desativados (pra poder reativar); crianca so ve os ativos.
    const requests = [isAdult ? getAllStoreItems() : getStoreItems(), getPoints()];
    if (isAdult) requests.push(getPendingRedemptions());

    const results = await Promise.all(requests);
    items = results[0] ?? [];
    balance = results[1] ?? { balance: 0, brl: 0 };
    pending = isAdult ? (results[2] ?? []) : [];
  }

  function draw() {
    content.innerHTML = `
      <div class="screen-header">
        <div>
          <p class="eyebrow">RECOMPENSAS</p>
          <h1>Loja de Pacus Points</h1>
        </div>
        <button class="btn btn-ghost" id="back">Hoje</button>
      </div>

      <section class="points-hero">
        <strong>${balance.balance}</strong>
        <span>Pacus Points</span>
        <small>${formatBrl(balance.brl)}</small>
      </section>

      <div class="screen-header">
        <h2>Itens da loja</h2>
        ${isAdult ? `<button class="btn btn-primary" id="add-item">+ Novo item</button>` : ""}
      </div>

      <div id="store-items">
        ${renderItems(items, balance.balance, isAdult)}
      </div>

      ${isAdult ? `
        <h2>Aguardando aprovação</h2>
        <div id="pending-list">
          ${renderPending(pending)}
        </div>
      ` : ""}

      ${renderBottomNav("store")}
    `;

    attachHandlers();
    attachBottomNav(content, navigate);
  }

  function renderItems(list, currentBalance, isAdultView) {
    if (!list.length) {
      return `<p class="task-empty">Nenhum item na loja ainda.</p>`;
    }

    return list
      .map((item) => {
        const affordable = currentBalance >= item.cost;
        const limitNote = item.dailyLimit
          ? `<span>limite ${item.dailyLimit}x/dia</span>`
          : "";
        const screenTimeNote = item.screenTimeMinutes
          ? `<span>+${item.screenTimeMinutes}min de tela</span>`
          : "";
        const inactiveNote = item.active === false ? `<span>desativado</span>` : "";

        const adultActions = isAdultView
          ? `
            <button class="btn btn-ghost" data-store-action="edit" data-item-id="${escapeHtml(String(item.id))}">
              Editar
            </button>
            <button class="btn btn-ghost" data-store-action="toggle-active" data-item-id="${escapeHtml(String(item.id))}" data-active="${item.active !== false}">
              ${item.active === false ? "Reativar" : "Desativar"}
            </button>
          `
          : "";

        return `
          <article class="task-card${item.active === false ? " task-card--inactive" : ""}">
            <div class="task-card__content">
              <strong class="task-title">
                ${item.icon ? `${escapeHtml(item.icon)} ` : ""}${escapeHtml(item.title)}
              </strong>

              ${item.description ? `<span class="task-description">${escapeHtml(item.description)}</span>` : ""}

              <div class="task-meta">
                <span>${CATEGORY_LABELS[item.category] ?? item.category}</span>
                <span>${item.cost} PP</span>
                ${limitNote}
                ${screenTimeNote}
                ${inactiveNote}
                ${item.stock !== null && item.stock !== undefined ? `<span>estoque: ${item.stock}</span>` : ""}
              </div>
            </div>

            <div class="task-actions">
              ${item.active !== false ? `
                <button
                  class="btn ${affordable ? "btn-primary" : "btn-ghost"}"
                  data-store-action="redeem"
                  data-item-id="${escapeHtml(String(item.id))}"
                  ${affordable ? "" : "disabled"}
                >
                  Resgatar
                </button>
              ` : ""}
              ${adultActions}
            </div>
          </article>
        `;
      })
      .join("");
  }

  function renderPending(list) {
    if (!list.length) {
      return `<p class="task-empty">Nenhum resgate aguardando aprovação.</p>`;
    }

    return list
      .map(
        (redemption) => `
          <article class="task-card">
            <div class="task-card__content">
              <strong class="task-title">${escapeHtml(redemption.itemTitle)}</strong>
              <div class="task-meta">
                <span>${redemption.cost} PP</span>
              </div>
            </div>

            <div class="task-actions">
              <button class="btn btn-primary" data-pending-action="approve" data-redemption-id="${escapeHtml(String(redemption.id))}">
                Aprovar
              </button>
              <button class="btn btn-ghost" data-pending-action="reject" data-redemption-id="${escapeHtml(String(redemption.id))}">
                Rejeitar
              </button>
            </div>
          </article>
        `
      )
      .join("");
  }

  async function refresh() {
    await load();
    draw();
  }

  function attachHandlers() {
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));

    content.querySelector("#add-item")?.addEventListener("click", async () => {
      const payload = await promptItemFields();
      if (!payload) return;

      try {
        await createStoreItem(payload);
        showToast("Item criado.");
        await refresh();
      } catch (err) {
        showToast(err.message, { error: true });
      }
    });

    content.querySelectorAll("[data-store-action=edit]").forEach((button) => {
      button.addEventListener("click", async () => {
        const itemId = button.dataset.itemId;
        const existing = items.find((item) => String(item.id) === itemId);
        if (!itemId || !existing) return;

        const payload = await promptItemFields(existing);
        if (!payload) return;

        try {
          await updateStoreItem(itemId, payload);
          showToast("Item atualizado.");
          await refresh();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });
    });

    content.querySelectorAll("[data-store-action=toggle-active]").forEach((button) => {
      button.addEventListener("click", async () => {
        const itemId = button.dataset.itemId;
        const isCurrentlyActive = button.dataset.active === "true";
        if (!itemId) return;

        try {
          await setStoreItemActive(itemId, !isCurrentlyActive);
          showToast(isCurrentlyActive ? "Item desativado." : "Item reativado.");
          await refresh();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });
    });

    content.querySelectorAll("[data-store-action=redeem]").forEach((button) => {
      button.addEventListener("click", async () => {
        const itemId = button.dataset.itemId;
        if (!itemId) return;

        try {
          await requestRedemption(itemId);
          showToast("Resgate solicitado! Aguarde a aprovação de um adulto.");
          await refresh();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });
    });

    content.querySelectorAll("[data-pending-action=approve]").forEach((button) => {
      button.addEventListener("click", async () => {
        const id = button.dataset.redemptionId;
        if (!id) return;

        try {
          await approveRedemption(id);
          showToast("Resgate aprovado.");
          await refresh();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });
    });

    content.querySelectorAll("[data-pending-action=reject]").forEach((button) => {
      button.addEventListener("click", async () => {
        const id = button.dataset.redemptionId;
        if (!id) return;

        try {
          await rejectRedemption(id);
          showToast("Resgate rejeitado.");
          await refresh();
        } catch (err) {
          showToast(err.message, { error: true });
        }
      });
    });
  }

  // Usado tanto por "+ Novo item" quanto por "Editar" -- painel unico
  // (components/modal.js promptStoreItemForm), mesmo padrao do editor de
  // tarefas: todos os campos visiveis de uma vez, com Categoria como grupo de
  // botoes em vez de pedir pra digitar "screen_time"/"toy"/... por extenso.
  // Devolve null se o adulto cancelar.
  async function promptItemFields(existing = null) {
    return promptStoreItemForm({
      title: existing ? "Editar item" : "Novo item",
      values: existing ?? {},
      confirmLabel: existing ? "Salvar" : "Adicionar"
    });
  }

  try {
    await load();
    draw();
  } catch (err) {
    content.innerHTML = `<p class="error-text">Não foi possível carregar a loja: ${escapeHtml(err.message)}</p>`;
  }
}

function escapeHtml(value = "") {
  const div = document.createElement("div");
  div.textContent = String(value);
  return div.innerHTML;
}
