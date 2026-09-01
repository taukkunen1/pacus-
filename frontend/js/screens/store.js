import {
  getStoreItems,
  createStoreItem,
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
    const requests = [getStoreItems(), getPoints()];
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
        ${renderItems(items, balance.balance)}
      </div>

      ${isAdult ? `
        <h2>Aguardando aprovação</h2>
        <div id="pending-list">
          ${renderPending(pending)}
        </div>
      ` : ""}
    `;

    attachHandlers();
  }

  function renderItems(list, currentBalance) {
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

        return `
          <article class="task-card">
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
                ${item.stock !== null && item.stock !== undefined ? `<span>estoque: ${item.stock}</span>` : ""}
              </div>
            </div>

            <div class="task-actions">
              <button
                class="btn ${affordable ? "btn-primary" : "btn-ghost"}"
                data-store-action="redeem"
                data-item-id="${escapeHtml(String(item.id))}"
                ${affordable ? "" : "disabled"}
              >
                Resgatar
              </button>
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
      const title = window.prompt("Nome do item:");
      if (!title?.trim()) return;

      const descriptionRaw = await promptTextarea({
        title: "Descrição do item",
        label: "Descrição (opcional) — um item por linha",
        value: "",
        placeholder: "Ex.: válido só aos fins de semana"
      });

      const cost = Number(window.prompt("Custo em Pacus Points:", "100"));
      if (!Number.isInteger(cost) || cost <= 0) {
        showToast("Custo inválido. Use um número inteiro maior que zero.", { error: true });
        return;
      }

      const category = window
        .prompt(`Categoria: ${CATEGORIES.join(", ")}`, "other")
        ?.trim()
        .toLowerCase();

      if (!CATEGORIES.includes(category)) {
        showToast(`Categoria inválida. Use uma de: ${CATEGORIES.join(", ")}.`, { error: true });
        return;
      }

      const icon = window.prompt("Emoji do item (opcional):", "") || null;

      const stockRaw = window.prompt(
        "Estoque (deixe em branco para ilimitado):",
        ""
      );
      const stock = stockRaw?.trim() ? Number(stockRaw) : null;
      if (stock !== null && (!Number.isInteger(stock) || stock <= 0)) {
        showToast("Estoque inválido. Deixe em branco para ilimitado ou use um número inteiro maior que zero.", { error: true });
        return;
      }

      const dailyLimitRaw = window.prompt(
        "Limite de resgates por dia (deixe em branco para sem limite):",
        ""
      );
      const dailyLimit = dailyLimitRaw?.trim() ? Number(dailyLimitRaw) : null;
      if (dailyLimit !== null && (!Number.isInteger(dailyLimit) || dailyLimit <= 0)) {
        showToast("Limite diário inválido. Deixe em branco para sem limite ou use um número inteiro maior que zero.", { error: true });
        return;
      }

      const screenTimeRaw = window.prompt(
        "Minutos de tempo de tela concedidos ao aprovar (deixe em branco se não for tempo de tela):",
        category === "screen_time" ? "60" : ""
      );
      const screenTimeMinutes = screenTimeRaw?.trim() ? Number(screenTimeRaw) : null;
      if (screenTimeMinutes !== null && (!Number.isInteger(screenTimeMinutes) || screenTimeMinutes <= 0)) {
        showToast("Minutos de tela inválidos. Deixe em branco ou use um número inteiro maior que zero.", { error: true });
        return;
      }

      try {
        await createStoreItem({
          title: title.trim(),
          description: descriptionRaw?.trim() || null,
          cost,
          category,
          icon,
          stock,
          dailyLimit,
          screenTimeMinutes
        });

        showToast("Item criado.");
        await refresh();
      } catch (err) {
        showToast(err.message, { error: true });
      }
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
