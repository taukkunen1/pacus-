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
import { promptTextarea } from "../components/modal.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

const CATEGORIES = ["screen_time", "toy", "activity", "other"];
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

  // Usado tanto por "+ Novo item" quanto por "Editar" -- se `existing` for passado, os
  // prompts vem pre-preenchidos com os valores atuais do item. Devolve null se o adulto
  // cancelar em qualquer etapa (mesmo padrão dos outros fluxos baseados em window.prompt).
  async function promptItemFields(existing = null) {
    const title = window.prompt("Nome do item:", existing?.title ?? "");
    if (!title?.trim()) return null;

    const descriptionRaw = await promptTextarea({
      title: "Descrição do item",
      label: "Descrição (opcional) — um item por linha",
      value: existing?.description ?? "",
      placeholder: "Ex.: válido só aos fins de semana"
    });

    const cost = Number(window.prompt("Custo em Pacus Points:", String(existing?.cost ?? 100)));
    if (!Number.isInteger(cost) || cost <= 0) {
      showToast("Custo inválido. Use um número inteiro maior que zero.", { error: true });
      return null;
    }

    const category = window
      .prompt(`Categoria: ${CATEGORIES.join(", ")}`, existing?.category ?? "other")
      ?.trim()
      .toLowerCase();

    if (!CATEGORIES.includes(category)) {
      showToast(`Categoria inválida. Use uma de: ${CATEGORIES.join(", ")}.`, { error: true });
      return null;
    }

    const icon = window.prompt("Emoji do item (opcional):", existing?.icon ?? "") || null;

    const stockRaw = window.prompt(
      "Estoque (deixe em branco para ilimitado):",
      existing?.stock !== null && existing?.stock !== undefined ? String(existing.stock) : ""
    );
    const stock = stockRaw?.trim() ? Number(stockRaw) : null;
    if (stock !== null && (!Number.isInteger(stock) || stock <= 0)) {
      showToast("Estoque inválido. Deixe em branco para ilimitado ou use um número inteiro maior que zero.", { error: true });
      return null;
    }

    const dailyLimitRaw = window.prompt(
      "Limite de resgates por dia (deixe em branco para sem limite):",
      existing?.dailyLimit ? String(existing.dailyLimit) : ""
    );
    const dailyLimit = dailyLimitRaw?.trim() ? Number(dailyLimitRaw) : null;
    if (dailyLimit !== null && (!Number.isInteger(dailyLimit) || dailyLimit <= 0)) {
      showToast("Limite diário inválido. Deixe em branco para sem limite ou use um número inteiro maior que zero.", { error: true });
      return null;
    }

    const screenTimeRaw = window.prompt(
      "Minutos de tempo de tela concedidos ao aprovar (deixe em branco se não for tempo de tela):",
      existing?.screenTimeMinutes ? String(existing.screenTimeMinutes) : (category === "screen_time" ? "60" : "")
    );
    const screenTimeMinutes = screenTimeRaw?.trim() ? Number(screenTimeRaw) : null;
    if (screenTimeMinutes !== null && (!Number.isInteger(screenTimeMinutes) || screenTimeMinutes <= 0)) {
      showToast("Minutos de tela inválidos. Deixe em branco ou use um número inteiro maior que zero.", { error: true });
      return null;
    }

    return {
      title: title.trim(),
      description: descriptionRaw?.trim() || null,
      cost,
      category,
      icon,
      stock,
      dailyLimit,
      screenTimeMinutes
    };
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
