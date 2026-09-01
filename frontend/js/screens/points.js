import { getPoints, getPointTransactions } from "../api/points-api.js";
import { formatBrl } from "../utils/format.js";
import { showToast } from "../components/toast.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

const PAGE_SIZE = 20;

export async function renderPoints(root, navigate) {
  root.innerHTML = `<div class="screen"><div class="container"><p class="task-empty">Carregando Pacus Points...</p></div></div>`;
  const content = root.querySelector(".container");

  let balance;
  let transactions = [];
  let page = 1;
  let totalPages = 1;

  try {
    const [balanceResult, transactionsResult] = await Promise.all([
      getPoints(),
      getPointTransactions({ page, pageSize: PAGE_SIZE })
    ]);
    balance = balanceResult;
    transactions = transactionsResult.items ?? [];
    totalPages = transactionsResult.totalPages ?? 1;
  } catch (err) {
    content.innerHTML = `<p class="error-text">Não foi possível carregar os pontos: ${err.message}</p>`;
    return;
  }

  function draw() {
    content.innerHTML = `
      <div class="screen-header"><div><p class="eyebrow">RECOMPENSAS</p><h1>Pacus Points</h1></div><button class="btn btn-ghost" id="back">Hoje</button></div>
      <section class="points-hero"><strong>${balance.balance}</strong><span>Pacus Points</span><small>${formatBrl(balance.brl)}</small></section>
      <h2>Movimentações</h2>
      <div class="history-list">${transactions.length ? transactions.map(t => `<div class="history-day"><span><strong>${escapeHtml(t.taskTitle || t.reason || t.type)}</strong><small>${t.date || ""}</small></span><strong>${t.points > 0 ? "+" : ""}${t.points} PP</strong></div>`).join("") : `<p class="task-empty">Nenhuma movimentação.</p>`}</div>
      ${page < totalPages ? `<button class="btn btn-ghost" id="load-more">Carregar mais</button>` : ""}
      ${renderBottomNav("points")}`;
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));
    attachBottomNav(content, navigate);
    content.querySelector("#load-more")?.addEventListener("click", async (event) => {
      const button = event.currentTarget;
      button.disabled = true;
      try {
        const next = await getPointTransactions({ page: page + 1, pageSize: PAGE_SIZE });
        transactions = transactions.concat(next.items ?? []);
        page += 1;
        totalPages = next.totalPages ?? totalPages;
        draw();
      } catch (err) {
        button.disabled = false;
        showToast(err.message, { error: true });
      }
    });
  }

  draw();
}
function escapeHtml(value = "") { const d = document.createElement("div"); d.textContent = value; return d.innerHTML; }
