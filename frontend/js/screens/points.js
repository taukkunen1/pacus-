import { getPoints, getPointTransactions } from "../api/points-api.js";
import { formatBrl } from "../utils/format.js";
import { renderBottomNav, attachBottomNav } from "../components/bottom-nav.js";

export async function renderPoints(root, navigate) {
  root.innerHTML = `<div class="screen"><div class="container"><p class="task-empty">Carregando Pacus Points...</p></div></div>`;
  const content = root.querySelector(".container");
  try {
    const [balance, transactions] = await Promise.all([getPoints(), getPointTransactions()]);
    content.innerHTML = `
      <div class="screen-header"><div><p class="eyebrow">RECOMPENSAS</p><h1>Pacus Points</h1></div><button class="btn btn-ghost" id="back">Hoje</button></div>
      <section class="points-hero"><strong>${balance.balance}</strong><span>Pacus Points</span><small>${formatBrl(balance.brl)}</small></section>
      <h2>Movimentações</h2>
      <div class="history-list">${(transactions ?? []).length ? transactions.map(t => `<div class="history-day"><span><strong>${escapeHtml(t.taskTitle || t.reason || t.type)}</strong><small>${t.date || ""}</small></span><strong>${t.points > 0 ? "+" : ""}${t.points} PP</strong></div>`).join("") : `<p class="task-empty">Nenhuma movimentação.</p>`}</div>
      ${renderBottomNav("points")}`;
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));
    attachBottomNav(content, navigate);
  } catch (err) { content.innerHTML = `<p class="error-text">Não foi possível carregar os pontos: ${err.message}</p>`; }
}
function escapeHtml(value = "") { const d = document.createElement("div"); d.textContent = value; return d.innerHTML; }
