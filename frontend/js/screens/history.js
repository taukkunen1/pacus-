import { getHistory } from "../api/history-api.js";
import { formatBrl } from "../utils/format.js";
import { showToast } from "../components/toast.js";

export async function renderHistory(root, navigate) {
  root.innerHTML = `<div class="screen"><div class="container"><h1>Histórico</h1><p class="task-empty">Carregando...</p></div></div>`;
  const content = root.querySelector(".container");
  try {
    const history = await getHistory();
    content.innerHTML = `
      <div class="screen-header"><div><p class="eyebrow">ROTINA</p><h1>Histórico</h1></div><button class="btn btn-ghost" id="back">Hoje</button></div>
      <div class="history-list">
        ${history.length ? history.map(day => {
          const pct = Math.round((day.stats?.completionRate ?? 0) * 100);
          return `<button class="history-day" data-date="${day.date}">
            <span><strong>${day.date}</strong><small>${day.stats?.mandatory?.done ?? 0}/${day.stats?.mandatory?.total ?? 0} obrigatórias</small></span>
            <span><strong>${pct}%</strong><small>+${day.pointsEarned ?? 0} PP</small></span>
          </button>`;
        }).join("") : `<p class="task-empty">Nenhum dia encerrado ainda.</p>`}
      </div>`;
    content.querySelector("#back")?.addEventListener("click", () => navigate("today"));
    content.querySelectorAll(".history-day").forEach(btn => btn.addEventListener("click", async () => {
      try {
        const day = await getHistory({ date: btn.dataset.date });
        renderHistoryDay(content, day);
      } catch (err) { showToast(err.message, { error: true }); }
    }));
  } catch (err) {
    content.innerHTML = `<p class="error-text">Não foi possível carregar o histórico: ${err.message}</p>`;
  }
}

// "valor equivalente" abaixo usa a taxa padrao (Settings.DefaultPointToBrlRate no
// backend, 0.06) direto no frontend -- esta tela nao busca a taxa configurada da
// familia. Se isso incomodar, o certo e chamar GET /api/v1/points aqui tambem e usar
// o "brl" de la, que ja reflete a taxa real da familia (ver PointsController).
function renderHistoryDay(content, day) {
  content.innerHTML = `
    <button class="btn btn-ghost" id="history-back">← Histórico</button>
    <p class="eyebrow">${day.date}</p>
    <h1>Dia encerrado</h1>
    <div class="history-summary">
      <div><strong>${Math.round((day.stats?.completionRate ?? 0) * 100)}%</strong><span>conclusão</span></div>
      <div><strong>+${day.pointsEarned ?? 0}</strong><span>Pacus Points</span></div>
      <div><strong>${formatBrl((day.pointsEarned ?? 0) * 0.06)}</strong><span>valor equivalente</span></div>
    </div>
    <div class="task-history">
      ${(day.tasks ?? []).map(task => `<div class="history-task ${task.status === "done" ? "done" : "pending"}">
        <span>${task.status === "done" ? "✓" : "○"}</span><span>${escapeHtml(task.title)}</span><strong>${task.status === "done" ? `+${task.points} PP` : "+0 PP"}</strong>
      </div>`).join("")}
    </div>`;
  content.querySelector("#history-back")?.addEventListener("click", () => location.hash = "history");
}
function escapeHtml(value = "") { const d = document.createElement("div"); d.textContent = value; return d.innerHTML; }
